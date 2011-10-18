using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Mono.Options;
using NLog;
using NLog.Config;
using ServerX.Common;
using Logger = NLog.Logger;

namespace ServerX.ExtensionRunner
{
	class Program
	{
		static void Main(string[] args)
		{
			ConfigurationItemFactory.Default.Targets.RegisterDefinition("ServiceManager", typeof(ServiceManagerTarget));

			string subdir = null, runDebugMethodOnExtension = null;
			var baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions");
			Environment.CurrentDirectory = ConfigurationManager.AppSettings["DataDirectory"] ?? AppDomain.CurrentDomain.BaseDirectory;
			var extensionIDs = new HashSet<string>();
			Process process = null;
			Guid guid = Guid.Empty;
			Logger logger = null;

			var options = new OptionSet
			{
				{ "guid=", "Specifies a GUID that the extension can use to identify itself to the parent process", v =>
					{
						Guid id;
						if(!Guid.TryParse(v, out id))
							throw new OptionException("The specified id was not a valid GUID", "guid");
						guid = id;
					}
				},
				{ "basedir=", "Specifies the base plugins directory (can be relative or absolute)", v => baseDir = Path.IsPathRooted(v) ? v : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, v) },
				{ "subdir=", "Specifies the extension subdirectory name", v => subdir = v },
				{ "debug=", "Specifies an extension ID to run the debug method on", v => runDebugMethodOnExtension = v },
				{ "pid=", "Parent process ID - if specified, this process will close when the parent process closes", v =>
					{
						int pid;
						if(!int.TryParse(v, out pid))
							throw new OptionException("The parent process ID must be a 32 bit integer", "pid");
						try
						{
							process = Process.GetProcessById(pid);
						}
						catch(Exception ex)
						{
							throw new OptionException(ex.Message, "pid");
						}
						if(process == null)
							throw new OptionException("There is no process with ID [" + pid + "]", "pid");
					}
				},
				{ "<>", v => extensionIDs.Add(v) }
			};

			CancellationTokenSource src = new CancellationTokenSource();
			try
			{
				options.Parse(args);
				if(subdir == null)
				{
					Console.Write("Enter plugin directory name (not the full path): ");
					subdir = Console.ReadLine();
					if(string.IsNullOrWhiteSpace(subdir))
					{
						Console.WriteLine("No plugin directory specified.");
						Exit(null, src, ExtensionRunnerExitCode.InvalidArguments);
					}
				}

				GlobalDiagnosticsContext.Set("ExeBaseDir", new FileInfo(Assembly.GetExecutingAssembly().Location).Directory.FullName);
				GlobalDiagnosticsContext.Set("SubDirName", subdir);
				GlobalDiagnosticsContext.Set("ParentProcess", process == null ? "" : process.Id.ToString());
				logger = LogManager.GetCurrentClassLogger();
				logger.Info(new [] {
					"ExtensionRunner Started:",
					"	=> Command Line: " + Environment.CommandLine,
					"	=> Subdirectory: " + subdir,
					"	=> Base Directory: " + baseDir,
					"	=> Specified Extensions: " + extensionIDs.Concat(", "),
					"	=> GUID: " + guid,
					"	=> Parent Process ID: " + (process == null ? "(none)" : process.Id.ToString())
				}.Concat(Environment.NewLine));

				AppDomain.CurrentDomain.UnhandledException += (s,e) => logger.FatalException("UNTRAPPED SERVICE EXCEPTION", (Exception)e.ExceptionObject);
				TaskScheduler.UnobservedTaskException += (s,e) => logger.FatalException("UNTRAPPED TASK EXCEPTION:", e.Exception);

				if(process != null)
				{
					Task.Factory.StartNew(() =>
					{
						while(!src.IsCancellationRequested)
						{
							process.Refresh();
							if(process.HasExited)
							{
								logger.Warn("Detected parent process shutdown.");
								Exit(logger, src, ExtensionRunnerExitCode.ParentExited);
								return;
							}
							Thread.Sleep(250);
						}
					});
				}

				// Read list of available extensions
				Dictionary<string, ExtensionInfo> extInfos;
				using(var loader = new SafeExtensionLoader(baseDir, subdir, process == null ? "" : process.Id.ToString(), src))
					extInfos = loader.AvailableExtensions.ToDictionary(x => x.ExtensionID, x => x.Clone());

				if(extensionIDs.Count == 0)
					extensionIDs = new HashSet<string>(extInfos.Select(x => x.Key)); // use all available extensions
				else
					extensionIDs = new HashSet<string>(extensionIDs.Where(x => extInfos.ContainsKey(x))); // eliminate invalid any extension IDs
				logger.Info("Active extensions: " + (extensionIDs.Any() ? extensionIDs.Concat(", ") : "(none)"));
				logger.Info("Inactive extensions: " + (!extensionIDs.Any() ? extInfos.Where(x => !extensionIDs.Contains(x.Key)).Concat(", ") : "(none)"));

				var extLoaders = new List<SafeExtensionLoader>();
				var extTasks = new List<Task>();
				try
				{
					foreach(var id in extensionIDs)
					{
						logger.Debug("Starting appdomain for extension: {0}", id);
						var loader = new SafeExtensionLoader(baseDir, subdir, process == null ? "" : process.Id.ToString(), src);
						var extID = id;
						extTasks.Add(Task.Factory.StartNew(() => loader.RunExtension(guid, runDebugMethodOnExtension == extID, extID)));
					}
					Task.WaitAll(extTasks.ToArray(), src.Token);
				}
				finally
				{
					foreach(var extLoader in extLoaders)
						extLoader.Dispose();
				}
				//using(var loader = new SafeExtensionLoader(baseDir, subdir, process == null ? "" : process.Id.ToString(), src))
				//{
				//    var runExtsTask = Task.Factory.StartNew(() =>
				//    {
				//        // Verify that all extensions are available and if so, run them
				//        var sb = new StringBuilder();
				//        sb.AppendLine("[list of all plugins]");
				//        foreach(var extInfo in loader.AllExtensions)
				//            sb.AppendLine("\t" + extInfo.ExtensionID + ": " + extInfo.Name + " [" + (extensionIDs.Count == 0 || extensionIDs.Contains(extInfo.ExtensionID) ? "ACTIVE" : "INACTIVE") + "]");
				//        logger.Info(sb.ToString());
				//        loader.RunExtensions(guid, runDebugMethodOnExtension, extensionIDs.ToArray());
				//    }, src.Token);

				//    loader.RunMainAppThread();
				//    Task.WaitAll(new[] { runExtsTask }, src.Token);
				//}
			}
			catch(OptionException ex)
			{
				if(logger != null)
					logger.Error("Invalid command options: " + ex.Message, options.WriteOptionDescriptions());
				Exit(logger, src, ExtensionRunnerExitCode.Exception);
			}
			catch(Exception ex)
			{
				if(logger != null)
					logger.FatalException("An exception was thrown", ex);
				Exit(logger, src, ExtensionRunnerExitCode.Exception);
			}
			finally
			{
				Exit(logger, src, ExtensionRunnerExitCode.Success);
			}
		}

		static void Exit(Logger logger, CancellationTokenSource src, ExtensionRunnerExitCode exitCode)
		{
			if(logger != null)
				logger.Info("Exiting (code: " + exitCode + ")");
			src.Cancel();
			Environment.Exit((int)exitCode);
		}
	}
}
