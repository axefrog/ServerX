using System.ServiceProcess;

namespace ServerX.Service
{
	class Program
	{
		static void Main(string[] args)
		{
			var services = new ServiceBase[] { new WindowsService() };
			ServiceBase.Run(services);
		}
	}
}
