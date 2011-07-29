﻿using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text.RegularExpressions;
using ServerX.Common;

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

		public ServiceManager()
		{
			ExtensionNotificationReceived += (extID, extName, source, message, level) => CallbackEachClient<IServiceManagerCallback>(c => c.ServerExtensionNotify(extID, extName, source, message, level));
			ServiceManagerNotificationReceived += (source, message, level) => CallbackEachClient<IServiceManagerCallback>(c => c.Notify(source, message, level));

			_extensionsBaseDir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions"));
			if(!_extensionsBaseDir.Exists)
				_extensionsBaseDir.Create();
			if(_extensionsBaseDir.GetFiles().Select(f => f.Extension.ToLower()).Any(ext => _extensionFileExtensions.Contains(ext)))
				throw new Exception("The extensions directory currently contains assemblies and/or executables. Extensions should be located in subdirectories of the Extensions folder; not the Extensions directory itself.");

			var extProcLog = new Logger("ext-proc-mgr");
			_extProcMgr = new ExtensionProcessManager(
				extProcLog,
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServerX.Run.exe"),
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions")
			);

			_extClientMgr = new ExtensionClientManager(_extProcMgr, extProcLog);
			_extClientMgr.ExtensionNotificationReceived += (extID, extName, source, message, level) =>
			{
				var handler = ExtensionNotificationReceived;
				if(handler != null)
					handler(extID, extName, source, message, level);
			};

			extProcLog.MessageLogged += (src, msg, lvl) => Notify("Extension Manager", msg, lvl);
			_extProcMgr.StartMonitoring();

			_extCfgMgr = new ExtensionsConfigFileManager(extProcLog, _extProcMgr);

			_cmdRunner = new CommandRunner(this, _extClientMgr);
			var cronLog = new Logger("cron");
			cronLog.MessageLogged += (src, msg, lvl) => Notify("Cron Manager", msg, lvl);
			_cronMgr = new CronManager(this, cronLog);
		}

		private void Notify(string src, string msg, LogLevel level)
		{
			var handler = ServiceManagerNotificationReceived;
			if(handler != null)
				handler(src, msg, level);
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

		public void NotifyExtensionServiceReady(Guid extProcID, string address)
		{
			if(_extProcMgr.IsValidID(extProcID))
			{
				var info = _extClientMgr.TryConnect(extProcID, address);
				if(info != null)
					Notify(info.Name, "Extension connected and ready.", LogLevel.Normal);
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
