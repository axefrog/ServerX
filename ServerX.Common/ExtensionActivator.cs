using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using NLog;
using NLog.Config;

namespace ServerX.Common
{
	internal interface IExtensionActivator : IDisposable
	{
		ExtensionInfo[] Extensions { get; }
		void Init(string dirName, string parentProcessId);
		void RunExtension(Guid guid, bool runDebugMethodOnExtension, string extensionID);
		void SignalCancellation();
		void RunMainAppThread();
	}

	[Serializable]
	internal class ExtensionActivator : MarshalByRefObject, IExtensionActivator
	{
		private ExtensionInfo[] _infos;
		private Logger _logger;

		public void Init(string dirName, string parentProcessId)
		{
			var exeDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.FullName;
			GlobalDiagnosticsContext.Set("ExeBaseDir", exeDir);
			GlobalDiagnosticsContext.Set("SubDirName", dirName);
			GlobalDiagnosticsContext.Set("ParentProcess", parentProcessId);

			ConfigurationItemFactory.Default.Targets.RegisterDefinition("ServiceManager", typeof(ServiceManagerTarget));
			ConfigurationItemFactory.Default.Targets.RegisterDefinition("ServiceManagerNotification", typeof(NLog.Targets.NullTarget));

			_dirName = dirName;
			_logger = LogManager.GetCurrentClassLogger();

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
					var types = (from t in asm.GetTypes()
								 where t.GetInterfaces().Any(i => i == typeof(IServerExtension)) && t.IsClass && !t.IsAbstract && t.GetConstructors().Where(i => i.GetParameters().Count() == 0).Any()
								 select t);
					var typeMap = (from t in types
								   select new { Ext = (IServerExtension)Activator.CreateInstance(t) }).ToDictionary(k => k.Ext.ID, v => v.Ext);
					list.AddRange(
						typeMap.Values.Select(ext => new ExtensionInfo
						{
							ExtensionID = ext.ID,
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
			_logger.Info("Obtained info for " + _infos.Length + " available extensions");
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
		private RunningExtension _runningExtension;
		private bool _extensionStarted;
		private CancellationTokenSource _cancelSource = new CancellationTokenSource();
		private string _dirName;

		public void RunExtension(Guid guid, bool runDebugMethodOnExtension, string extensionID)
		{
			_logger.Info("Starting extension: {0} -- process monitor ID: {1}", extensionID, guid);

			lock(this)
				_runningExtension = Activate(extensionID);
			var ext = _runningExtension.Extension;

			Thread extThread = new Thread(new ThreadStart(() => 
			{
			//const TaskCreationOptions atp = TaskCreationOptions.AttachedToParent | TaskCreationOptions.LongRunning;
			//var task = Task.Factory.StartNew(() =>
			//{
				try
				{
					_logger.Debug("Extension activation/monitoring task starting...");
					lock (this)
						_runningExtension.Task = Task.Factory.StartNew(() => ext.Run(_cancelSource), TaskCreationOptions.LongRunning);
						//_runningExtension.Task = Task.Factory.StartNew(() => ext.Run(_cancelSource), _cancelSource.Token, atp, TaskScheduler.Current);
					_extensionStarted = true;
					if(runDebugMethodOnExtension)
					{
						while(!ext.RunCalled && !_cancelSource.IsCancellationRequested)
							Thread.Sleep(250); // give the extension a chance to start up
						_cancelSource.Token.ThrowIfCancellationRequested();
						ext.Debug();
					}

					ServiceManagerClient client = null;
					if(guid != Guid.Empty)
					{
						_logger.Info("Connecting to Service Manager...");
						client = new ServiceManagerClient("ServiceManagerClient");
						client.Disconnected += c =>
						{
							_logger.Info("Client state changed to: " + c.State);
							_logger.Info("Disconnected from service manager. Execution on all extensions will be cancelled now to allow the process to shut down.");
							_cancelSource.Cancel(); // process manager will take care of restarting this process
						};

						// Don't notify the server until Run has been called, otherwise the extension's Logger won't be available
						while(!ext.RunCalled)
							Thread.Sleep(250);
						_logger.Info("Sending extension connection address: " + _runningExtension.Address);
						client.NotifyExtensionServiceReady(guid, _runningExtension.Address);
						_logger.Info("Connected.");
						
					}

					_logger.Info("Extension is now running.");
					const string exitMsg = " Execution on all extensions will be cancelled now to allow the process to restart.";
					while(!_cancelSource.IsCancellationRequested)
					{
						if(!ext.IsRunning)
							_logger.Info("Extension {" + ext.Name + "} IsRunning == false." + exitMsg);
						if(_runningExtension.Task.IsCompleted)
							_logger.Info("Extension {" + ext.Name + "} Task.IsCompleted == true." + exitMsg);
						if(_runningExtension.Task.IsFaulted)
						{
							_logger.Info("Extension {" + ext.Name + "} Task.IsFaulted == true." + exitMsg);
							_logger.Info("The exception thrown by the task was: " + _runningExtension.Task.Exception);
						}
						if(!ext.IsRunning || _runningExtension.Task.IsCompleted || _runningExtension.Task.IsFaulted)
							_cancelSource.Cancel();
						if(client != null)
						{
							client.KeepExtensionProcessAlive(guid);
						}
						Thread.Sleep(3000);
					}
				}
				catch(ThreadAbortException)
				{
					_logger.Warn("Extension state monitoring terminating.");
				}
				catch(Exception ex)
				{
					_logger.FatalException("EXTENSION MONITORING TASK EXCEPTION:", ex);
				}
			})); //, _cancelSource.Token, atp, TaskScheduler.Current);

			extThread.Start();

			_logger.Info("Waiting on task threads to finish...");
			try
			{
				extThread.Join();
				//task.Wait(_cancelSource.Token);
			}
			catch (ThreadAbortException) { }
			catch (ThreadInterruptedException) { }
			catch (ThreadStateException) { }
			catch (OperationCanceledException)
			{
			}
			_logger.Info("Task threads have all ended.");
		}

		/// <summary>
		/// This method is only for specialised extensions that have processes requiring execution in the main thread.
		/// In general, these sorts of extensions should be run in isolation from other extensions. This method will
		/// block until the extensions is loaded and running.
		/// </summary>
		public void RunMainAppThread()
		{
			while(!_extensionStarted && !_cancelSource.IsCancellationRequested)
				Thread.Sleep(100);
			if(_runningExtension.Extension.HasMainLoop && !_cancelSource.IsCancellationRequested)
			{
				while(!_runningExtension.Extension.RunCalled)
					Thread.Yield();
				_runningExtension.Extension.RunMainAppThreadLoop(_cancelSource);
			}
		}

		public void SignalCancellation()
		{
			_cancelSource.Cancel();
		}

		RunningExtension Activate(string id)
		{
			var info = _infos.FirstOrDefault(i => i.ExtensionID == id);
			if(info == null)
				throw new Exception("Cannot run extension with ID [" + id + "] - extension does not exist");
			var type = Type.GetType(info.AssemblyQualifiedName);
			var ext = (ServerExtension)Activator.CreateInstance(type);
			ServiceManagerTarget.Extension = ext;
			ext.Init();

			var host = new ServiceHost(ext);
			host.Opening += (s,e) => OnServiceHostOpening(id);
			host.Opened += (s,e) => OnServiceHostOpened(id);
			host.Faulted += (s,e) => OnServiceHostFaulted(id);
			host.Closing += (s,e) => OnServiceHostClosing(id);
			host.Closed += (s,e) => OnServiceHostClosed(id);
			host.UnknownMessageReceived += (s,e) => OnServiceHostUnknownMessageReceived(id, e);

			var port = ConfigurationManager.AppSettings[string.Concat(_dirName, ".", id, ".Port")];
			// if a port was specified, make sure it's a valid port and that nothing else is already using it. otherwise just use a random free port.
			if(port != null)
			{
				int p;
				IPGlobalProperties igp;
				if(!(int.TryParse(port, out p)
					&& p >= 0 && p < 65535
					&& !(igp = IPGlobalProperties.GetIPGlobalProperties()).GetActiveTcpListeners().Any(k => k.Port == p)
					&& !igp.GetActiveUdpListeners().Any(k => k.Port == p)))
					port = null;
			}

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
			_logger.FatalException("UNOBSERVED TASK EXCEPTION:", e.Exception);
			throw e.Exception;
		}

		void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			_logger.FatalException("UNHANDLED APPDOMAIN EXCEPTION:", (Exception)e.ExceptionObject);
			throw (Exception)e.ExceptionObject;
		}

		void OnServiceHostUnknownMessageReceived(string id, UnknownMessageReceivedEventArgs e)
		{
			var writer = new StringWriter();
			var dict = XmlDictionaryWriter.CreateDictionaryWriter(XmlWriter.Create(writer));
			e.Message.WriteMessage(dict);
			_logger.Warn(string.Concat("[", id, "] ", "UNKNOWN MESSAGE RECEIVED: {0}"), writer.ToString());
		}

		void OnServiceHostOpened(string id)
		{
			_logger.Info(string.Concat("[", id, "] ", "Service host opened."));
		}

		void OnServiceHostOpening(string id)
		{
			_logger.Info(string.Concat("[", id, "] ", "Service host opening... "));
		}

		void OnServiceHostFaulted(string id)
		{
			_logger.Info(string.Concat("[", id, "] ", "Service host faulted!"));
			throw new Exception("Service Host Faulted!");
		}

		void OnServiceHostClosing(string id)
		{
			_logger.Info(string.Concat("[", id, "] ", "Service host closing..."));
		}

		void OnServiceHostClosed(string id)
		{
			_logger.Info(string.Concat("[", id, "] ", "Service host closed."));
		}

		public void Dispose()
		{
			lock(this)
				if(_runningExtension != null)
				{
					if(_runningExtension.Extension is IDisposable)
						((IDisposable)_runningExtension.Extension).Dispose();
					_runningExtension = null;
				}
		}
	}
}