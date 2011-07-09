using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using ServerX.Common;

namespace ServerX
{
	internal class ServerExtensionClientManager
	{
		ConcurrentDictionary<string, ClientInfo> _clients = new ConcurrentDictionary<string, ClientInfo>();

		private class ClientInfo : ExtensionInfo
		{
			[IgnoreDataMember]
			public ServerExtensionClient Client { get; set; }
		}

		public bool TryConnect(string address)
		{
			var info = new ClientInfo();
			ServerExtensionClient client;
			try
			{
				client = new ServerExtensionClient(address);
				client.RegisterClient();
				info.ID = client.ID;
				info.CommandID = Regex.Replace(client.CommandID ?? "", @"\s+", "").ToLower();
				info.Name = client.Name;
				info.Description = client.Description;
				info.SupportsCommandLine = client.SupportsCommandLine;
				info.Client = client;
			}
			catch
			{
				return false;
			}
			client.Disconnected += c =>
			{
				ClientInfo temp;
				_clients.TryRemove(info.ID, out temp);
			};
			client.NotificationReceived += msg =>
			{
				var handler = ExtensionNotificationReceived;
				if(handler != null)
					handler(info.ID, info.Name, msg);
			};

			// make sure the extension ID is unique
			if(_clients.ContainsKey(info.ID))
			{
				string cmdid = info.CommandID;
				for(var i = 2; i < 50; i++) // 50 is an arbitrary limit in case something unexpected causes the loop to continue forever
				{
					cmdid = info.CommandID + i;
					if(!_clients.ContainsKey(cmdid))
					{
						info.CommandID = cmdid;
						break;
					}
				}
				if(cmdid == info.CommandID)
				{
					client.Close();
					return false;
				}
			}

			return _clients.TryAdd(info.CommandID, info);
		}

		public string ExecuteCommand(string cmdID, string[] args)
		{
			ClientInfo info;
			if(_clients.TryGetValue((cmdID ?? "").ToLower(), out info))
				return info.Client.Command(args);
			return "%!command %@" + cmdID + "%@ is no longer available.";
		}

		public bool IsCommandAvailable(string cmd)
		{
			return _clients.ContainsKey(cmd);
		}

		public ExtensionInfo[] ListConnectedExtensions()
		{
			return _clients.Values.Select(e => e.Clone()).ToArray();
		}

		public event ServiceManagerCallback.ExtensionNotificationHandler ExtensionNotificationReceived;
	}
}
