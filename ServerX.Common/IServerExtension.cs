using System;
using System.ServiceModel;

namespace ServerX.Common
{
	[ServiceContract]
	public interface IServerExtension
	{
		string ID { [OperationContract] get; }
		string CommandID { [OperationContract] get; }
		string Name { [OperationContract] get; }
		string Description { [OperationContract] get; }

		[OperationContract]
		string JsonCall(string name, string data);
		
		[OperationContract]
		string Command(string args);

		bool SupportsCommandLine { [OperationContract] get; }
		bool SupportsJsonCall { [OperationContract] get; }
	}

	[ServiceContract(CallbackContract = typeof(IServerExtensionCallback), SessionMode = SessionMode.Allowed)]
	public interface IServerExtensionHost : IServiceHost, IServerExtension
	{
	}

	public interface IServerExtensionCallback
	{
		[OperationContract(IsOneWay = true)]
		void Notify(string message);
	}
}
