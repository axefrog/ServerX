using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Mono.Options;
using Server.ClientConsole;
using ServerX.Service;

namespace ServerX.ServiceConsole.Plugins
{
	[Export(typeof(IConsolePlugin))]
	public class ServerPlugin : IConsolePlugin
	{
		public string Name
		{
			get { return "Server"; }
		}

		public string Description
		{
			get { return "Provides functionality for hosting and managing the main service"; }
		}

		const int DefaultPort = 13401;

		class ServiceCommandParams
		{
			public int Port = DefaultPort;
			public string Action = "status";
			public bool Local = false;
			public bool AutoConnect = false;
		}

		class ConnectCommandParams
		{
			public string Address = "localhost";
			public int Port = DefaultPort;
		}

		OptionSet GetServiceCommandOptions(ServiceCommandParams prms = null)
		{
			return new OptionSet
			{
				{ "local|l", "Hosts the service locally for debugging", v =>
					{
						prms.Local = v != null;
					}
				},
				{ "connect|c", "Connect to the service once it has started", v =>
					{
						prms.AutoConnect = true;
					}
				},
				{ "port|p=", "Specifies the port on which the service manager will listen for connections", v =>
					{
						int port;
						if(!int.TryParse(v, out port))
							throw new OptionException("The port must be an integer", "-p");
						prms.Port = port;
					}
				},
				{ "<>", "Specify %@status%@, %@start%@ or %@stop%@.", v =>
					{
						v = v.ToLower();
						switch(v)
						{
							case "status":
							case "start":
							case "stop":
								prms.Action = v;
								break;
							default:
								throw new OptionException("Unknown command action: " + v, "<>");
						}
					}
				},
			};
		}

		OptionSet GetConnectCommandOptions(ConnectCommandParams prms = null)
		{
			return new OptionSet
			{
				{ "address|a=", "Specifies the address to connect to", v =>
					{
						prms.Address = v;
					}
				},
				{ "port|p=", "Specifies the port to connect to", v =>
					{
						int port;
						if(!int.TryParse(v, out port))
							throw new OptionException("The port must be an integer", "-p");
						prms.Port = port;
					}
				},
			};
		}

		public void Init(Application application)
		{
			application.RegisterCommand(new ConsoleCommand
			{
				Title = "Service Manager",
				CommandAliases = new [] { "service", "svc", "s" },
				Description = "Controls the active state of the service",
				HelpOptions = GetServiceCommandOptions().WriteOptionDescriptions(),
				HelpUsage = "svc {start|stop|status} [options...]",
				HelpDescription = "Starts or stops the service manager, or obtains its status.",
				HelpRemarks = "Note that if no port is specified, the port will default to " + DefaultPort + ".",
				Handler = (app, command, args) =>
				{
					var prms = new ServiceCommandParams();
					var options = GetServiceCommandOptions(prms);
					try
					{
						options.Parse(args);
					}
					catch(OptionException e)
					{
						ColorConsole.WriteLine("%!" + e.Message + "%!");
						return null;
					}

					switch(prms.Action)
					{
						case "status":
							return null;

						case "start":
							File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServiceParams.txt"), new JavaScriptSerializer().Serialize(new ServiceInstallParams
							{
								Port = prms.Port
							}));
							if(!prms.Local && app.IsWindowsServiceRunning)
								return "%!Service is already installed.";
							
							var success = app.StartHost(prms.Port, prms.Local);
							ColorConsole.WriteLine(success ? "%~Service now running on port " + prms.Port + "." : "%~Unable to start service on port " + prms.Port + ".");
							if(prms.AutoConnect && success)
								return app.Connect("localhost", prms.Port);
							return null;

						case "stop":
							if(!prms.Local && !app.IsWindowsServiceRunning)
								return "%!Can't uninstall; service is not installed.";
							return app.StopHost() ? "%~Service uninstalled successfully." : "%!Service uninstallation failed.";
					}

					return null;
				}
			});

			application.RegisterCommand(new ConsoleCommand
			{
				Title = "Connect to Service Manager",
				CommandAliases = new [] { "connect", "conn", "c" },
				Description = "Attempts to connect to an active service manager",
				HelpDescription = "Connects to the service manager.",
				HelpOptions = GetConnectCommandOptions().WriteOptionDescriptions(),
				HelpUsage = "connect [options...]",
				HelpRemarks = "Note that if no port is specified, the port will default to " + DefaultPort + ".",
				Handler = (app, command, args) =>
				{
					var prms = new ConnectCommandParams();
					var options = GetConnectCommandOptions(prms);
					try
					{
						options.Parse(args);
					}
					catch(OptionException e)
					{
						ColorConsole.WriteLine("%!" + e.Message + "%!");
						return null;
					}

					return app.Connect(prms.Address, prms.Port);
				}
			});
		}
	}
}
