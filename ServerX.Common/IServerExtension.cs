using System;
using System.ServiceModel;

namespace ServerX.Common
{
	[ServiceContract(CallbackContract = typeof(IServerExtensionCallback), SessionMode = SessionMode.Allowed)]
	public interface IServerExtension : IServiceXBase
	{
		string ID { [OperationContract] get; }
		string CommandID { [OperationContract] get; }
		string Name { [OperationContract] get; }
		string Description { [OperationContract] get; }

		[OperationContract]
		string JsonCall(string name, string[] jsonArgs);

		[OperationContract]
		string GetJavaScriptWrapper();
		
		[OperationContract]
		string Command(string[] args);

		bool SupportsCommandLine { [OperationContract] get; }

		void Debug();
	}

	public interface IServerExtensionCallback : IServiceCallbackBase
	{
	}
}
