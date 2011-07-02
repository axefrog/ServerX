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
		private ExtensionProcessManager _extMgr;

		public ServiceManager()
		{
			_extensionsBaseDir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions"));
			if(!_extensionsBaseDir.Exists)
				_extensionsBaseDir.Create();
			if(_extensionsBaseDir.GetFiles().Select(f => f.Extension.ToLower()).Any(ext => _extensionFileExtensions.Contains(ext)))
				throw new Exception("The extensions directory currently contains assemblies and/or executables. Extensions should be located in subdirectories of the Extensions folder; not the Extensions directory itself.");

			_extMgr = new ExtensionProcessManager(
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServerX.Run.exe"),
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions")
			);

			StartExtensions();
			//var commandRunner = new CommandRunner(this);
		}

		private void StartExtensions()
		{
			foreach(var line in File.ReadLines(Path.Combine(ConfigurationManager.AppSettings["DataDirectory"] ?? Environment.CurrentDirectory, "Config", "extensions.txt")))
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
				_extMgr.Execute(dir, ids ?? new string[0]);
			}
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

		public Result RestartExtension(string name)
		{
			throw new NotImplementedException();
		}

		public Result RestartExtensionsInDirectory(string name)
		{
			throw new NotImplementedException();
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
			using(var pl = new SafeExtensionLoader(_extensionsBaseDir.FullName, name))
				return pl.AllExtensions;
		}

		public string ExecuteCommand(string command)
		{
			throw new NotImplementedException();
		}

		public CommandInfo ListCommands()
		{
			throw new NotImplementedException();
		}

		public string GetCommandHelp(string command)
		{
			throw new NotImplementedException();
		}

		public ScriptInfo[] ListScripts()
		{
			throw new NotImplementedException();
		}

		public string ExecuteScript(string name)
		{
			throw new NotImplementedException();
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
			_extMgr.KeepAlive(id);
		}

		public virtual void Dispose()
		{
			_extMgr.Dispose();
			_extMgr = null;
		}
	}
}
