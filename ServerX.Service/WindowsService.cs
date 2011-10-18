using System;
using System.Configuration;
using System.IO;
using System.ServiceModel;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml;
using NLog;
using ServerX.Common;

namespace ServerX.Service
{
	public class WindowsService : ServiceBase
	{
		public WindowsService()
		{
			Environment.CurrentDirectory = ConfigurationManager.AppSettings["DataDirectory"] ?? AppDomain.CurrentDomain.BaseDirectory;
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
				_logger.ErrorException("An exception was thrown while constructing the windows service class", ex);
			}
		}

		protected override void Dispose(bool disposing)
		{
			GC.Collect();
			base.Dispose(disposing);
		}

		private ServiceHost _serviceHost;

		Logger _logger = LogManager.GetCurrentClassLogger();
		protected override void OnStart(string[] args)
		{
			_logger.Info("Service starting...\r\n");
			try
			{
				_serviceHost.Open();
			}
			catch(Exception ex)
			{
				_logger.Info(ex.ToString());
				throw;
			}
			_logger.Info("Service started\r\n");
		}

		void OnTaskSchedulerUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			_logger.FatalException("UNTRAPPED TASK EXCEPTION", e.Exception);
		}

		void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			_logger.FatalException("UNTRAPPED SERVICE EXCEPTION", (Exception)e.ExceptionObject);
		}

		void OnServiceHostUnknownMessageReceived(object sender, UnknownMessageReceivedEventArgs e)
		{
			var writer = new StringWriter();
			var dict = XmlDictionaryWriter.CreateDictionaryWriter(XmlWriter.Create(writer));
			e.Message.WriteMessage(dict);
			_logger.Warn("UNKNOWN MESSAGE RECEIVED: {0}", writer);
		}

		void OnServiceHostOpened(object sender, EventArgs e)
		{
			_logger.Info("Service host opened.");
		}

		void OnServiceHostOpening(object sender, EventArgs e)
		{
			_logger.Info("Service host opening... ");
		}

		void OnServiceHostFaulted(object sender, EventArgs e)
		{
			_logger.Info("Service host faulted!");
		}

		void OnServiceHostClosing(object sender, EventArgs e)
		{
			_logger.Info("Service host closing...");
		}

		void OnServiceHostClosed(object sender, EventArgs e)
		{
			_logger.Info("Service host closed.");
		}

		protected override void OnStop()
		{
			_logger.Info("Service stopping...");
			try
			{
				_serviceHost.Close();
				_serviceHost = null;
			}
			catch(Exception ex)
			{
				_logger.ErrorException("An exception was thrown while closing the service host", ex);
			}
			_logger.Info("Service stopped");
		}
	}
}