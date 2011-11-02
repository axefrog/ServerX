using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NCrontab;
using NLog;
using ServerX.Common;

namespace ServerX
{
	public class CronManager : IDisposable
	{
		private readonly ServiceManager _svc;
		FileInfo _file = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "cron.txt"));
		FileSystemWatcher _fsw;
		CancellationTokenSource _cancelSource;
		PersistenceManager<CronManagerSettings> _settings = new PersistenceManager<CronManagerSettings>("cron-settings");
		Logger _logger = LogManager.GetCurrentClassLogger();

		public CronManager(ServiceManager svc)
		{
			_svc = svc;
			Init();
		}

		void Init()
		{
			if(_cancelSource != null)
				_cancelSource.Cancel();

			_file.Refresh();
			if(!_file.Exists)
			{
				if(!_file.Directory.Exists)
					_file.Directory.Create();
				File.WriteAllText(_file.FullName, "");
				_file.Refresh();
			}

			ReadAndParseFile();
			if(_fsw != null)
				_fsw.Dispose();
			_fsw = new FileSystemWatcher(_file.DirectoryName, "cron.txt");
			_fsw.Changed += OnCronFileChanged;
			_fsw.Deleted += OnCronFileChanged;
			_fsw.Renamed += OnCronFileChanged;
			_fsw.EnableRaisingEvents = true;

			_cancelSource = new CancellationTokenSource();
			Task.Factory.StartNew(Run, _cancelSource.Token);
		}

		List<CronJob> _cronList;
		void ReadAndParseFile()
		{
			_logger.Info("Parsing cron.txt file...");
			_cronList = new List<CronJob>();

			foreach(var line in File.ReadAllLines(_file.FullName))
			{
				if(string.IsNullOrWhiteSpace(line))
					continue;

				var str = line.Trim();
				if(str.StartsWith("#"))
					continue;

				var arr = str.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
				if(arr.Length < 2)
					continue;

				var cronstr = arr[0].Trim();
				var cmdstr = arr[1].Trim();
				var cmdarr = Regex.Replace(cmdstr, @"\s+", " ").Split(' ');
				var cmd = cmdarr.First();
				var args = cmdarr.Skip(1).ToArray();

				CrontabSchedule schedule;
				try
				{
					schedule = CrontabSchedule.Parse(arr[0].Trim());
					if(schedule == null)
						continue;
				}
				catch
				{
					continue;
				}

				_cronList.Add(new CronJob(schedule, cronstr, cmd, args));
			}
			_logger.Info("Found {0} cron job(s) - {1}", _cronList.Count, DateTime.Now);
			_logger.Info("Cron job list successfully updated from config file.");
		}

		void OnCronFileChanged(object sender, FileSystemEventArgs e)
		{
			_logger.Trace("[FILECHANGE] " + e.ChangeType + Environment.NewLine);
			Init();
		}

		void Run()
		{
			if(_cronList.Count == 0)
				return;
			var jobs = _cronList;
			var token = _cancelSource.Token;
			try
			{
				while(!token.IsCancellationRequested)
				{
					_settings.Lock();
					try
					{
						foreach(var nextJob in jobs)
						{
							if(nextJob.NextRun <= DateTime.Now)
							{
								_logger.Info("Executing: " + nextJob.CronString + " => " + nextJob.Command + " " + nextJob.Args.Concat(" "));
								string response;
								try
								{
									token.ThrowIfCancellationRequested();
									response = _svc.ExecuteCommand(nextJob.Command, nextJob.Args);
									_logger.Info("Execution finished: " + (response ?? "(null response)"));
								}
								catch(Exception ex)
								{
									_logger.WarnException("Exception thrown while executing command via cron", ex);
								}
								nextJob.Recalculate();
							}
						}
						_settings.Values.LastRun = DateTime.Now;
						_settings.Save();
						jobs.Sort();
					}
					finally
					{
						_settings.Unlock();
					}
					Thread.Sleep(15000);
				}
			}
			catch(OperationCanceledException)
			{
				return;
			}
		}

		public void Dispose()
		{
			if(_cancelSource != null)
			{
				_cancelSource.Cancel();
				_cancelSource = null;
			}
			if(_fsw != null)
			{
				_fsw.Dispose();
				_fsw = null;
			}
			_settings.Dispose();
			_settings = null;
		}

		private class CronManagerSettings
		{
			public DateTime LastRun { get; set; }
		}

		private class CronJob : IComparable
		{
			public CronJob(CrontabSchedule schedule, string cronString, string command, string[] args)
			{
				Schedule = schedule;
				CronString = cronString;
				Command = command;
				Args = args;
			}

			public CrontabSchedule Schedule { get; private set; }
			private DateTime? _nextRun;
			public DateTime NextRun
			{
				get
				{
					if(!_nextRun.HasValue)
						_nextRun = Schedule.GetNextOccurrence(DateTime.Now);
					return _nextRun.Value;
				}
			}

			public string CronString { get; private set; }
			public string Command { get; private set; }
			public string[] Args { get; private set; }

			public int CompareTo(object obj)
			{
				if(obj == null || !(obj is CronJob))
					return 1;
				var cron = (CronJob)obj;
				return NextRun.CompareTo(cron.NextRun);
			}

			public void Recalculate()
			{
				_nextRun = null;
			}
		}
	}
}
