using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Text;
using System.Threading;

namespace ServerX.Common
{
	[ServiceContract]
	public interface IServerExtension
	{
		string ID { [OperationContract] get; }
		string Name { [OperationContract] get; }
		string Description { [OperationContract] get; }

		[OperationContract]
		string JsonCall(string name, string data);
		
		[OperationContract]
		string Command(string name, string[] args);
	}

	[ServiceContract]
	public interface IServerExtensionHost : IServerExtension
	{
		[OperationContract]
		void RegisterClient(Guid id);

		[OperationContract]
		void KeepAlive();
	}
}
