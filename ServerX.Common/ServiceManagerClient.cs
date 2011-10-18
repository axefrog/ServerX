using System;
using System.ServiceModel;
using NLog;

namespace ServerX.Common
{
	public sealed class ServiceManagerClient : ClientBase<ServiceManagerClient, IServiceManager, ServiceManagerCallback>, IServiceManager
	{
		public ServiceManagerClient()
		{
		}

		public ServiceManagerClient(string address, int port)
			: this(address, port, new ServiceManagerCallback())
		{
		}

		private ServiceManagerClient(string address, int port, ServiceManagerCallback callback)
			: base(callback, new NetTcpBinding("Default"), new EndpointAddress("net.tcp://" + address + ":" + port + "/ServiceManager"))
		{
		}

		public ServiceManagerClient(string endpointConfigurationName) : base(endpointConfigurationName)
		{
		}

		protected override void InitCallback(ServiceManagerCallback callback)
		{
			callback.NotificationReceived += (src, msg, lvl) =>
			{
				var handler = NotificationReceived;
				if(handler != null)
					handler(src, msg, lvl);
			};
			callback.ExtensionNotificationReceived += (extID, extName, source, msg, level) =>
			{
				var handler = ExtensionNotificationReceived;
				if(handler != null)
					handler(extID, extName, source, msg, level);
			};
		}

		public event ServiceManagerCallback.ExtensionNotificationHandler ExtensionNotificationReceived;
		public event ServiceCallbackBase.NotificationHandler NotificationReceived;

		public DateTime GetServerTime()
		{
			return Channel.GetServerTime();
		}

		//public Result SetExtensionDirectoryIncluded(string name, bool include)
		//{
		//    return Channel.SetExtensionDirectoryIncluded(name, include);
		//}

		//public Result SetExtensionsEnabledInDirectory(string name, bool enabled)
		//{
		//    return Channel.SetExtensionsEnabledInDirectory(name, enabled);
		//}

		//public Result SetExtensionEnabled(string name, bool enabled)
		//{
		//    return Channel.SetExtensionEnabled(name, enabled);
		//}

		public Result RestartExtensions(string subdirName, params string[] extensionIDs)
		{
			return Channel.RestartExtensions(subdirName, extensionIDs);
		}

		public string[] ListExtensionDirectories()
		{
			return Channel.ListExtensionDirectories();
		}

		//public string[] ListIncludedExtensionDirectories()
		//{
		//    return Channel.ListIncludedExtensionDirectories();
		//}

		//public ExtensionInfo[] ListAvailableExtensions()
		//{
		//    return Channel.ListAvailableExtensions();
		//}

		public ExtensionInfo[] ListExtensionsInDirectory(string name)
		{
			return Channel.ListExtensionsInDirectory(name);
		}

		public string ExecuteCommand(string command, string[] args)
		{
			return Channel.ExecuteCommand(command, args);
		}

		public CommandInfo[] ListExtensionCommands()
		{
			return Channel.ListExtensionCommands();
		}

		public CommandInfo[] ListServiceManagerCommands()
		{
			return Channel.ListServiceManagerCommands();
		}

		public CommandInfo GetCommandInfo(string cmdAlias)
		{
			return Channel.GetCommandInfo(cmdAlias);
		}

		public ScriptInfo[] ListScripts()
		{
			return Channel.ListScripts();
		}

		public Result ExecuteScriptFile(string filename)
		{
			return Channel.ExecuteScriptFile(filename);
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

		public void NotifyExtensionServiceReady(Guid extProcID, string address)
		{
			Channel.NotifyExtensionServiceReady(extProcID, address);
		}
	}

	public class ServiceManagerCallback : IServiceManagerCallback
	{
		public void ServerExtensionNotify(string extID, string extName, string logLevel, string source, string message)
		{
			var handler = ExtensionNotificationReceived;
			if(handler != null)
				handler(extID, extName, logLevel, source, message);
		}

		public delegate void ExtensionNotificationHandler(string extID, string extName, string logLevel, string source, string message);
		public event ExtensionNotificationHandler ExtensionNotificationReceived;

		public void Notify(string logLevel, string source, string message)
		{
			var handler = NotificationReceived;
			if(handler != null)
				handler(logLevel, source, message);
		}

		public delegate void NotificationHandler(string logLevel, string source, string message);
		public event NotificationHandler NotificationReceived;
	}
}