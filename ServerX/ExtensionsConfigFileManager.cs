using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using ServerX.Common;

namespace ServerX
{
	class ExtensionsConfigFileManager
	{
		private readonly ExtensionProcessManager _extProcMgr;
		FileInfo _file = new FileInfo(Path.Combine(ConfigurationManager.AppSettings["DataDirectory"] ?? Environment.CurrentDirectory, "Config", "extensions.txt"));
		FileSystemWatcher _fsw;
		Logger _logger = LogManager.GetCurrentClassLogger();

		public ExtensionsConfigFileManager(ExtensionProcessManager extProcMgr)
		{
			_extProcMgr = extProcMgr;
		}

		public void Init()
		{
			_file.Refresh();
			if(!_file.Exists)
			{
				if(!_file.Directory.Exists)
					_file.Directory.Create();
				File.WriteAllText(_file.FullName, "");
				_file.Refresh();
			}

			UpdateRunningExtensions();

			if(_fsw != null)
				_fsw.Dispose();
			_fsw = new FileSystemWatcher(_file.DirectoryName, _file.Name);
			_fsw.Changed += OnConfigFileChanged;
			_fsw.Deleted += OnConfigFileChanged;
			_fsw.Renamed += OnConfigFileChanged;
			_fsw.EnableRaisingEvents = true;
		}

		void OnConfigFileChanged(object sender, FileSystemEventArgs e)
		{
			_logger.Trace("[FILECHANGE] " + e.ChangeType);
			Init();
		}

		private void UpdateRunningExtensions()
		{
			_logger.Info("Parsing extensions.txt file...");

			IEnumerable<string> lines;
			try
			{
				lines = _file.Exists ? File.ReadLines(_file.FullName) : new string[0];
			}
			catch(IOException ex)
			{
				_logger.WarnException("Unable to read extensions file", ex);
				return;
			}

			var bag = new List<KeyValuePair<string, Guid>>(_extProcMgr.GetExtensionProcessList()
				.Select(p => new KeyValuePair<string, Guid>(string.Concat(p.DirectoryName, '\t', p.RequestedExtensionIDs.Concat(",")).Trim(), p.ID)));

			foreach(var line in lines)
			{
				var linestr = Regex.Replace((line ?? "").Trim(), @"\t+", "\t");
				if(string.IsNullOrWhiteSpace(linestr) || linestr.StartsWith("#"))
					continue;
				var arr = linestr.Split('\t');
				if(arr.Length > 2)
					continue;
				var dir = arr[0].Trim();
				string[] ids = null;
				if(arr.Length == 2)
					ids = arr[1].Split(new [] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()).ToArray();

				// linestr becomes the key identifying the extension process. there may be multiple processes with the same linestr.
				linestr = string.Concat(dir, '\t', ids.Concat(",")).Trim();
				// find out if we already have an extension process matching linestr and if so, remove it from the bag so it stays running.
				var item = bag.FirstOrDefault(b => b.Key == linestr);
				if(item.Key != null)
				{
					_logger.Info("Extension config line matches pre-existing process: " + linestr);
					bag.Remove(item);
				}
				else
				{
					// seeing as there were no entries in the bag matching the current linestr, we've identified a new process that needs to be started up
					_logger.Info("New extension config line found -> starting new extension process: " + linestr);
					_extProcMgr.Execute(dir, ids ?? new string[0]);
				}
			}

			// anything left in the bag is no longer in the config file and needs to be shut down
			foreach(var item in bag)
			{
				_logger.Info("Existing extension process is no longer in the config file and will be shut down: " + item.Key);
				_extProcMgr.Stop(item.Value);
			}

			_logger.Info("Extensions list successfully updated from config file.");
		}

		public void Dispose()
		{
			if(_fsw != null)
			{
				_fsw.Dispose();
				_fsw = null;
			}
		}
	}
}
