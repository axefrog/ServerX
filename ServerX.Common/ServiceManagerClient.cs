using System;
using System.ServiceModel;
using System.Timers;

namespace ServerX.Common
{
	public sealed class ServiceManagerClient : ClientBase<ServiceManagerClient, IServiceManagerHost, ServiceManagerCallback>, IServiceManagerHost
	{
		public ServiceManagerClient()
		{
		}

		public ServiceManagerClient(string address, int port)
			: this(address, port, new ServiceManagerCallback())
		{
		}

		private ServiceManagerClient(string address, int port, ServiceManagerCallback callback)
			: base(callback, new NetTcpBinding("Default"), new EndpointAddress("net.tcp://" + address + ":" + port + "/ServiceManagerHost"))
		{
		}

		public ServiceManagerClient(string endpointConfigurationName) : base(endpointConfigurationName)
		{
		}

		protected override void InitCallback(ServiceManagerCallback callback)
		{
			callback.NotificationReceived += msg =>
			{
				var handler = NotificationReceived;
				if(handler != null)
					handler(msg);
			};
			callback.ExtensionNotificationReceived += (extID, extName, msg) =>
			{
				var handler = ExtensionNotificationReceived;
				if(handler != null)
					handler(extID, extName, msg);
			};
		}

		public event ServiceManagerCallback.ExtensionNotificationHandler ExtensionNotificationReceived;
		public event ServiceCallbackBase.NotificationHandler NotificationReceived;

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

		public string ExecuteCommand(string command, string[] args)
		{
			return Channel.ExecuteCommand(command, args);
		}

		public ExtensionInfo[] ListExtensionCommands()
		{
			return Channel.ListExtensionCommands();
		}

		public Command[] ListServiceManagerCommands()
		{
			return Channel.ListServiceManagerCommands();
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

		public void NotifyExtensionServiceReady(string address)
		{
			Channel.NotifyExtensionServiceReady(address);
		}
	}

	public class ServiceManagerCallback : IServiceManagerCallback
	{
		public void ServerExtensionNotify(string extID, string extName, string message)
		{
			var handler = ExtensionNotificationReceived;
			if(handler != null)
				handler(extID, extName, message);
		}

		public delegate void ExtensionNotificationHandler(string extID, string extName, string message);
		public event ExtensionNotificationHandler ExtensionNotificationReceived;

		public void ServiceManagerNotify(string message)
		{
			var handler = NotificationReceived;
			if(handler != null)
				handler(message);
		}

		public delegate void NotificationHandler(string message);
		public event NotificationHandler NotificationReceived;
	}
}