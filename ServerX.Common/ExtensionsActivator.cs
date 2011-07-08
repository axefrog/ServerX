using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace ServerX.Common
{
	internal interface IExtensionsActivator : IDisposable
	{
		ExtensionInfo[] Extensions { get; }
		void Init(string dirName);
		void RunExtensions(Guid guid, params string[] ids);
	}

	[Serializable]
	internal class ExtensionsActivator : MarshalByRefObject, IExtensionsActivator
	{
		private ExtensionInfo[] _infos;
		private Logger _logger;

		public void Init(string dirName)
		{
			_dirName = dirName;
			_logger = new Logger("extension-" + dirName);

			AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
			TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;

			var list = new List<ExtensionInfo>();
			var files = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).GetFiles("*.dll");
			foreach(var file in files)
			{
				var asm = Assembly.Load(Path.GetFileNameWithoutExtension(file.Name));
				var typeMap = (from t in asm.GetTypes()
							   where t.GetInterfaces().Any(i => i == typeof(IServerExtension)) && t.IsClass && !t.IsAbstract && t.GetConstructors().Where(i => i.GetParameters().Count() == 0).Any()
							   select new { Ext = (IServerExtension)Activator.CreateInstance(t) }).ToDictionary(k => k.Ext.ID, v => v.Ext);
				list.AddRange(
					typeMap.Values.Select(ext => new ExtensionInfo
					{
						ID = ext.ID,
						Name = ext.Name,
						Description = ext.Description,
						AssemblyQualifiedName = ext.GetType().AssemblyQualifiedName
					})
				);
			}
			_infos = list.ToArray();
			_logger.WriteLine("[MAIN] Obtained info for " + _infos.Length + " available extensions");
		}

		public ExtensionInfo[] Extensions
		{
			get { return _infos; }
		}

		class RunningExtension
		{
			public ServerExtension Extension { get; set; }
			public ServiceHost Host { get; set; }
			public string Address { get; set; }
			public Task Task { get; set; }
		}
		private Dictionary<string, RunningExtension> _runningExtensions = new Dictionary<string, RunningExtension>();
		private CancellationTokenSource _cancelSource;
		private string _dirName;

		public void RunExtensions(Guid guid, params string[] ids)
		{
			if(ids.Length == 0)
				ids = _infos.Select(i => i.ID).ToArray();
			_logger.WriteLines(
				"[MAIN] Starting extensions:",
				"	=> " + string.Join(", ", ids),
				"	=> process monitor ID: " + guid
			);

			lock(this)
			{
				foreach(var id in ids)
					_runningExtensions.Add(id, Activate(id));
				_cancelSource = new CancellationTokenSource();
			}

			const TaskCreationOptions atp = TaskCreationOptions.AttachedToParent;
			var task = Task.Factory.StartNew(() =>
			{
				try
				{
					_logger.WriteLine("Extension activation/monitoring task starting...");
					lock(this)
						foreach(var ext in _runningExtensions.Values)
						{
							var extension = ext.Extension;
							ext.Task = Task.Factory.StartNew(() => extension.Run(_cancelSource, _logger), _cancelSource.Token, atp, TaskScheduler.Current);
						}

					ServiceManagerClient client = null;
					if(guid != Guid.Empty)
					{
						_logger.WriteLine("[MONITOR] Connecting to Service Manager...");
						client = new ServiceManagerClient("ServiceManagerClient");
						client.Disconnected += c =>
						{
							_logger.WriteLine("[MONITOR/CLIENT] Client state changed to: " + c.State);
							_logger.WriteLine("[MONITOR/CLIENT] Disconnected from service manager. Execution on all extensions will be cancelled now to allow the process to shut down.");
							_cancelSource.Cancel(); // process manager will take care of restarting this process
						};
						foreach(var ext in _runningExtensions.Values)
						{
							_logger.WriteLine("[MONITOR] Sending extension connection address: " + ext.Address);
							client.NotifyExtensionServiceReady(ext.Address);
						}
						_logger.WriteLine("[MONITOR] Connected.");
					}

					_logger.WriteLine("[MONITOR] " + _runningExtensions.Count + " extensions now running.");
					while(!_cancelSource.IsCancellationRequested)
					{
						foreach(var ext in _runningExtensions.Values)
							if(!ext.Extension.IsRunning || ext.Task.IsCompleted || ext.Task.IsFaulted)
							{
								_logger.WriteLine("[MONITOR] Extension {" + ext.Extension.Name + "} is no longer running. Execution on all extensions will be cancelled now to allow the process to restart.");
								_cancelSource.Cancel();
							}
						if(client != null)
						{
							client.KeepExtensionProcessAlive(guid);
						}
						Thread.Sleep(3000);
					}
				}
				catch(ThreadAbortException)
				{
					_logger.WriteLine("[MONITOR] Extension state monitoring terminating.");
				}
				catch(Exception ex)
				{
					_logger.WriteLines("[MONITOR] EXTENSION MONITORING TASK EXCEPTION:", ex);
				}
			}, _cancelSource.Token, atp, TaskScheduler.Current);

			_logger.WriteLine("[MAIN] Waiting on task threads to finish...");
			try
			{
				task.Wait(_cancelSource.Token);
			}
			catch(OperationCanceledException)
			{
			}
			_logger.WriteLine("[MAIN] Task threads have all ended.");
		}

		RunningExtension Activate(string id)
		{
			var info = _infos.FirstOrDefault(i => i.ID == id);
			if(info == null)
				throw new Exception("Cannot run extension with ID [" + id + "] - extension does not exist");
			var type = Type.GetType(info.AssemblyQualifiedName);
			var ext = (ServerExtension)Activator.CreateInstance(type);
			var host = new ServiceHost(ext);
			host.Opening += (s,e) => OnServiceHostOpening(id);
			host.Opened += (s,e) => OnServiceHostOpened(id);
			host.Faulted += (s,e) => OnServiceHostFaulted(id);
			host.Closing += (s,e) => OnServiceHostClosing(id);
			host.Closed += (s,e) => OnServiceHostClosed(id);
			host.UnknownMessageReceived += (s,e) => OnServiceHostUnknownMessageReceived(id, e);

			var endPoint = new ServiceEndpoint(
				ContractDescription.GetContract(ext.ContractType),
				new NetTcpBinding("Default"),
				new EndpointAddress(string.Concat("net.tcp://localhost:", (ConfigurationManager.AppSettings[string.Concat(_dirName, ".", id, ".Port")] ?? "0"), "/", id))
			) { ListenUriMode = ListenUriMode.Unique };
			host.AddServiceEndpoint(endPoint);
			host.Open();

			var addr = host.ChannelDispatchers.First().Listener.Uri.ToString();

			return new RunningExtension { Extension = ext, Host = host, Address = addr };
		}

		void OnTaskSchedulerUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			_logger.WriteLines("UNOBSERVED TASK EXCEPTION:", e.Exception, Environment.NewLine);
			throw e.Exception;
		}

		void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			_logger.WriteLines("UNHANDLED APPDOMAIN EXCEPTION:", e.ExceptionObject, Environment.NewLine);
			throw (Exception)e.ExceptionObject;
		}

		void OnServiceHostUnknownMessageReceived(string id, UnknownMessageReceivedEventArgs e)
		{
			var writer = new StringWriter();
			var dict = XmlDictionaryWriter.CreateDictionaryWriter(XmlWriter.Create(writer));
			e.Message.WriteMessage(dict);
			_logger.WriteLines(string.Concat("[", id, "] ", "UNKNOWN MESSAGE RECEIVED:"), writer);
		}

		void OnServiceHostOpened(string id)
		{
			_logger.WriteLine(string.Concat("[", id, "] ", "Service host opened."));
		}

		void OnServiceHostOpening(string id)
		{
			_logger.WriteLine(string.Concat("[", id, "] ", "Service host opening... "));
		}

		void OnServiceHostFaulted(string id)
		{
			_logger.WriteLine(string.Concat("[", id, "] ", "Service host faulted!"));
			throw new Exception("Service Host Faulted!");
		}

		void OnServiceHostClosing(string id)
		{
			_logger.WriteLine(string.Concat("[", id, "] ", "Service host closing..."));
		}

		void OnServiceHostClosed(string id)
		{
			_logger.WriteLine(string.Concat("[", id, "] ", "Service host closed."));
		}

		public void Dispose()
		{
			lock(this)
				if(_runningExtensions != null)
				{
					foreach(var id in _runningExtensions.Keys.ToArray()) // ToArray => prevents exceptions being thrown due to modification of the original collection during iteration
					{
						var ext = _runningExtensions[id];
						_runningExtensions.Remove(id);
						if(ext.Extension is IDisposable)
							((IDisposable)ext.Extension).Dispose();
					}
					_runningExtensions = null;
				}
		}
	}
}