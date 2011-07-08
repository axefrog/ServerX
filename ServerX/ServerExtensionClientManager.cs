using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using ServerX.Common;

namespace ServerX
{
	internal class ServerExtensionClientManager
	{
		ConcurrentDictionary<string, ClientInfo> _clients = new ConcurrentDictionary<string, ClientInfo>();

		private class ClientInfo : ExtensionInfo
		{
			public ServerExtensionClient Client { get; set; }
		}

		public bool TryConnect(string address)
		{
			var client = new ServerExtensionClient(address);
			var info = new ClientInfo();
			try
			{
				info.ID = client.ID;
				info.CommandID = Regex.Replace(client.CommandID ?? "", @"\s+", "").ToLower();
				info.Name = client.Name;
				info.Description = client.Description;
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

		public string ExecuteCommand(string extid, string args)
		{
			ClientInfo info;
			if(_clients.TryGetValue((extid ?? "").ToLower(), out info))
				return info.Client.Command(args);
			return "%!command " + extid + " is no longer available.";
		}

		public ExtensionInfo[] ListConnectedExtensions()
		{
			return _clients.Values.ToArray();
		}
	}
}
