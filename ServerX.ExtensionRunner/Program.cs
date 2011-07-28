using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mono.Options;
using ServerX.Common;

namespace ServerX.ExtensionRunner
{
	class Program
	{
		static void Main(string[] args)
		{
			string subdir = null, runDebugMethodOnExtension = null;
			var baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions");
			Environment.CurrentDirectory = ConfigurationManager.AppSettings["DataDirectory"] ?? AppDomain.CurrentDomain.BaseDirectory;
			var plugins = new HashSet<string>();
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
				{ "<>", v => plugins.Add(v) }
			};

			CancellationTokenSource src = new CancellationTokenSource();
			try
			{
				options.Parse(args);

				logger = new Logger("extension-" + subdir) { WriteToConsole = process == null };
				logger.WriteLines(
					"ExtensionRunner Started:",
					"	=> Command Line: " + Environment.CommandLine,
					"	=> Subdirectory: " + subdir,
					"	=> Base Directory: " + baseDir,
					"	=> Specified Plugins: " + plugins.Concat(", "),
					"	=> GUID: " + guid,
					"	=> Parent Process ID: " + (process == null ? "(none)" : process.Id.ToString())
				);

				AppDomain.CurrentDomain.UnhandledException += (s,e) => logger.Write("UNTRAPPED SERVICE EXCEPTION:\r\n" + e.ExceptionObject);
				TaskScheduler.UnobservedTaskException += (s,e) => logger.Write("UNTRAPPED TASK EXCEPTION:\r\n" + e.Exception);

				if(process != null)
				{
					Task.Factory.StartNew(() =>
					{
						while(!src.IsCancellationRequested)
						{
							process.Refresh();
							if(process.HasExited)
							{
								logger.WriteLine("Detected parent process shutdown.");
								Exit(logger, src, ExtensionRunnerExitCode.ParentExited);
								return;
							}
							Thread.Sleep(250);
						}
					});
				}

				// Read directory name and optional list of plugins to load
				if(subdir == null)
				{
					Console.Write("Enter plugin directory name (not the full path): ");
					subdir = Console.ReadLine();
					if(string.IsNullOrWhiteSpace(subdir))
					{
						Console.WriteLine("No plugin directory specified.");
						Exit(logger, src, ExtensionRunnerExitCode.InvalidArguments);
					}
				}

				using(var loader = new SafeExtensionLoader(baseDir, subdir, logger.WriteToConsole, src))
				{
					var runExtsTask = Task.Factory.StartNew(() =>
					{
						// Verify that all plugins are available and if so, run them
						logger.WriteLine("[list of all plugins]");
						foreach(var plugin in loader.AllExtensions)
							logger.WriteLine("\t" + plugin.ID + ": " + plugin.Name + " [" + (plugins.Count == 0 || plugins.Contains(plugin.ID) ? "ACTIVE" : "INACTIVE") + "]");
						logger.WriteLine("[end of plugins list]");
						loader.RunExtensions(guid, runDebugMethodOnExtension, plugins.ToArray());
					}, src.Token);

					loader.RunMainAppThread();
					Task.WaitAll(new[] { runExtsTask }, src.Token);
				}
			}
			catch(OptionException ex)
			{
				if(logger != null)
					logger.WriteLines("Invalid command options: " + ex.Message, options.WriteOptionDescriptions());
				Exit(logger, src, ExtensionRunnerExitCode.Exception);
			}
			catch(Exception ex)
			{
				if(logger != null)
					logger.WriteLine(ex.ToString());
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
				logger.WriteLine("Exiting (code: " + exitCode + ")");
			src.Cancel();
			Environment.Exit((int)exitCode);
		}
	}
}
