using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Text.RegularExpressions;
using ServerX.Common;

namespace ServerX
{
	internal class ExtensionClientManager
	{
		private readonly ExtensionProcessManager _extProcMgr;
		private readonly Logger _logger;
		ConcurrentDictionary<string, ClientInfo> _clients = new ConcurrentDictionary<string, ClientInfo>();

		public ExtensionClientManager(ExtensionProcessManager extProcMgr, Logger logger)
		{
			_extProcMgr = extProcMgr;
			_logger = logger;
		}

		internal class ClientInfo : ExtensionInfo
		{
			[IgnoreDataMember]
			public ServerExtensionClient Client { get; set; }

			public Guid ExtProcID { get; set; }
		}

		public ClientInfo TryConnect(Guid extProcID, string address)
		{
			var info = new ClientInfo();
			ServerExtensionClient client;
			try
			{
				client = new ServerExtensionClient(address);
				client.RegisterClient();
				info.ID = client.ID;
				info.ExtProcID = extProcID;
				info.CommandID = Regex.Replace(client.CommandID ?? "", @"\s+", "").ToLower();
				info.Name = client.Name;
				info.Description = client.Description;
				info.SupportsCommandLine = client.SupportsCommandLine;
				info.Client = client;
				_logger.WriteLine("Extension Client Manager", "Connected to extension: " + info.Name);
			}
			catch(Exception ex)
			{
				_logger.WriteLine("Extension Client Manager", "Exception thrown while trying to connect to extension \"" + info.Name + "\":" + Environment.NewLine + ex);
				return null;
			}
			client.Disconnected += c =>
			{
				ClientInfo temp;
				_clients.TryRemove(info.ID, out temp);
				_logger.WriteLine("Extension Client Manager", "Lost connection to extension: " + (temp == null ? "(Unknown)" : temp.Name));
			};
			client.NotificationReceived += (src, msg) =>
			{
				var handler = ExtensionNotificationReceived;
				if(handler != null)
					handler(info.ID, info.Name, src, msg);
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
					return null;
				}
			}

			return _clients.TryAdd(info.CommandID, info) ? info : null;
		}

		public string ExecuteCommand(string cmdID, string[] args)
		{
			ClientInfo info;
			if(_clients.TryGetValue((cmdID ?? "").ToLower(), out info))
			{
				try
				{
					return info.Client.Command(args);
				}
				catch(CommunicationObjectFaultedException)
				{
					_extProcMgr.RestartExtension(info.ExtProcID);
					var msg = "command " + info.CommandID + " failed - the connection to the extension has broken. The extension will be restarted. Check the logs for fault exception details.";
					_logger.WriteLine("Extension Client Manager", msg);
					return "%!" + msg;
				}
				catch(Exception ex)
				{
					_extProcMgr.RestartExtension(info.ExtProcID);
					var msg = "command " + info.CommandID + " failed - An exception was thrown. The extension will be restarted. Exception details: " + ex;
					_logger.WriteLine("Extension Client Manager", msg);
					return "%!" + msg;
				}
			}
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

		public string GetJavaScriptWrappers()
		{
			var sb = new StringBuilder();
			foreach(var client in _clients.Values.ToArray())
				if(client.Client.State == CommunicationState.Opened)
				{
					try
					{
						sb.AppendLine(client.Client.GetJavaScriptWrapper());
					}
					catch(Exception ex)
					{
						sb.AppendLine("// ERROR: Unable to generate wrapper for extension " + client.ID + ": " + ex.Message);
					}
				}
			return sb.ToString();
		}

		public string JsonCall(string extID, string methodName, string[] jsonArgs)
		{
			ClientInfo client = _clients.Values.FirstOrDefault(c => c.ID == extID);
			if(client == null || client.Client.State != CommunicationState.Opened)
				return JavaScriptInterface.JsonErrorResponse("The specified extension (" + extID + ") is not currently connected - it may have crashed or be restarting");
			try
			{
				return client.Client.JsonCall(methodName, jsonArgs);
			}
			catch(Exception ex)
			{
				return JavaScriptInterface.JsonErrorResponse("There was an error communicating with the extension: " + ex.Message);
			}
		}
	}
}
