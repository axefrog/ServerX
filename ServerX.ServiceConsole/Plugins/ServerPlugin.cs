using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Web.Script.Serialization;
using Mono.Options;
using ServerX.Common;
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
			public ServiceAction Action = ServiceAction.Status;
			public bool Local;
			public bool AutoConnect;
			public bool Watch { get; set; }
			public bool Exit { get; set; }

			public ServiceCommandParams()
			{
				AutoConnect = false;
			}
		}

		private enum ServiceAction
		{
			Status,
			Install,
			Uninstall
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
				{ "install|i", "Requests that the service be activated", v =>
					{
						prms.Action = ServiceAction.Install;
					}
				},
				{ "uninstall|u", "Requests that the service be activated", v =>
					{
						prms.Action = ServiceAction.Uninstall;
					}
				},
				{ "status|s", "Display the service status", v =>
					{
						prms.Action = ServiceAction.Status;
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
				{ "watch|w", "Puts the console into watch mode if --connect is also specified", v =>
					{
						prms.Watch = true;
					}
				},
				{ "exit|e", "Exits the console once the service command completes", v =>
					{
						prms.Exit = true;
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

		private void Watch(Application app)
		{
			Console.CursorVisible = false;
			ColorConsole.WriteLine("%~Watch mode is active.%~ %?Press any key to exit watch mode.%?");
			app.DisplayServerNotifications = true;
			while(!Console.KeyAvailable)
				Thread.Sleep(10);
			app.DisplayServerNotifications = false;
			Console.ReadKey(true);
			ColorConsole.WriteLine("Watch mode deactivated.", ConsoleColor.Green);
			Console.CursorVisible = true;
		}

		public void Init(Application application)
		{
			application.RegisterCommand(new ConsoleCommand
			{
				Title = "Service Manager",
				CommandAliases = new [] { "service", "svc", "s" },
				ShortDescription = "Controls the active state of the service",
				HelpOptions = GetServiceCommandOptions().WriteOptionDescriptions(),
				HelpUsage = "svc {start|stop|status} [options...]",
				HelpDescription = "Starts or stops the service manager, or obtains its status.",
				HelpRemarks = "Note that if no port is specified, the port will default to " + DefaultPort + ".",
				Handler = (app, command, args) =>
				{
					app.DisplayServerNotifications = true;
					try
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
							case ServiceAction.Status:
								break;

							case ServiceAction.Install:
								bool success = true;
								if(!prms.Local && app.IsWindowsServiceRunning)
									ColorConsole.WriteLine("%!Service is already installed.%!");
								else
								{
									success = app.StartHost(prms.Port, prms.Local);
									ColorConsole.WriteLine(success ? "%~Service now running on port " + prms.Port + "." : "%~Unable to start service on port " + prms.Port + ".");
								}
								if(prms.AutoConnect && success)
								{
									ColorConsole.WriteLine(app.Connect("localhost", prms.Port));
									if(prms.Watch)
										Watch(app);
								}
								break;

							case ServiceAction.Uninstall:
								//if(!prms.Local && !app.IsWindowsServiceRunning)
								//    return "%!Can't uninstall; service is not installed.";
								ColorConsole.WriteLine(app.UninstallService() ? "%~Service uninstalled successfully." : "%!Service uninstallation failed.");
								break;
						}

						if(prms.Exit && !prms.Watch)
							app.ExitRequested = true;

						return null;
					}
					finally
					{
						app.DisplayServerNotifications = false;
					}
				}
			});

			application.RegisterCommand(new ConsoleCommand
			{
				Title = "Connect to Service Manager",
				CommandAliases = new [] { "connect", "conn", "c" },
				ShortDescription = "Attempts to connect to an active service manager",
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

			application.RegisterCommand(new ConsoleCommand
			{
				Title = "Watch for Notifications",
				CommandAliases = new [] { "watch", "w" },
				ShortDescription = "Displays notifications as they are received",
				HelpDescription = "Puts the console into an idle state where it will display any server notifications received.",
				Handler = (app, command, args) =>
				{
					Watch(app);
					return null;
				}
			});
		}
	}
}
