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

		public string JsonCall(string name, string data)
		{
			return Channel.JsonCall(name, data);
		}

		public string Command(string args)
		{
			return Channel.Command(args);
		}

		public bool SupportsCommandLine
		{
			get { return Channel.SupportsCommandLine; }
		}

		public bool SupportsJsonCall
		{
			get { return Channel.SupportsJsonCall; }
		}

		protected override void InitCallback(ServerExtensionCallback callback)
		{
			
		}
	}

	public class ServerExtensionCallback : ServiceCallbackBase, IServerExtensionCallback
	{
		public void Notify(string extID, string extName, string message)
		{
			var handler = ExtensionNotificationReceived;
			if(handler != null)
				handler(extID, extName, message);
		}

		public delegate void ExtensionNotificationHandler(string extID, string extName, string message);
		public event ExtensionNotificationHandler ExtensionNotificationReceived;
	}
}
