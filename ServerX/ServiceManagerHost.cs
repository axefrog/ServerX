﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using ServerX.Common;

namespace ServerX
{
	public class ServiceManagerHost : IServiceManagerHost
	{
		ServiceManager _service;
		Dictionary<Guid, OperationContext> _clients = new Dictionary<Guid, OperationContext>();
		Logger _exlogger = new Logger("servicemanager-exceptions");

		public ServiceManagerHost()
		{
			_service = new ServiceManager();
		}

		void HandleException(Exception ex)
		{
			_exlogger.WriteLines("ServiceManager Exception:", ex, Environment.NewLine);
		}

		void CallbackEachClient(Action<IServiceManagerCallback> callback)
		{
			lock(_clients)
			{
				foreach(var kvp in _clients.ToList())
				{
					if(kvp.Value.Channel.State == CommunicationState.Faulted)
						_clients.Remove(kvp.Key);
					else if(kvp.Value.Channel.State == CommunicationState.Opened)
					{
						try
						{
							var cb = kvp.Value.GetCallbackChannel<IServiceManagerCallback>();
							callback(cb);
						}
						catch
						{
							_clients.Remove(kvp.Key);
						}
					}
				}
			}
		}

		public void RegisterClient(Guid id)
		{
			try
			{
				lock(_clients)
				{
					if(OperationContext.Current != null)
					{
						if(!_clients.ContainsKey(id))
							_clients.Add(id, OperationContext.Current);
					}
				}
			}
			catch(Exception ex)
			{
				HandleException(ex);
			}
		}

		public void KeepAlive()
		{

		}

		public DateTime GetServerTime()
		{
			return _service.GetServerTime();
		}

		public Result SetExtensionDirectoryIncluded(string name, bool include)
		{
			return _service.SetExtensionDirectoryIncluded(name, include);
		}

		public Result SetExtensionsEnabledInDirectory(string name, bool enabled)
		{
			return _service.SetExtensionsEnabledInDirectory(name, enabled);
		}

		public Result SetExtensionEnabled(string name, bool enabled)
		{
			return _service.SetExtensionEnabled(name, enabled);
		}

		public Result RestartExtension(string name)
		{
			return _service.RestartExtension(name);
		}

		public Result RestartExtensionsInDirectory(string name)
		{
			return _service.RestartExtensionsInDirectory(name);
		}

		public string[] ListExtensionDirectories()
		{
			return _service.ListExtensionDirectories();
		}

		public string[] ListIncludedExtensionDirectories()
		{
			return _service.ListIncludedExtensionDirectories();
		}

		public ExtensionInfo[] ListAvailableExtensions()
		{
			return _service.ListAvailableExtensions();
		}

		public ExtensionInfo[] ListExtensionsInDirectory(string name)
		{
			return _service.ListExtensionsInDirectory(name);
		}

		public string ExecuteCommand(string command)
		{
			return _service.ExecuteCommand(command);
		}

		public CommandInfo ListCommands()
		{
			return _service.ListCommands();
		}

		public string GetCommandHelp(string command)
		{
			return _service.GetCommandHelp(command);
		}

		public ScriptInfo[] ListScripts()
		{
			return _service.ListScripts();
		}

		public string ExecuteScript(string name)
		{
			return _service.ExecuteScript(name);
		}

		public ScriptInfo GetScript(string name)
		{
			return _service.GetScript(name);
		}

		public string SaveScript(ScriptInfo script)
		{
			return _service.SaveScript(script);
		}

		public string DeleteScript(string name)
		{
			return _service.DeleteScript(name);
		}

		public ScheduledCommand[] ListScheduledCommands()
		{
			return _service.ListScheduledCommands();
		}

		public ScheduledCommand AddScheduledCommand(string cron, string command)
		{
			return _service.AddScheduledCommand(cron, command);
		}

		public Result DeleteScheduledCommand(int id)
		{
			return _service.DeleteScheduledCommand(id);
		}

		public void KeepExtensionProcessAlive(Guid id)
		{
			_service.KeepExtensionProcessAlive(id);
		}

		public void Dispose()
		{
			_service.Dispose();
			_service = null;
		}
	}
}
