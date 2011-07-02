using System;
using System.ServiceModel;
using System.Timers;
using ServerX.Common;

namespace Server
{
	public sealed class ServiceManagerClient : DuplexClientBase<IServiceManagerHost>, IServiceManagerHost
	{
		private Guid? _id;
		public Guid ID
		{
			get
			{
				if(!_id.HasValue)
					throw new InvalidOperationException("Call RegisterClient() before reading the ID property");
				return _id.Value;
			}
			private set { _id = value; }
		}

		public ServiceManagerClient()
			: this(new ServiceManagerCallback())
		{
		}

		public ServiceManagerClient(string endpointConfigurationName)
			: this(endpointConfigurationName, new ServiceManagerCallback())
		{
		}

		private ServiceManagerClient(ServiceManagerCallback callback)
			: base(callback)
		{
			Construct(callback);
		}

		private ServiceManagerClient(string endpointConfigurationName, ServiceManagerCallback callback)
			: base(callback, endpointConfigurationName)
		{
			Construct(callback);
		}

		private void Construct(ServiceManagerCallback callback)
		{
			//callback.NotificationReceived += OnNotificationReceived;
			//callback.PluginStatusChanged += OnPluginStatusChanged;
			InnerDuplexChannel.Closing += OnInnerDuplexChannelClosing;
			InnerDuplexChannel.Opening += OnInnerDuplexChannelOpening;
			InnerDuplexChannel.Faulted += OnInnerDuplexChannelFaulted;
			_keepAliveTimer.Elapsed += OnKeepAliveTimerElapsed;
		}

		public ServiceManagerClient(string address, int port)
			: this(address, port, new ServiceManagerCallback())
		{
		}
		private ServiceManagerClient(string address, int port, ServiceManagerCallback callback)
			: base(callback, new NetTcpBinding("Default"), new EndpointAddress("net.tcp://" + address + ":" + port + "/ServiceManagerHost"))
		{
			Construct(callback);
		}

		private Timer _keepAliveTimer = new Timer(5000);
		private void OnInnerDuplexChannelOpening(object sender, EventArgs e)
		{
			_keepAliveTimer.Start();
		}

		void OnInnerDuplexChannelClosing(object sender, EventArgs e)
		{
			OnDisconnected();
			_keepAliveTimer.Stop();
		}

		void OnInnerDuplexChannelFaulted(object sender, EventArgs e)
		{
			OnDisconnected();
			_keepAliveTimer.Stop();
		}

		void OnDisconnected()
		{
			var handler = Disconnected;
			if(handler != null)
				handler(this);
		}

		void OnKeepAliveTimerElapsed(object sender, ElapsedEventArgs e)
		{
			KeepAlive();
		}

		//void OnNotificationReceived(LogMode mode, string plugin, string[] messages)
		//{
		//    var handler = MessagesReceived;
		//    if(handler != null)
		//        handler(mode, plugin, messages);
		//}
		//void OnPluginStatusChanged(string plugin, string status)
		//{
		//    var handler = StatusChanged;
		//    if(handler != null)
		//        handler(plugin, status);
		//}

		//public string SendCommand(string command, string[] args)
		//{
		//    var str = Channel.SendCommand(command, args);
		//    if(str == ((char)27).ToString())
		//        throw new CommandNotFoundException(command);
		//    return str;
		//}

		//public PluginCommandInfo[] ListCommands()
		//{
		//    return Channel.ListCommands();
		//}

		//public string GetCommandHelp(string command)
		//{
		//    return Channel.GetCommandHelp(command);
		//}

		///// <summary>
		///// Informs all plugins that any long-running processes should be finalised and stopped, and that the plugin should enter a state where the service can safely shut down.
		///// </summary>
		//public void RequestShutdown()
		//{
		//    Channel.RequestShutdown();
		//}

		//public string GetServiceName()
		//{
		//    return Channel.GetServiceName();
		//}

		//public event PluginServiceNotify MessagesReceived;
		//public event PluginServiceNotifyStatus StatusChanged;
		public event ServiceManagerClientDisconnectedHandler Disconnected;
		void IServiceManagerHost.RegisterClient(Guid id)
		{
			ID = id;
			Channel.RegisterClient(id);
		}

		public void RegisterClient()
		{
			((IServiceManagerHost)this).RegisterClient(Guid.NewGuid());
		}

		public void KeepAlive()
		{
			try
			{
				if(State == CommunicationState.Opened)
					try
					{
						Channel.KeepAlive();
					}
					catch(ObjectDisposedException)
					{
					}
			}
			catch(TimeoutException)
			{
				TimedOut = true;
				try
				{
					Close();
				}
				catch
				{
				}
			}
		}

		public bool TimedOut { get; set; }

		internal class ServiceManagerCallback : IServiceManagerCallback
		{
			public void SendMessage(string message)
			{

			}

			//public void Notify(LogMode mode, string plugin, string[] messages)
			//{
			//    var handler = NotificationReceived;
			//    if(handler != null)
			//        handler(mode, plugin, messages);
			//}

			//public void NotifyStatus(string plugin, string status)
			//{
			//    var handler = PluginStatusChanged;
			//    if(handler != null)
			//        handler(plugin, status);
			//}

			//public event PluginServiceNotify NotificationReceived;
			//public event PluginServiceNotifyStatus PluginStatusChanged;
		}

		public DateTime GetServerTime()
		{
			return Channel.GetServerTime();
		}

		public Result SetExtensionDirectoryIncluded(string name, bool include)
		{
			return Channel.SetExtensionDirectoryIncluded(name, include);
		}

		public Result SetExtensionsEnabledInDirectory(string name, bool enabled)
		{
			return Channel.SetExtensionsEnabledInDirectory(name, enabled);
		}

		public Result SetExtensionEnabled(string name, bool enabled)
		{
			return Channel.SetExtensionEnabled(name, enabled);
		}

		public Result RestartExtension(string name)
		{
			return Channel.RestartExtension(name);
		}

		public Result RestartExtensionsInDirectory(string name)
		{
			return Channel.RestartExtensionsInDirectory(name);
		}

		public string[] ListExtensionDirectories()
		{
			return Channel.ListExtensionDirectories();
		}

		public string[] ListIncludedExtensionDirectories()
		{
			return Channel.ListIncludedExtensionDirectories();
		}

		public ExtensionInfo[] ListAvailableExtensions()
		{
			return Channel.ListAvailableExtensions();
		}

		public ExtensionInfo[] ListExtensionsInDirectory(string name)
		{
			return Channel.ListExtensionsInDirectory(name);
		}

		public string ExecuteCommand(string command)
		{
			return Channel.ExecuteCommand(command);
		}

		public CommandInfo ListCommands()
		{
			return Channel.ListCommands();
		}

		public string GetCommandHelp(string command)
		{
			return Channel.GetCommandHelp(command);
		}

		public ScriptInfo[] ListScripts()
		{
			return Channel.ListScripts();
		}

		public string ExecuteScript(string name)
		{
			return Channel.ExecuteScript(name);
		}

		public ScriptInfo GetScript(string name)
		{
			return Channel.GetScript(name);
		}

		public string SaveScript(ScriptInfo script)
		{
			return Channel.SaveScript(script);
		}

		public string DeleteScript(string name)
		{
			return Channel.DeleteScript(name);
		}

		public ScheduledCommand[] ListScheduledCommands()
		{
			return Channel.ListScheduledCommands();
		}

		public ScheduledCommand AddScheduledCommand(string cron, string command)
		{
			return Channel.AddScheduledCommand(cron, command);
		}

		public Result DeleteScheduledCommand(int id)
		{
			return Channel.DeleteScheduledCommand(id);
		}

		public void KeepExtensionProcessAlive(Guid id)
		{
			Channel.KeepExtensionProcessAlive(id);
		}
	}

	public delegate void ServiceManagerClientDisconnectedHandler(ServiceManagerClient client);
}