using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ServerX.Common;

namespace ServerX
{
	public class ServiceManager : IServiceManager
	{
		private DirectoryInfo _extensionsBaseDir;
		private readonly string[] _extensionFileExtensions = new[] { "dll", "exe" };
		private ExtensionProcessManager _extProcMgr;
		private ExtensionsConfigFileManager _extCfgMgr;
		private ServerExtensionClientManager _extClientMgr = new ServerExtensionClientManager();
		private CommandRunner _cmdRunner;
		private CronManager _cronMgr;

		public ServiceManager()
		{
			_extensionsBaseDir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions"));
			if(!_extensionsBaseDir.Exists)
				_extensionsBaseDir.Create();
			if(_extensionsBaseDir.GetFiles().Select(f => f.Extension.ToLower()).Any(ext => _extensionFileExtensions.Contains(ext)))
				throw new Exception("The extensions directory currently contains assemblies and/or executables. Extensions should be located in subdirectories of the Extensions folder; not the Extensions directory itself.");

			_extClientMgr.ExtensionNotificationReceived += (extID, extName, source, message) =>
			{
				
				var handler = ExtensionNotificationReceived;
				if(handler != null)
					handler(extID, extName, source, message);
			};

			var extProcLog = new Logger("ext-proc-mgr");
			extProcLog.MessageLogged += msg => Notify("Extension Manager", msg);

			_extProcMgr = new ExtensionProcessManager(
				extProcLog,
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServerX.Run.exe"),
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions")
			);
			_extCfgMgr = new ExtensionsConfigFileManager(extProcLog, _extProcMgr);

			_cmdRunner = new CommandRunner(this, _extClientMgr);
			var cronLog = new Logger("cron");
			cronLog.MessageLogged += msg => Notify("Cron Manager", msg);
			_cronMgr = new CronManager(this, cronLog);
		}

		private void Notify(string src, string msg)
		{
			var handler = ServiceManagerNotificationReceived;
			if(handler != null)
				handler(src, msg);
		}

		public event ServiceManagerCallback.ExtensionNotificationHandler ExtensionNotificationReceived;
		public event ServiceManagerCallback.NotificationHandler ServiceManagerNotificationReceived;

		internal string JsonCall(string name, string[] jsonArgs)
		{
			return JavaScriptInterface.JsonCall(this, name, jsonArgs, JavaScriptInterface.ExcludedServiceManagerJsMethodNames);
		}

		public DateTime GetServerTime()
		{
			return DateTime.Now;
		}

		public Result SetExtensionDirectoryIncluded(string name, bool include)
		{
			throw new NotImplementedException();
		}

		public Result SetExtensionsEnabledInDirectory(string name, bool enabled)
		{
			throw new NotImplementedException();
		}

		public Result SetExtensionEnabled(string name, bool enabled)
		{
			throw new NotImplementedException();
		}

		public Result RestartExtensions(string subdirName, params string[] extensionIDs)
		{
			return _extProcMgr.RestartExtensions(subdirName, extensionIDs);
		}

		public string[] ListExtensionDirectories()
		{
			return _extensionsBaseDir.GetDirectories().Select(d => d.Name).ToArray();
		}

		public string[] ListIncludedExtensionDirectories()
		{
			throw new NotImplementedException();
		}

		public ExtensionInfo[] ListAvailableExtensions()
		{
			throw new NotImplementedException();
		}

		public ExtensionInfo[] ListExtensionsInDirectory(string name)
		{
			using(var pl = new SafeExtensionLoader(_extensionsBaseDir.FullName, name, false, null))
				return pl.AllExtensions;
		}

		public string ExecuteCommand(string command, string[] args)
		{
			return _cmdRunner.Execute(command, args);
		}

		public ExtensionInfo[] ListExtensionCommands()
		{
			return _extClientMgr.ListConnectedExtensions().Where(e => e.SupportsCommandLine).ToArray();
		}

		public Command[] ListServiceManagerCommands()
		{
			return _cmdRunner.ListCommands();
		}

		public string GetCommandHelp(string command)
		{
			throw new NotImplementedException();
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

		public void NotifyExtensionServiceReady(string address)
		{
			_extClientMgr.TryConnect(address);
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
