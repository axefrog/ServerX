using System;
using System.Configuration;
using System.IO;
using System.ServiceModel;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml;
using ServerX.Common;

namespace ServerX.Service
{
	public class WindowsService : ServiceBase
	{
		public WindowsService()
		{
			Environment.CurrentDirectory = ConfigurationManager.AppSettings["DataDirectory"] ?? AppDomain.CurrentDomain.BaseDirectory;
			_logger = new Logger("windows-service");
			try
			{
				var prmsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServiceParams.txt");
				var prms = new JavaScriptSerializer().Deserialize<ServiceInstallParams>(File.ReadAllText(prmsPath));
				try { File.Delete(prmsPath); }
				catch { }

				AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
				TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;
				_serviceHost = new ServiceHost(typeof(ServiceManager));
				_serviceHost.AddServiceEndpoint(typeof(IServiceManager), new NetTcpBinding("Default"), "net.tcp://localhost:" + prms.Port + "/ServiceManager");
				_serviceHost.Opening += OnServiceHostOpening;
				_serviceHost.Opened += OnServiceHostOpened;
				_serviceHost.Faulted += OnServiceHostFaulted;
				_serviceHost.Closing += OnServiceHostClosing;
				_serviceHost.Closed += OnServiceHostClosed;
				_serviceHost.UnknownMessageReceived += OnServiceHostUnknownMessageReceived;
			}
			catch(Exception ex)
			{
				_logger.WriteLine(ex.ToString());
			}
		}

		protected override void Dispose(bool disposing)
		{
			GC.Collect();
			base.Dispose(disposing);
		}

		private ServiceHost _serviceHost;

		Logger _logger;
		protected override void OnStart(string[] args)
		{
			_logger.Write("Service starting...\r\n");
			try
			{
				_serviceHost.Open();
			}
			catch(Exception ex)
			{
				_logger.Write(ex.ToString());
				throw;
			}
			_logger.Write("Service started\r\n");
		}

		void OnTaskSchedulerUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			_logger.Write("UNTRAPPED TASK EXCEPTION:\r\n" + e.Exception);
		}

		void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			_logger.Write("UNTRAPPED SERVICE EXCEPTION:\r\n" + e.ExceptionObject);
		}

		void OnServiceHostUnknownMessageReceived(object sender, UnknownMessageReceivedEventArgs e)
		{
			var writer = new StringWriter();
			var dict = XmlDictionaryWriter.CreateDictionaryWriter(XmlWriter.Create(writer));
			e.Message.WriteMessage(dict);
			_logger.Write("UNKNOWN MESSAGE RECEIVED:" + Environment.NewLine + writer + Environment.NewLine);
		}

		void OnServiceHostOpened(object sender, EventArgs e)
		{
			_logger.Write("Service host opened." + Environment.NewLine);
		}

		void OnServiceHostOpening(object sender, EventArgs e)
		{
			_logger.Write("Service host opening... " + Environment.NewLine);
		}

		void OnServiceHostFaulted(object sender, EventArgs e)
		{
			_logger.Write("Service host faulted!" + Environment.NewLine);
		}

		void OnServiceHostClosing(object sender, EventArgs e)
		{
			_logger.Write("Service host closing..." + Environment.NewLine);
		}

		void OnServiceHostClosed(object sender, EventArgs e)
		{
			_logger.Write("Service host closed." + Environment.NewLine);
		}

		protected override void OnStop()
		{
			_logger.Write("Service stopping...\r\n");
			try
			{
				_serviceHost.Close();
				_serviceHost = null;
			}
			catch(Exception ex)
			{
				_logger.Write(ex.ToString());
			}
			_logger.Write("Service stopped\r\n");
		}
	}
}