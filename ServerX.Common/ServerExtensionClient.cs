using System;
using System.ServiceModel;

namespace ServerX.Common
{
	public class ServerExtensionClient : ClientBase<ServerExtensionClient, IServerExtension, ServerExtensionCallback>, IServerExtension
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

		public string Name
		{
			get { return Channel.Name; }
		}

		public string Description
		{
			get { return Channel.Description; }
		}

		public bool SingleInstanceOnly
		{
			get { return Channel.SingleInstanceOnly; }
		}

		public string JsonCall(string name, string[] jsonArgs)
		{
			return Channel.JsonCall(name, jsonArgs);
		}

		public string GetJavaScriptWrapper()
		{
			return Channel.GetJavaScriptWrapper();
		}

		public CommandInfo[] GetCommands()
		{
			return Channel.GetCommands();
		}

		public string Command(string cmdAlias, string[] args)
		{
			return Channel.Command(cmdAlias, args);
		}

		void IServerExtension.Debug()
		{
			throw new NotSupportedException();
		}

		protected override void InitCallback(ServerExtensionCallback callback)
		{
			callback.NotificationReceived += (src, msg, lvl) =>
			{
				var handler = NotificationReceived;
				if(handler != null)
					handler(src, msg, lvl);
			};
		}
		public event ServiceCallbackBase.NotificationHandler NotificationReceived;
	}

	public class ServerExtensionCallback : ServiceCallbackBase, IServerExtensionCallback
	{
	}
}
