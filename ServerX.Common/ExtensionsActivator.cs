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
		void Init(string dirName, bool outputToConsole);
		void RunExtensions(Guid guid, string runDebugMethodOnExtension, params string[] ids);
		void SignalCancellation();
		void RunMainAppThread();
	}

	[Serializable]
	internal class ExtensionsActivator : MarshalByRefObject, IExtensionsActivator
	{
		private ExtensionInfo[] _infos;
		private Logger _logger;

		public void Init(string dirName, bool outputToConsole)
		{
			_dirName = dirName;
			_logger = new Logger("extension-" + dirName) { WriteToConsole = outputToConsole };

			AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
			TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;

			var list = new List<ExtensionInfo>();
			var files = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).GetFiles("*.dll");
			var asmExclusionsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "loader.exclude.txt");
			var exclusions = new HashSet<string>();
			if(File.Exists(asmExclusionsPath))
				exclusions = new HashSet<string>(
				File.ReadAllLines(asmExclusionsPath)
					.Select(s => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, s).ToLower())
					.Where(File.Exists));

			foreach(var file in files)
			{
				if(exclusions.Contains(file.FullName.ToLower()))
					continue;
				try
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
				catch(BadImageFormatException)
				{
					continue;
				}
			}
			_infos = list.ToArray();
			_logger.WriteLine("Extensions Activator", "Obtained info for " + _infos.Length + " available extensions");
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
		private bool _allExtensionsStarted;
		private CancellationTokenSource _cancelSource = new CancellationTokenSource();
		private string _dirName;

		public void RunExtensions(Guid guid, string runDebugMethodOnExtension, params string[] ids)
		{
			if(ids.Length == 0)
				ids = _infos.Select(i => i.ID).ToArray();
			_logger.WriteLines(
				"Extensions Activator", "Starting extensions:",
				"	=> " + string.Join(", ", ids),
				"	=> process monitor ID: " + guid
			);

			lock(this)
			{
				foreach(var id in ids)
					_runningExtensions.Add(id, Activate(id));
			}

			const TaskCreationOptions atp = TaskCreationOptions.AttachedToParent;
			var task = Task.Factory.StartNew(() =>
			{
				try
				{
					_logger.WriteLine("Extension activation/monitoring task starting...");
					lock(this)
					{
						foreach(var ext in _runningExtensions.Values)
						{
							var extension = ext.Extension;
							ext.Task = Task.Factory.StartNew(() => extension.Run(_cancelSource, _logger), _cancelSource.Token, atp, TaskScheduler.Current);
						}
					}
					_allExtensionsStarted = true;
					IEnumerable<RunningExtension> debugs;
					lock(this)
						debugs = _runningExtensions.Values.Where(ext => ext.Extension.ID == runDebugMethodOnExtension);
					foreach(var ext in debugs)
					{
						if(ext.Extension.ID == runDebugMethodOnExtension)
						{
							Thread.Sleep(1000); // give the extension a chance to start up
							ext.Extension.Debug();
						}
					}


					ServiceManagerClient client = null;
					if(guid != Guid.Empty)
					{
						_logger.WriteLine("Monitor", "Connecting to Service Manager...");
						client = new ServiceManagerClient("ServiceManagerClient");
						client.Disconnected += c =>
						{
							_logger.WriteLine("Monitor/Client", "Client state changed to: " + c.State);
							_logger.WriteLine("Monitor/Client", "Disconnected from service manager. Execution on all extensions will be cancelled now to allow the process to shut down.");
							_cancelSource.Cancel(); // process manager will take care of restarting this process
						};
						foreach(var ext in _runningExtensions.Values)
						{
							// Don't notify the server until Run has been called, otherwise the extension's Logger won't be available
							while(!ext.Extension.RunCalled)
								Thread.Sleep(250);
							_logger.WriteLine("Monitor", "Sending extension connection address: " + ext.Address);
							client.NotifyExtensionServiceReady(guid, ext.Address);
						}
						_logger.WriteLine("Monitor", "Connected.");
					}

					_logger.WriteLine("Monitor", "" + _runningExtensions.Count + " extensions now running.");
					var exitMsg = " Execution on all extensions will be cancelled now to allow the process to restart.";
					while(!_cancelSource.IsCancellationRequested)
					{
						foreach(var ext in _runningExtensions.Values)
						{
							if(!ext.Extension.IsRunning)
								_logger.WriteLine("Monitor", "Extension {" + ext.Extension.Name + "} IsRunning == false." + exitMsg);
							if(ext.Task.IsCompleted)
								_logger.WriteLine("Monitor", "Extension {" + ext.Extension.Name + "} Task.IsCompleted == true." + exitMsg);
							if(ext.Task.IsFaulted)
							{
								_logger.WriteLine("Monitor", "Extension {" + ext.Extension.Name + "} Task.IsFaulted == true." + exitMsg);
								_logger.WriteLine("Monitor", "The exception thrown by the task was: " + ext.Task.Exception);
							}
							if(!ext.Extension.IsRunning || ext.Task.IsCompleted || ext.Task.IsFaulted)
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
					_logger.WriteLine("Monitor", "Extension state monitoring terminating.");
				}
				catch(Exception ex)
				{
					_logger.WriteLines("Monitor", "EXTENSION MONITORING TASK EXCEPTION:", ex);
				}
			}, _cancelSource.Token, atp, TaskScheduler.Current);

			_logger.WriteLine("Extensions Activator", "Waiting on task threads to finish...");
			try
			{
				task.Wait(_cancelSource.Token);
			}
			catch(OperationCanceledException)
			{
			}
			_logger.WriteLine("Extensions Activator", "Task threads have all ended.");
		}

		/// <summary>
		/// This method is only for specialised extensions that have processes requiring execution in the main thread.
		/// In general, these sorts of extensions should be run in isolation from other extensions. If multiple active
		/// extensions are found that want to run in the main app thread, an exception will be thrown as they should be
		/// run in separate processes. This method will block until extensions are loaded and running.
		/// </summary>
		public void RunMainAppThread()
		{
			while(!_allExtensionsStarted && !_cancelSource.IsCancellationRequested)
				Thread.Sleep(100);
			IEnumerable<RunningExtension> list;
			lock(this)
				list = _runningExtensions.Values.Where(e => e.Extension.HasMainLoop);
			if(list.Count() > 1)
				throw new Exception("Multiple active extensions were found that have RunMainAppThreadLoop() implementations. Only one active extension is allowed to return true for HasMainLoop.");
			var ext = list.FirstOrDefault();
			if(ext != null && !_cancelSource.IsCancellationRequested)
			{
				while(!ext.Extension.RunCalled)
					Thread.Yield();
				ext.Extension.RunMainAppThreadLoop(_cancelSource, _logger);
			}
		}

		public void SignalCancellation()
		{
			_cancelSource.Cancel();
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

			var port = ConfigurationManager.AppSettings[string.Concat(_dirName, ".", id, ".Port")];
			var endPoint = new ServiceEndpoint(
				ContractDescription.GetContract(ext.ContractType),
				new NetTcpBinding("Default"),
				new EndpointAddress(string.Concat("net.tcp://localhost:", (port ?? "0"), "/", id))
			) { ListenUriMode = port == null ? ListenUriMode.Unique : ListenUriMode.Explicit };
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