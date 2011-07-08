using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;

namespace ServerX.Common
{
	[ServiceContract]//(CallbackContract = typeof(IServiceCallback), SessionMode = SessionMode.Allowed)]
	public interface IServiceHost
	{
		[OperationContract]
		void RegisterClient(Guid id);

		[OperationContract]
		void KeepAlive();
	}

	public abstract class ServiceCallbackBase
	{
		public void Notify(string message)
		{
			var handler = NotificationReceived;
			if(handler != null)
				handler(message);
		}

		public delegate void NotificationHandler(string message);
		public event NotificationHandler NotificationReceived;
	}
}
