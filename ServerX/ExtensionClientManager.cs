using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using NLog;
using ServerX.Common;

namespace ServerX
{
	internal class ExtensionClientManager
	{
		private readonly ExtensionProcessManager _extProcMgr;
		private readonly Logger _logger = LogManager.GetCurrentClassLogger();
		List<ClientInfo> _clients = new List<ClientInfo>();

		public ExtensionClientManager(ExtensionProcessManager extProcMgr)
		{
			_extProcMgr = extProcMgr;
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
				info.ExtensionID = client.ID;
				if(client.SingleInstanceOnly)
				{
					bool isNotUnique;
					lock(_clients)
						isNotUnique = _clients.Any(c => c.ExtensionID == info.ExtensionID);
					if(isNotUnique)
					{
						_logger.Warn("A second instance of the extension \"" + info.ExtensionID + "\" was found, but the extension has specified that only one instance is allowed to run at a time. The second instance will be stopped.");
						_extProcMgr.Stop(extProcID);
						return null;
					}
				}
				info.ExtProcID = extProcID;
				info.Name = client.Name;
				info.Description = client.Description;
				info.Commands = client.GetCommands();
				info.Client = client;
				_logger.Info("Connected to extension: " + info.Name);
			}
			catch(Exception ex)
			{
				_logger.ErrorException("Exception thrown while trying to connect to extension \"" + info.Name + "\"", ex);
				return null;
			}
			client.Disconnected += c =>
			{
				lock(_clients)
					_clients.Remove(info);
				_logger.Info("Lost connection to extension: " + info.Name);
			};
			client.NotificationReceived += (src, msg, lvl) =>
			{
				var handler = ExtensionNotificationReceived;
				if(handler != null)
					handler(info.ExtensionID, info.Name, src, msg, lvl);
			};

			lock(_clients)
				_clients.Add(info);
			return info;
		}

		private bool GetClient(string extID, int extNum, string cmdAlias, out ClientInfo client, out CommandInfo cmd)
		{
			cmd = null;
			IEnumerable<ClientInfo> candidates;
			// get all the clients with the matching extension ID, or all clients if no ID was specified
			lock(_clients)
				candidates = _clients.Where(c => extID == null || string.Compare(c.ExtensionID, extID, true) == 0);
			// filter down to clients containing the required command, then if no ID was specified, select the first
			// matching client, or if an ID and an extension number was specified, select the nth matching client.
			client = (from c in candidates
					  where c.Commands != null
					  from cc in c.Commands
					  where cc.CommandAliases != null
					  from ca in cc.CommandAliases
					  where string.Compare(cmdAlias, ca, true) == 0
					  select c).Skip(extID == null ? 0 : extNum - 1).FirstOrDefault();
			if(client != null)
			{
				// select the first matching command with a preference for a command alias that appears first in the list.
				// note that this obtuse way of getting the command is to allow for badly written extensions that contain
				// more than one command with the same command alias.
				cmd = client.Commands.Where(c => c.CommandAliases != null && c.CommandAliases.FirstOrDefault() == cmdAlias).FirstOrDefault()
					?? client.Commands.Where(c => c.CommandAliases != null && c.CommandAliases.Any(a => a == cmdAlias)).FirstOrDefault();
			}
			return cmd != null;
		}

		public CommandInfo GetCommandInfo(string extID, int extNum, string cmdAlias)
		{
			ClientInfo client;
			CommandInfo cmd;
			return GetClient(extID, extNum, cmdAlias, out client, out cmd) ? cmd : null;
		}

		public string ExecuteCommand(string extID, int extNum, string cmdAlias, string[] args)
		{
			cmdAlias = cmdAlias.ToLower();
			ClientInfo matchingClient;
			CommandInfo cmd;
			if(GetClient(extID, extNum, cmdAlias, out matchingClient, out cmd))
			{
				cmdAlias = cmd.CommandAliases.First();
				try
				{
					return matchingClient.Client.Command(cmdAlias, args);
				}
				catch(CommunicationObjectFaultedException)
				{
					_extProcMgr.RestartExtension(matchingClient.ExtProcID);
					var msg = "command " + cmdAlias + " failed - the connection to the extension has broken. The extension will be restarted. Check the logs for fault exception details.";
					_logger.Warn(msg);
					lock(_clients)
						_clients.Remove(matchingClient);
					return "%!" + msg;
				}
				catch(Exception ex)
				{
					_extProcMgr.RestartExtension(matchingClient.ExtProcID);
					var msg = "command " + cmdAlias + " failed - An exception was thrown. The extension will be restarted. Exception details: " + ex;
					_logger.Warn(msg);
					lock(_clients)
						_clients.Remove(matchingClient);
					return "%!" + msg;
				}
			}
			return "%!command %@" + cmdAlias + "%@ is invalid or is no longer available.";
		}

		public bool IsCommandAvailable(string cmd)
		{
			cmd = cmd.ToLower();
			lock(_clients)
				return _clients.Any(c => c.Commands != null && c.Commands.Any(cc => cc.CommandAliases != null && cc.CommandAliases.Any(a => a.ToLower() == cmd)));
		}

		public ExtensionInfo[] ListConnectedExtensions()
		{
			lock(_clients)
				return _clients.Select(e => e.Clone()).ToArray();
		}

		public event ServiceManagerCallback.ExtensionNotificationHandler ExtensionNotificationReceived;

		public string GetJavaScriptWrappers()
		{
			var sb = new StringBuilder();

			// only select the first client for each extension ID (will rework scripting later to make it possible to interact with multiple instances of a single extension)
			ClientInfo[] clients;
			lock(_clients)
				clients = _clients.GroupBy(c => c.ExtensionID).Select(g => g.FirstOrDefault()).Where(c => c != null).ToArray();
			foreach(var client in clients)
				if(client.Client.State == CommunicationState.Opened)
				{
					try
					{
						sb.AppendLine(client.Client.GetJavaScriptWrapper());
					}
					catch(Exception ex)
					{
						sb.AppendLine("// ERROR: Unable to generate wrapper for extension " + client.ExtensionID + ": " + ex.Message);
					}
				}
			return sb.ToString();
		}

		public string JsonCall(string extID, string methodName, string[] jsonArgs)
		{
			ClientInfo client;
			lock(_clients)
				client = _clients.FirstOrDefault(c => c.ExtensionID == extID);
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
