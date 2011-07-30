using System;
using System.ServiceModel;

namespace ServerX.Common
{
	[ServiceContract(CallbackContract = typeof(IServerExtensionCallback), SessionMode = SessionMode.Allowed)]
	public interface IServerExtension : IServiceXBase
	{
		string ID { [OperationContract] get; }
		string Name { [OperationContract] get; }
		string Description { [OperationContract] get; }
		bool SingleInstanceOnly { [OperationContract] get; }

		[OperationContract]
		string JsonCall(string name, string[] jsonArgs);

		[OperationContract]
		string GetJavaScriptWrapper();

		[OperationContract]
		CommandInfo[] GetCommands();
		
		[OperationContract]
		string Command(string cmdAlias, string[] args);

		void Debug();
	}

	public interface IServerExtensionCallback : IServiceCallbackBase
	{
	}
}
