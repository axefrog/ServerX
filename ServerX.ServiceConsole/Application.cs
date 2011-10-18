using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using ServerX.Common;
using ServerX.Service;
using NLog;

namespace ServerX.ServiceConsole
{
	public class Application : IDisposable
	{
		private readonly ConsoleEvents _consoleEvents;
		public bool ExitRequested { get; set; }
		private ServiceHost _serviceHost;
		private PersistenceManager<Settings> _settings = new PersistenceManager<Settings>("console");
		private bool _displayServerNotifications;

		readonly Regex _rxSplitCommand = new Regex(@"((""((?<match>.*?)(?<!\\)"")|(?<match>[\w\S]+))(\s)*)", RegexOptions.ExplicitCapture);
		readonly Regex _execMacro = new Regex(@"^\![A-Za-z0-9\-_]+$");
		readonly private Dictionary<string, ConsoleCommand> _commandsByAlias = new Dictionary<string, ConsoleCommand>();
		readonly private Dictionary<string, ConsoleCommand> _commandsByTitle = new Dictionary<string, ConsoleCommand>();
		
		public Dictionary<Type, IConsolePlugin> Plugins { get; private set; }
		public Dictionary<string, ConsoleCommand> CommandsByTitle { get { return _commandsByTitle; } }
		public Dictionary<string, ConsoleCommand> CommandsByAlias { get { return _commandsByAlias; } }

		internal Application(IEnumerable<IConsolePlugin> plugins, ConsoleEvents consoleEvents)
		{
			_consoleEvents = consoleEvents;
			Plugins = plugins.ToDictionary(p => p.GetType());
			//PluginsToWatch = new HashSet<string>();
		}

		public void RegisterCommand(ConsoleCommand cmd)
		{
			if(_commandsByTitle.ContainsKey(cmd.Title))
				throw new ArgumentException("A command with the ID [" + cmd.Title + "] has already been added", "cmd");
			_commandsByTitle.Add(cmd.Title, cmd);
			foreach(var alias in cmd.CommandAliases)
			{
				if(_commandsByAlias.ContainsKey(alias))
					throw new ArgumentException("A command with the command name/alias [" + alias + "] has already been added", "cmd");
				_commandsByAlias.Add(alias.ToLower(), cmd);
			}
		}

		public CommandInfo GetCommandHelp(string cmdAlias)
		{
			ConsoleCommand cmd;
			if(CommandsByAlias.TryGetValue(cmdAlias, out cmd))
				return cmd;
			if(Client != null && (Client.State == CommunicationState.Created || Client.State == CommunicationState.Opened))
				return Client.GetCommandInfo(cmdAlias);
			return null;
		}

		internal class ConsoleEvents
		{
			private Application _app;

			internal Application Application { set { _app = value; } }

			public void NotifyStarting()
			{
				if(_app.ConsoleStarting != null)
					_app.ConsoleStarting(_app);
			}

			public void NotifyReady()
			{
				if(_app.ConsoleReady != null)
					_app.ConsoleReady(_app);
			}

			public void NotifyClosing()
			{
				if(_app.ConsoleClosing != null)
					_app.ConsoleClosing(_app);
			}
		}

		public event ApplicationEventHandler ConsoleStarting;
		public event ApplicationEventHandler ConsoleReady;
		public event ApplicationEventHandler ConsoleClosing;

		public event Action<bool> DisplayServerNotificationsChanged;
		public bool DisplayServerNotifications
		{
			get { return _displayServerNotifications; }
			set
			{
				_displayServerNotifications = value;
				var handler = DisplayServerNotificationsChanged;
				if(handler != null)
					DisplayServerNotificationsChanged(value);
			}
		}

		public string Connect(string address, int port)
		{
			if(Client != null && Client.State == CommunicationState.Opened)
				Client.Abort();
				
			Client = new ServiceManagerClient(address, port);
			try
			{
				Client.RegisterClient();
			}
			catch(EndpointNotFoundException)
			{
				return "%!Unable to connect to " + address + ":" + port;
			}
			Client.Disconnected += OnServiceDisconnected;
			Client.NotificationReceived += OnNotificationReceived;
			Client.ExtensionNotificationReceived += OnExtensionNotificationReceived;

			var dt = Client.GetServerTime();
			return "%~Connected. Server time is " + dt + ".";
		}

		void OnNotificationReceived(string logLevel, string source, string message)
		{
			var handler = NotificationReceived;
			if(handler != null)
				handler(logLevel.ToString(), source, message);
		}

		void OnExtensionNotificationReceived(string extID, string extName, string logLevel, string source, string message)
		{
			var handler = ExtensionNotificationReceived;
			if(handler != null)
				handler(extID, extName, logLevel, source, message);
		}

		void OnServiceDisconnected(ServiceManagerClient psc)
		{
			Client = null;
		}

		public bool StartHost(int port, bool local = false)
		{
			StopHost();
			if(!local)
			{
				File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServiceParams.txt"),
					new JavaScriptSerializer().Serialize(new ServiceInstallParams { Port = port }));
				return WindowsServiceInstaller.Install(false, new string[0]);
			}

			var host = new ServiceHost(typeof(ServiceManager));
			host.AddServiceEndpoint(typeof(IServiceManager), new NetTcpBinding("Default"), "net.tcp://localhost:" + port + "/ServiceManager");
			try
			{
				host.Open();
			}
			catch
			{
				return false;
			}
			_serviceHost = host;
			return true;
		}

		public bool StopHost()
		{
			if(IsWindowsServiceRunning)
				return WindowsServiceInstaller.Install(true, new string[0]);

			if(_serviceHost != null)
			{
				_serviceHost.Close();
				_serviceHost = null;
				return true;
			}
			return false;
		}

		public void Execute(string commandString)
		{
		    var parsed = _rxSplitCommand.Matches((commandString ?? "").Trim()).Cast<Match>().Select(m => m.Groups["match"].Value.Replace("\\\"", "\""));
		    var cmd = parsed.FirstOrDefault();
		    var cmdargs = parsed.Skip(1).ToArray();
		    if(string.IsNullOrWhiteSpace(cmd))
		        return;
			cmd = cmd.ToLower();
			var macroMatch = _execMacro.Match(cmd);
			if(macroMatch.Success)
			{
				var macro = _settings.Values.Macros.FirstOrDefault(m => m.Name == macroMatch.Value.Substring(1));
				if(macro != null)
				{
					Execute(macro.Command);
					return;
				}
			}

			ConsoleCommand cc;
			if(!_commandsByAlias.TryGetValue(cmd, out cc))
			{
				if(Client != null)
				{
					var response = Client.ExecuteCommand(cmd, cmdargs);
					ColorConsole.WriteLine(response ?? "%~Ok.");
					return;
				}
				ColorConsole.WriteLine("Unrecognized command: " + cmd, ConsoleColor.Red);
				return;
			}

			var output = cc.Handler(this, cc, cmdargs);
			if(output != null)
				ColorConsole.WriteLine(output.TrimEnd());
		}

		public bool IsWindowsServiceRunning
		{
		    get { return File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Service.InstallState"); }
		}

		public ServiceManagerClient Client { get; private set; }

		public void AddMacro(string name, string command)
		{
			_settings.Values.AddMacro(name, command);
			_settings.Save();
		}

		public void DeleteMacro(string name)
		{
			_settings.Values.DeleteMacro(name);
			_settings.Save();
		}

		public List<Macro> ListMacros()
		{
			return _settings.Values.Macros;
		}

		public void Dispose()
		{
		}

		public event ServiceManagerCallback.ExtensionNotificationHandler ExtensionNotificationReceived;
		public event ServiceCallbackBase.NotificationHandler NotificationReceived;
	}

	public delegate void ApplicationEventHandler(Application app);
}
