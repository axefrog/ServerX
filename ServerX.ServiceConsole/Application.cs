using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text.RegularExpressions;
using Server;
using ServerX.Common;
using ServerX.Service;

namespace ServerX.ServiceConsole
{
	public class Application : IDisposable
	{
		private readonly ConsoleEvents _consoleEvents;
		public bool ExitRequested { get; set; }
		private ServiceHost _serviceHost;
		private bool _displayServerNotifications;
		private PersistenceManager<Settings> _settings = new PersistenceManager<Settings>("console");

		readonly Regex _rxSplitCommand = new Regex(@"((""((?<match>.*?)(?<!\\)"")|(?<match>[\w\S]+))(\s)*)", RegexOptions.ExplicitCapture);
		readonly Regex _fastRun = new Regex(@"^\![A-Za-z0-9\-_]+$");
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

		private ServiceManagerClient _client;
		public string Connect(string address, int port)
		{
			if(_client != null && _client.State == CommunicationState.Opened)
				_client.Abort();
				
			_client = new ServiceManagerClient(address, port);
			try
			{
				_client.RegisterClient();
			}
			catch(EndpointNotFoundException)
			{
				return "%!Unable to connect to " + address + ":" + port;
			}
			_client.Disconnected += OnServiceDisconnected;
			var dt = _client.GetServerTime();
			return "%~Connected. Server time is " + dt + ".";
		}

		void OnServiceDisconnected(ServiceManagerClient psc)
		{
			_client = null;
		}

		public bool StartHost(int port, bool local = false)
		{
			StopHost();
			if(!local)
				return WindowsServiceInstaller.Install(false, new string[0]);

			var host = new ServiceHost(typeof(ServiceManagerHost));
			host.AddServiceEndpoint(typeof(IServiceManagerHost), new NetTcpBinding("Default"), "net.tcp://localhost:" + port + "/ServiceManagerHost");
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

		public void Execute(string commandString)
		{
		    var parsed = _rxSplitCommand.Matches((commandString ?? "").Trim()).Cast<Match>().Select(m => m.Groups["match"].Value.Replace("\\\"", "\""));
		    var cmd = parsed.FirstOrDefault();
		    var cmdargs = parsed.Skip(1).ToArray();
		    if(string.IsNullOrWhiteSpace(cmd))
		        return;
			cmd = cmd.ToLower();

			ConsoleCommand cc;
			if(!_commandsByAlias.TryGetValue(cmd, out cc))
			{
				ColorConsole.WriteLine("Unrecognized command: " + cmd, ConsoleColor.Red);
				return;
			}

			var response = cc.Handler(this, cc, cmdargs);
			if(response != null)
				ColorConsole.WriteLine(response.TrimEnd());
		}

		public bool IsWindowsServiceRunning
		{
		    get { return File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Service.InstallState"); }
		}

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
	}

	public delegate void ApplicationEventHandler(Application app);
}
