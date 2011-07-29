using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;

namespace ServerX.Common
{
	[ServiceContract(CallbackContract = typeof(IServiceCallbackBase), SessionMode = SessionMode.Allowed)]
	public interface IServiceXBase
	{
		[OperationContract]
		void RegisterClient(Guid id);

		[OperationContract]
		void KeepAlive();
	}

	public abstract class ServiceXBase
	{
		Dictionary<Guid, OperationContext> _clients = new Dictionary<Guid, OperationContext>();
		public void CallbackEachClient<TCallback>(Action<TCallback> callback)
			where TCallback : IServiceCallbackBase
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
							var cb = kvp.Value.GetCallbackChannel<TCallback>();
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
			lock(_clients)
			{
				if(OperationContext.Current != null)
				{
					if(!_clients.ContainsKey(id))
						_clients.Add(id, OperationContext.Current);
					OnRegisterClient(id);
				}
			}
		}
		
		protected virtual void OnRegisterClient(Guid id)
		{
		}

		public void KeepAlive()
		{
		}
	}

	public interface IServiceCallbackBase
	{
		[OperationContract(IsOneWay = true)]
		void Notify(string source, string message, LogLevel level);
	}

	public abstract class ServiceCallbackBase : IServiceCallbackBase
	{
		public virtual void Notify(string source, string message, LogLevel level)
		{
			var handler = NotificationReceived;
			if(handler != null)
				handler(source, message, level);
		}

		public delegate void NotificationHandler(string source, string message, LogLevel level);
		public event NotificationHandler NotificationReceived;
	}

	public enum LogLevel
	{
		Normal = 0,
		/// <summary>
		/// Extended log messages are not shown in the console under normal circumstances
		/// </summary>
		Extended = 1
	}
}
