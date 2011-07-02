using System;
using System.ServiceModel;

namespace ServerX.Common
{
	[ServiceContract(Name = "ServiceManager", Namespace = "http://ServerX/ServiceManager", CallbackContract = typeof(IServiceManagerCallback), SessionMode = SessionMode.Allowed)]
	public interface IServiceManagerHost : IServiceManager
	{
		[OperationContract]
		void RegisterClient(Guid id);

		[OperationContract]
		void KeepAlive();
	}
}
