using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text.RegularExpressions;
using ServerX.Common;
using NLog;

namespace ServerX
{
	[ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.Single, UseSynchronizationContext = true, IncludeExceptionDetailInFaults = true)]
	public class ServiceManager : ServiceXBase, IServiceManager
	{
		private DirectoryInfo _extensionsBaseDir;
		private readonly string[] _extensionFileExtensions = new[] { "dll", "exe" };
		private ExtensionProcessManager _extProcMgr;
		private ExtensionsConfigFileManager _extCfgMgr;
		private ExtensionClientManager _extClientMgr;
		private CommandRunner _cmdRunner;
		private CronManager _cronMgr;
		private Logger _logger;

		public ServiceManager()
		{
			ServiceManagerNotificationTarget.ServiceManager = this;
			_logger = LogManager.GetCurrentClassLogger();

			ExtensionNotificationReceived += (extID, extName, level, source, message) => CallbackEachClient<IServiceManagerCallback>(c => c.ServerExtensionNotify(extID, extName, level, source, message));
			ServiceManagerNotificationReceived += (level, source, message) => CallbackEachClient<IServiceManagerCallback>(c => c.Notify(level, source, message));

			_extensionsBaseDir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions"));
			if(!_extensionsBaseDir.Exists)
				_extensionsBaseDir.Create();
			if(_extensionsBaseDir.GetFiles().Select(f => f.Extension.ToLower()).Any(ext => _extensionFileExtensions.Contains(ext)))
				throw new Exception("The extensions directory currently contains assemblies and/or executables. Extensions should be located in subdirectories of the Extensions folder; not the Extensions directory itself.");

			_extProcMgr = new ExtensionProcessManager(
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServerX.Run.exe"),
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions")
			);

			_extClientMgr = new ExtensionClientManager(_extProcMgr);
			_extClientMgr.ExtensionNotificationReceived += (extID, extName, level, source, message) =>
			{
				var handler = ExtensionNotificationReceived;
				if(handler != null)
					handler(extID, extName, level, source, message);
			};

			_extProcMgr.StartMonitoring();

			_extCfgMgr = new ExtensionsConfigFileManager(_extProcMgr);

			_cmdRunner = new CommandRunner(this, _extClientMgr);
			_cronMgr = new CronManager(this);
		}

		private void Notify(LogLevel level, string src, string msg)
		{
			var handler = ServiceManagerNotificationReceived;
			if(handler != null)
				handler(level.Name, src, msg);
		}

		public event ServiceManagerCallback.ExtensionNotificationHandler ExtensionNotificationReceived;
		public event ServiceManagerCallback.NotificationHandler ServiceManagerNotificationReceived;

		internal string JsonCall(string name, string[] jsonArgs)
		{
			return JavaScriptInterface.JsonCall(this, name, jsonArgs, JavaScriptInterface.ExcludedServiceManagerJsMethodNames);
		}

		internal void CreateNotification(string level, string source, string message)
		{
			CallbackEachClient<IServiceManagerCallback>(c => c.Notify(level, source, message));
		}

		public DateTime GetServerTime()
		{
			return DateTime.Now;
		}

		//public Result SetExtensionDirectoryIncluded(string name, bool include)
		//{
		//    throw new NotImplementedException();
		//}

		//public Result SetExtensionsEnabledInDirectory(string name, bool enabled)
		//{
		//    throw new NotImplementedException();
		//}

		//public Result SetExtensionEnabled(string name, bool enabled)
		//{
		//    throw new NotImplementedException();
		//}

		public Result RestartExtensions(string subdirName, params string[] extensionIDs)
		{
			return _extProcMgr.RestartExtensions(subdirName, extensionIDs);
		}

		public string[] ListExtensionDirectories()
		{
			return _extensionsBaseDir.GetDirectories().Select(d => d.Name).ToArray();
		}

		//public string[] ListIncludedExtensionDirectories()
		//{
		//    throw new NotImplementedException();
		//}

		//public ExtensionInfo[] ListAvailableExtensions()
		//{
		//    throw new NotImplementedException();
		//}

		public ExtensionInfo[] ListExtensionsInDirectory(string name)
		{
			using(var pl = new SafeExtensionLoader(_extensionsBaseDir.FullName, name, "", null))
				return pl.AvailableExtensions;
		}

		public string ExecuteCommand(string command, string[] args)
		{
			return _cmdRunner.Execute(command, args);
		}

		public CommandInfo[] ListExtensionCommands()
		{
			return _extClientMgr.ListConnectedExtensions()
				.Where(e => e.Commands != null && e.Commands.Length > 0)
				.SelectMany(e => e.Commands)
				.ToArray();
		}

		public CommandInfo[] ListServiceManagerCommands()
		{
			return _cmdRunner.ListCommands();
		}

		public CommandInfo GetCommandInfo(string cmdAlias)
		{
			return _cmdRunner.GetCommandInfo(cmdAlias);
		}

		public ScriptInfo[] ListScripts()
		{
			throw new NotImplementedException();
		}

		public Result ExecuteScriptFile(string filename)
		{
			var scriptRunner = new ScriptRunner(this, _extClientMgr);
			var result = scriptRunner.ExecuteJavaScriptFile(filename);
			return result;
		}

		public ScriptInfo GetScript(string name)
		{
			throw new NotImplementedException();
		}

		public string SaveScript(ScriptInfo script)
		{
			throw new NotImplementedException();
		}

		public string DeleteScript(string name)
		{
			throw new NotImplementedException();
		}

		public ScheduledCommand[] ListScheduledCommands()
		{
			throw new NotImplementedException();
		}

		public ScheduledCommand AddScheduledCommand(string cron, string command)
		{
			throw new NotImplementedException();
		}

		public Result DeleteScheduledCommand(int id)
		{
			throw new NotImplementedException();
		}

		public void KeepExtensionProcessAlive(Guid id)
		{
			_extProcMgr.KeepAlive(id);
		}

		public void NotifyExtensionServiceReady(Guid extProcID, string address)
		{
			if(_extProcMgr.IsValidID(extProcID))
			{
				var info = _extClientMgr.TryConnect(extProcID, address);
				if(info != null)
					Notify(LogLevel.Info, info.Name, "Extension connected and ready.");
			}
		}

		public virtual void Dispose()
		{
			_extProcMgr.Dispose();
			_extProcMgr = null;
			_extCfgMgr.Dispose();
			_extCfgMgr = null;
			_cronMgr.Dispose();
			_cronMgr = null;
		}
	}
}
