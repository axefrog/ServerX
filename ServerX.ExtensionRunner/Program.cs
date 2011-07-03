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
			string subdir = null;
			var baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions");
			Environment.CurrentDirectory = ConfigurationManager.AppSettings["DataDirectory"] ?? AppDomain.CurrentDomain.BaseDirectory;
			var plugins = new HashSet<string>();
			Process process = null;
			Guid guid = Guid.Empty;

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
					}
				},
				{ "<>", v => plugins.Add(v) }
			};

			CancellationTokenSource src = new CancellationTokenSource();
			if(process != null)
			{
				Task.Factory.StartNew(() =>
				{
					while(!src.IsCancellationRequested)
					{
						if(process.HasExited)
						{
							Exit(src, ExtensionRunnerExitCode.ParentExited);
							return;
						}
						Thread.Sleep(100);
					}
				});
			}

			try
			{
				options.Parse(args);

				// Read directory name and optional list of plugins to load
				if(subdir == null)
				{
					Console.Write("Enter plugin directory name (not the full path): ");
					subdir = Console.ReadLine();
					if(string.IsNullOrWhiteSpace(subdir))
					{
						Console.WriteLine("No plugin directory specified.");
						Exit(src, ExtensionRunnerExitCode.InvalidArguments);
					}
				}

				// Verify that all plugins are available and if so, run them
				using(var loader = new SafeExtensionLoader(baseDir, subdir))
				{
					Console.WriteLine("<list of all plugins>");
					foreach(var plugin in loader.AllExtensions)
						Console.WriteLine(plugin.ID + ": " + plugin.Name + " [" + (plugins.Count == 0 || plugins.Contains(plugin.ID) ? "ACTIVE" : "INACTIVE") + "]");
					Console.WriteLine("<end of plugins list>");
					loader.RunExtensions(guid, plugins.ToArray());
				}
			}
			catch(OptionException ex)
			{
				Console.WriteLine("Invalid command options: " + ex.Message);
				Console.WriteLine(options.WriteOptionDescriptions());
				Exit(src, ExtensionRunnerExitCode.Exception);
			}
			catch(Exception ex)
			{
				Console.WriteLine(ex.ToString());
				Exit(src, ExtensionRunnerExitCode.Exception);
			}
			finally
			{
				Exit(src, ExtensionRunnerExitCode.Success);
			}
		}

		static void Exit(CancellationTokenSource src, ExtensionRunnerExitCode exitCode)
		{
			Console.WriteLine("Done - exiting.");
			src.Cancel();
			Environment.Exit((int)exitCode);
		}
	}
}
