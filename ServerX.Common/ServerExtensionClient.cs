using System;
using System.ServiceModel;

namespace ServerX.Common
{
	public class ServerExtensionClient : ClientBase<ServerExtensionClient, IServerExtensionHost, ServerExtensionCallback>, IServerExtensionHost
	{
		public ServerExtensionClient(string tcpAddress)
			: base(new ServerExtensionCallback(), new NetTcpBinding("Default"), new EndpointAddress(tcpAddress))
		{
			if(!tcpAddress.StartsWith("net.tcp://"))
				throw new ArgumentException("Specified address must be a TCP endpoint address (net.tcp://...)", "tcpAddress");
		}

		public string ID
		{
			get { return Channel.ID; }
		}

		public string CommandID
		{
			get { return Channel.CommandID; }
		}

		public string Name
		{
			get { return Channel.Name; }
		}

		public string Description
		{
			get { return Channel.Description; }
		}

		public string JsonCall(string name, string[] jsonArgs)
		{
			return Channel.JsonCall(name, jsonArgs);
		}

		public string GetJavaScriptWrapper()
		{
			return Channel.GetJavaScriptWrapper();
		}

		public string Command(string[] args)
		{
			return Channel.Command(args);
		}

		public bool SupportsCommandLine
		{
			get { return Channel.SupportsCommandLine; }
		}

		protected override void InitCallback(ServerExtensionCallback callback)
		{
			callback.NotificationReceived += msg =>
			{
				var handler = NotificationReceived;
				if(handler != null)
					handler(msg);
			};
		}
		public event ServiceCallbackBase.NotificationHandler NotificationReceived;
	}

	public class ServerExtensionCallback : ServiceCallbackBase, IServerExtensionCallback
	{
	}
}
