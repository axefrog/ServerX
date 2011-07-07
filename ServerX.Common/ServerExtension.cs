using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;

namespace ServerX.Common
{
	[ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.Single, UseSynchronizationContext = true, IncludeExceptionDetailInFaults = true)]
	public abstract class ServerExtension : IServerExtensionHost
	{
		public abstract string ID { get; }
		public abstract string Name { get; }
		public abstract string Description { get; }
		public abstract string JsonCall(string name, string data);
		public abstract string Command(string name, string[] args);
		public abstract void Run(CancellationTokenSource tokenSource, Logger logger);
		public abstract bool IsRunning { get; }

		public virtual Type ContractType
		{
			get { return typeof(IServerExtensionHost); }
		}

		ConcurrentDictionary<Guid, OperationContext> _clients = new ConcurrentDictionary<Guid, OperationContext>();

		public void RegisterClient(Guid id)
		{
			if(OperationContext.Current != null)
				_clients.AddOrUpdate(id, OperationContext.Current, (k,v) => OperationContext.Current);
		}

		public void KeepAlive()
		{
			// this method contains no code - the very act of calling it prevents a connected session from timing out
		}
	}
}
