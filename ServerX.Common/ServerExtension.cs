using System;
using System.Collections.Concurrent;
using System.Linq;
using System.ServiceModel;
using System.Threading;

namespace ServerX.Common
{
	[ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.Single, UseSynchronizationContext = true, IncludeExceptionDetailInFaults = true, AddressFilterMode = AddressFilterMode.Any)]
	public abstract class ServerExtension : IServerExtensionHost
	{
		public abstract string ID { get; }
		public virtual string CommandID { get { return ID; } }
		public abstract string Name { get; }
		public abstract string Description { get; }
		public abstract string JsonCall(string name, string data);
		public abstract string Command(string args);

		public abstract bool SupportsCommandLine { get; }
		public abstract bool SupportsJsonCall { get; }

		internal bool RunCalled { get; private set; }
		public void Run(CancellationTokenSource tokenSource, Logger logger)
		{
			Logger = logger;
			Logger.WriteLine("[" + ID + "] Run() called.");
			RunCalled = true;
			Run(tokenSource);
		}

		public abstract void Run(CancellationTokenSource tokenSource);
		public abstract bool IsRunning { get; }

		/// <summary>
		/// If additional OperationContracts are present, return the WCF contract type of the implemented extension.
		/// </summary>
		public virtual Type ContractType
		{
			get { return typeof(IServerExtensionHost); }
		}

		protected Logger Logger { get; private set; }

		ConcurrentDictionary<Guid, OperationContext> _clients = new ConcurrentDictionary<Guid, OperationContext>();

		public void RegisterClient(Guid id)
		{
			Logger.WriteLine("[" + ID + "] Incoming client registration: " + id);
			if(OperationContext.Current != null)
				_clients.AddOrUpdate(id, OperationContext.Current, (k,v) => OperationContext.Current);
			else
				Logger.WriteLine("[" + ID + "] CLIENT REGISTRATION FAILED (OperationContext is null)");
		}

		public void KeepAlive()
		{
			// this method contains no code - the very act of calling it prevents a connected session from timing out
		}

		public void CallbackEachClient(Action<IServerExtensionCallback> callback)
		{
			lock(_clients)
			{
				foreach(var kvp in _clients.ToList())
				{
					OperationContext ctx;
					if(kvp.Value.Channel.State == CommunicationState.Faulted)
					{
						Logger.WriteLine("[" + ID + "] Can't callback client - state is faulted");
						_clients.TryRemove(kvp.Key, out ctx);
					}
					else if(kvp.Value.Channel.State == CommunicationState.Opened)
					{
						try
						{
							var cb = kvp.Value.GetCallbackChannel<IServerExtensionCallback>();
							callback(cb);
							//Logger.WriteLine("[" + ID + "] Callback sent.");
						}
						catch(Exception ex)
						{
							Logger.WriteLine("[" + ID + "] Can't callback client - " + ex.Message);
							_clients.TryRemove(kvp.Key, out ctx);
						}
					}
				}
			}
		}
	}
}
