using System.ServiceModel;

namespace ServerX.Common
{
	public interface IServiceManagerCallback
	{
		[OperationContract(IsOneWay = true)]
		void SendMessage(string message);
	}
}
