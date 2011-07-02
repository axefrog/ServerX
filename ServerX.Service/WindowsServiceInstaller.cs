using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration;
using System.Configuration.Install;
using System.ServiceProcess;

namespace ServerX.Service
{
	[RunInstaller(true)]
	public class WindowsServiceProcessInstaller : ServiceProcessInstaller
	{
		public WindowsServiceProcessInstaller()
		{
			Account = ServiceAccount.NetworkService;
		}
	}

	[RunInstaller(true)]
	public class WindowsServiceInstaller : ServiceInstaller
	{
		public WindowsServiceInstaller()
		{
			Description = ConfigurationManager.AppSettings["ServiceDescription"] ?? "ServerX memory-resident service";
			DisplayName = ConfigurationManager.AppSettings["ServiceDisplayName"] ?? "ServerX";
			ServiceName = GetServiceName();
			StartType = ServiceStartMode.Automatic;
		}

		static string GetServiceName()
		{
			return ConfigurationManager.AppSettings["ServiceName"] ?? "ServerX";
		}

		public static bool Install(bool undo, string[] args)
		{
			try
			{
				using(var inst = new AssemblyInstaller(typeof(Program).Assembly, args))
				{
					inst.AfterInstall += OnAfterInstall;
					IDictionary state = new Hashtable();
					inst.UseNewContext = true;
					try
					{
						if(undo)
							inst.Uninstall(state);
						else
						{
							inst.Install(state);
							inst.Commit(state);
						}
					}
					catch
					{
						try
						{
							inst.Rollback(state);
						}
						catch
						{
							return false;
						}
						throw;
					}
				}
			}
			catch(Exception ex)
			{
				Console.WriteLine(ex);
				return false;
			}
			return true;
		}

		static void OnAfterInstall(object sender, InstallEventArgs e)
		{
			var sc = new ServiceController(GetServiceName());
			sc.Start(new string[0]);
		}
	}
}
