using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using ServerX.Common;

namespace ServerX
{
	public class ExtensionProcessManager : IDisposable
	{
		private readonly Logger _logger;
		public ExtensionProcessManager(string launcherExePath, string extensionsBasePath, bool killOrphanedProcesses = true, TimeSpan monitorInterval = default(TimeSpan))
		{
			if(monitorInterval.Ticks <= 0)
				monitorInterval = new TimeSpan(0, 0, 30);

			_logger = LogManager.GetCurrentClassLogger();
			_launcherExePath = launcherExePath;
			_extensionsBasePath = extensionsBasePath;
			_monitorInterval = monitorInterval;

			if(killOrphanedProcesses)
				foreach(var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(launcherExePath)))
					if(process.StartInfo.WorkingDirectory.StartsWith(_extensionsBasePath)) // only kill extension processes that are running from this base location
						process.Kill();
		}

		private List<KeyValuePair<DateTime, Exception>> _previousExceptions = new List<KeyValuePair<DateTime, Exception>>();
		internal void StartMonitoring()
		{
			_cancelSrc = new CancellationTokenSource();
			Task.Factory.StartNew(() => MonitorProcesses(_cancelSrc.Token, _monitorInterval, _processes))
				.ContinueWith(t =>
				{
					_logger.ErrorException("Process Monitor threw an exception", t.Exception);
					if(_previousExceptions.Count > 0)
					{
						// remove exceptions that occurred more than 5 minutes ago
						_previousExceptions = _previousExceptions.Where(p => p.Key > DateTime.Now.AddMinutes(-5)).ToList();

						// if this exception has been thrown at least 5 times in the past 5 minutes, we should probably stop the monitoring process
						if(_previousExceptions.Count >= 5 && _previousExceptions.All(p => p.Value.GetType() == t.Exception.GetType()))
						{
							_logger.Fatal("!!! TOO MANY EXCEPTIONS - TERMINATING MONITORING THREAD !!!");
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

		public class ExtensionInfo
		{
			public Guid ID { get; set; }
			public string[] ActiveExtensionIDs { get; set; }
			public string[] RequestedExtensionIDs { get; set; }
			public string DirectoryName { get; set; }
		}

		class ProcessInfo : ExtensionInfo
		{
			public Process Process { get; set; }
			public DateTime Timeout { get; set; }
			public bool RequestRestart { get; set; }
			public bool RequestShutdown { get; set; }
		}

		private ProcessInfo StartProcess(string dirName, params string[] requestedExtensionIDs)
		{
			string[] foundExtensionIDs, activeExtensionIDs;
			try
			{
				using(var loader = new SafeExtensionLoader(_extensionsBasePath, dirName, "", null))
					foundExtensionIDs = loader.AvailableExtensions.Select(e => e.ExtensionID).ToArray();
			}
			catch(FileNotFoundException)
			{
				_logger.Error("Unable to start extension process - the extension subdirectory \"" + dirName + "\" does not exist or does not contain a valid set of extension assemblies.");
				return null;
			}
			if(requestedExtensionIDs == null || requestedExtensionIDs.Length == 0)
				activeExtensionIDs = foundExtensionIDs;
			else
				activeExtensionIDs = requestedExtensionIDs.Where(e => foundExtensionIDs.Contains(e)).ToArray();
			var info = new ProcessInfo
			{
				ID = Guid.NewGuid(),
				DirectoryName = dirName,
				RequestedExtensionIDs = requestedExtensionIDs,
				ActiveExtensionIDs = activeExtensionIDs,
				Timeout = DateTime.UtcNow.Add(_monitorInterval).AddSeconds(ExtensionStartupSeconds) // we add 5 seconds to give the process time to start
			};
			_logger.Info("Starting new process for extensions in /" + dirName + " - " + info.ID + " (" + (activeExtensionIDs.Length == 0 ? "all" : activeExtensionIDs.Concat(", ")) + ")");
			var cmdargs = string.Format(
				"-subdir \"{0}\" -basedir \"{1}\" -pid={2} -guid \"{3}\"{4}",
				dirName, _extensionsBasePath, Process.GetCurrentProcess().Id, info.ID, activeExtensionIDs.Concat(s => " " + s)
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

		private static void MonitorProcesses(CancellationToken token, TimeSpan interval, ConcurrentDictionary<Guid, ProcessInfo> processes)
		{
			var logger = LogManager.GetCurrentClassLogger();
			DateTime nextCheck = DateTime.Now.Add(interval);
			while(!token.IsCancellationRequested)
			{
				try
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
							{
								if(p.Process.HasExited)
								{
									var exitCode = p.Process.ExitCode;
									p.Process.Start();
									logger.Info("Process " + p.ID + " (" + p.DirectoryName + ") exited with code " + exitCode + " and so has been restarted");
								}
								else if(p.Timeout < DateTime.Now)
								{
									logger.Warn("Process " + p.ID + " (" + p.DirectoryName + ") did not call back inside the timeout period and will be restarted... ");
									p.Process.Kill();
									p.Process.WaitForExit();
									p.Process.Start();
									logger.Info("Process " + p.ID + " (" + p.DirectoryName + ") restarted successfully");
								}
								else if(p.RequestRestart)
								{
									p.RequestRestart = false;
									logger.Info("Restart requested for process " + p.ID + " (" + p.DirectoryName + ")");
									p.Process.Kill();
									p.Process.WaitForExit();
									p.Process.Start();
									logger.Info("Process " + p.ID + " (" + p.DirectoryName + ") restarted successfully");
								}
								else if(p.RequestShutdown)
								{
									p.RequestRestart = false;
									logger.Info("Shutdown requested for process " + p.ID + " (" + p.DirectoryName + ")");
									p.Process.Kill();
									p.Process.WaitForExit();
									ProcessInfo pi;
									processes.TryRemove(p.ID, out pi);
									logger.Info("Process " + p.ID + " (" + p.DirectoryName + ") shut down and removed successfully");
								}
							}
						}
					}
				}
				catch(TaskCanceledException)
				{
					throw;
				}
				catch(Exception ex)
				{
					logger.ErrorException("MonitorProcesses() (crash prevented - waiting 5 seconds before resuming)", ex);
					Thread.Sleep(5000);
				}
				nextCheck = DateTime.Now.Add(interval);
			}
		}

		public List<ExtensionInfo> GetExtensionProcessList()
		{
			return _processes.Values.Select(p => new ExtensionInfo
			{
				ID = p.ID,
				DirectoryName = p.DirectoryName,
				ActiveExtensionIDs = p.ActiveExtensionIDs.ToArray(),
				RequestedExtensionIDs = p.RequestedExtensionIDs.ToArray()
			}).ToList();
		}

		public Guid? Execute(string dirName, params string[] extensionIDs)
		{
			ProcessInfo info;
			try
			{
				info = StartProcess(dirName, extensionIDs);
				if(info == null)
					return null;
				_processes.AddOrUpdate(info.ID, info, (k, p) =>
				{
					lock(p)
						if(!p.Process.HasExited)
						{
							p.Process.Kill();
							p.Process.Dispose();
						}
					return info;
				});
				return info.ID;
			}
			catch(Exception ex)
			{
				_logger.ErrorException(string.Format("Exception while calling Execute() -- dirName: {0}, extension IDs: {1}", dirName, extensionIDs.Concat(", ")), ex);
				return Guid.Empty;
			}
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

		public Result RestartExtensions(string subdirName, string[] extensionIDs)
		{
			int count = 0;
			foreach(var process in (from p in _processes.Values
									where (string.IsNullOrWhiteSpace(subdirName) || p.DirectoryName.ToLower() == subdirName.ToLower())
										&& (extensionIDs == null || extensionIDs.Length == 0 || (p.ActiveExtensionIDs != null && (from e in p.ActiveExtensionIDs
																															join x in extensionIDs on e equals x
																															select e).Count() > 0))
									select p))
			{
				process.RequestRestart = true;
				count++;
			}
			return new Result(count > 0, count + " matching extension process(es) were flagged for restart.");
		}

		public Result RestartExtension(Guid id)
		{
			ProcessInfo proc;
			if(!_processes.TryGetValue(id, out proc))
				return Result.Failed("No extension was found with the ID: " + id);
			proc.RequestRestart = true;
			return Result.Succeeded;
		}

		public bool IsValidID(Guid id)
		{
			ProcessInfo proc;
			return _processes.TryGetValue(id, out proc);
		}

		public void Stop(Guid id)
		{
			ProcessInfo info;
			if(_processes.TryGetValue(id, out info))
				info.RequestShutdown = true;
		}
	}
}
