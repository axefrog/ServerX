using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ServerX.Common;

namespace ServerX
{
	public class ServiceManager : IServiceManager
	{
		private DirectoryInfo _extensionsBaseDir;
		private readonly string[] _extensionFileExtensions = new[] { "dll", "exe" };
		private ExtensionProcessManager _extProcMgr;
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

			_extClientMgr.ExtensionNotificationReceived += (extID, extName, message) =>
			{
				var handler = ExtensionNotificationReceived;
				if(handler != null)
					handler(extID, extName, message);
			};

			_extProcMgr = new ExtensionProcessManager(
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServerX.Run.exe"),
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions")
			);

			_cmdRunner = new CommandRunner(this, _extClientMgr);
			_cronMgr = new CronManager(this);

			StartExtensions();
		}

		public event ServiceManagerCallback.ExtensionNotificationHandler ExtensionNotificationReceived;

		private void StartExtensions()
		{
			var cfgPath = Path.Combine(ConfigurationManager.AppSettings["DataDirectory"] ?? Environment.CurrentDirectory, "Config", "extensions.txt");
			if(!File.Exists(cfgPath))
				throw new Exception("Unable to start extensions. There must be a Config subdirectory containing a file extensions.txt. The file must contain zero or more lines, each starting with the name of a subdirectory inside the Extensions subdirectory, optionally followed by a tab and then a comma-delimited list of server extension IDs to load");
			foreach(var line in File.ReadLines(cfgPath))
			{
				var s = Regex.Replace((line ?? "").Trim(), @"\t+", "\t");
				if(string.IsNullOrWhiteSpace(s) || s.StartsWith("#"))
					continue;
				var arr = s.Split('\t');
				if(arr.Length > 2)
					continue;
				var dir = arr[0].Trim();
				string[] ids = null;
				if(arr.Length == 2)
					ids = arr[1].Split(new [] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()).ToArray();
				_extProcMgr.Execute(dir, ids ?? new string[0]);
			}
		}

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
			using(var pl = new SafeExtensionLoader(_extensionsBaseDir.FullName, name, false))
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
			_cronMgr.Dispose();
			_cronMgr = null;
		}
	}
}
