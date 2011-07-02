using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ServerX.Common;

namespace ServerX
{
	public class ExtensionProcessManager : IDisposable
	{
		private readonly Logger _logger = new Logger("ext-proc-mgr");
		public ExtensionProcessManager(string launcherExePath, string extensionsBasePath, bool killOrphanedProcesses = true, TimeSpan monitorInterval = default(TimeSpan))
		{
			if(monitorInterval.Ticks <= 0)
				monitorInterval = new TimeSpan(0, 0, 30);

			_launcherExePath = launcherExePath;
			_extensionsBasePath = extensionsBasePath;
			_monitorInterval = monitorInterval;

			if(killOrphanedProcesses)
				foreach(var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(launcherExePath)))
					if(process.StartInfo.WorkingDirectory.StartsWith(_extensionsBasePath)) // only kill extension processes that are running from this base location
						process.Kill();

			StartMonitoring();
		}

		private List<KeyValuePair<DateTime, Exception>> _previousExceptions = new List<KeyValuePair<DateTime, Exception>>();
		private void StartMonitoring()
		{
			_cancelSrc = new CancellationTokenSource();
			Task.Factory.StartNew(() => MonitorProcesses(_cancelSrc.Token, _monitorInterval, _processes, _logger))
				.ContinueWith(t =>
				{
					_logger.WriteLine("Process Monitor threw an exception" + Environment.NewLine + t.Exception);
					if(_previousExceptions.Count > 0)
					{
						// remove exceptions that occurred more than 5 minutes ago
						_previousExceptions = _previousExceptions.Where(p => p.Key > DateTime.Now.AddMinutes(-5)).ToList();

						// if this exception has been thrown at least 5 times in the past 5 minutes, we should probably stop the monitoring process
						if(_previousExceptions.Count >= 5 && _previousExceptions.All(p => p.Value.GetType() == t.Exception.GetType()))
						{
							_logger.WriteLine("!!! TOO MANY EXCEPTIONS - TERMINATING MONITORING THREAD !!!");
							return;
						}
						_previousExceptions.Add(new KeyValuePair<DateTime, Exception>(DateTime.Now, t.Exception));
					}
					StartMonitoring();
				}, TaskContinuationOptions.OnlyOnFaulted);
		}

		private readonly string _launcherExePath;
		private readonly string _extensionsBasePath;
		private readonly TimeSpan _monitorInterval;
		private readonly ConcurrentDictionary<Guid, ProcessInfo> _processes = new ConcurrentDictionary<Guid, ProcessInfo>();
		private CancellationTokenSource _cancelSrc;
		const int ExtensionStartupSeconds = 5;

		class ProcessInfo
		{
			public Guid ID { get; set; }
			public Process Process { get; set; }
			public string[] ExtensionIDs { get; set; }
			public string DirectoryName { get; set; }
			public DateTime Timeout { get; set; }
		}

		private ProcessInfo StartProcess(string dirName, params string[] extensionIDs)
		{
			var info = new ProcessInfo
			{
				ID = Guid.NewGuid(),
				DirectoryName = dirName,
				ExtensionIDs = extensionIDs,
				Timeout = DateTime.UtcNow.Add(_monitorInterval).AddSeconds(ExtensionStartupSeconds) // we add 5 seconds to give the process time to start
			};
			_logger.WriteLine("Starting new process for extensions in /" + dirName + " - " + info.ID + " (" + (extensionIDs.Length == 0 ? "all" : extensionIDs.Concat(", ")) + ")");
			var cmdargs = string.Format(
				"-subdir \"{0}\" -basedir \"{1}\" -pid:{2} -guid \"{3}\"{4}",
				dirName, _extensionsBasePath, Process.GetCurrentProcess().Id, info.ID, extensionIDs.Concat(s => " " + s)
			);
			var psi = new ProcessStartInfo(_launcherExePath, cmdargs)
			{
				ErrorDialog = false,
				CreateNoWindow = true,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				WorkingDirectory = Path.Combine(_extensionsBasePath, dirName),
				UseShellExecute = false
			};
			info.Process = Process.Start(psi);
			return info;
		}

		private static void MonitorProcesses(CancellationToken token, TimeSpan interval, ConcurrentDictionary<Guid, ProcessInfo> processes, Logger logger)
		{
			DateTime nextCheck = DateTime.Now.Add(interval);
			while(!token.IsCancellationRequested)
			{
				if(nextCheck > DateTime.Now)
				{
					Thread.Sleep(100);
					continue;
				}
				foreach(var key in processes.Keys.ToArray())
				{
					ProcessInfo p;
					if(processes.TryGetValue(key, out p))
					{
						lock(p)
							if(p.Process.HasExited)
							{
								var exitCode = p.Process.ExitCode;
								p.Process.Start();
								logger.WriteLine("Process " + p.ID + " (" + p.DirectoryName + ") exited with code " + exitCode + " and so has been restarted");
							}
							else if(p.Timeout < DateTime.Now)
							{
								logger.WriteLine("Process " + p.ID + " (" + p.DirectoryName + ") did not call back inside the timeout period and will be restarted... ");
								p.Process.Kill();
								p.Process.WaitForExit();
								p.Process.Start();
								logger.WriteLine("Process " + p.ID + " (" + p.DirectoryName + ") restarted successfully");
							}
					}
				}
				nextCheck = DateTime.Now.Add(interval);
			}
		}

		public void Execute(string dirName, params string[] extensionIDs)
		{
			var pi = StartProcess(dirName, extensionIDs);
			_processes.AddOrUpdate(pi.ID, pi, (k,p) =>
			{
				lock(p)
					if(!p.Process.HasExited)
					{
						p.Process.Kill();
						p.Process.Dispose();
					}
				return pi;
			});
		}

		internal void KeepAlive(Guid id)
		{
			ProcessInfo pi;
			if(!_processes.TryGetValue(id, out pi))
			{
				// if we've been signalled by a process we don't have a reference to, find it and kill it
				var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(_launcherExePath));
				var idstr = id.ToString();
				foreach(var process in processes)
					if(process.StartInfo.Arguments.Contains(idstr))
						process.Kill();
				return;
			}
			lock(pi)
				pi.Timeout = DateTime.Now.Add(_monitorInterval);
		}

		public void Dispose()
		{
			if(_cancelSrc != null)
				_cancelSrc.Cancel();

			foreach(var p in _processes.Values)
				lock(p)
					if(!p.Process.HasExited)
						try
						{
							p.Process.Kill();
							p.Process.Dispose();
						}
						catch
						{
						
						}
			_processes.Clear();
		}
	}
}
