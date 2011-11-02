using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NLog.Config;
using ServerX.Common;
using NLog;

namespace ServerX.ServiceConsole
{
	[Export]
	class Program : IDisposable
	{
		static Logger _logger;
		static void Main(string[] args)
		{
			ConfigurationItemFactory.Default.Targets.RegisterDefinition("ServiceManagerNotification", typeof(ServiceManagerNotificationTarget));
			_logger = LogManager.GetCurrentClassLogger();

			try
			{
				Console.BufferWidth = 120;
				Console.WindowWidth = 120;
			}
			catch
			{
			}
			Environment.CurrentDirectory = ConfigurationManager.AppSettings["DataDirectory"] ?? AppDomain.CurrentDomain.BaseDirectory;
			AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
			TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;

			using(var container = new CompositionContainer(new AggregateCatalog(
				new DirectoryCatalog(AppDomain.CurrentDomain.BaseDirectory, "*.exe"),
				new DirectoryCatalog(AppDomain.CurrentDomain.BaseDirectory, "*.dll"))))
			{
				var prog = container.GetExportedValue<Program>();
				prog._events.NotifyStarting();
				prog.Run(args);	
				prog._events.NotifyClosing();
			}
		}

		static void OnTaskSchedulerUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			_logger.FatalException("UNTRAPPED TASK EXCEPTION", e.Exception);
		}

		static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			_logger.FatalException("UNTRAPPED SERVICE EXCEPTION", (Exception)e.ExceptionObject);
		}

		private Application _app;
		private Application.ConsoleEvents _events;

		[ImportingConstructor]
		public Program([ImportMany] IConsolePlugin[] consolePlugins)
		{
			_events = new Application.ConsoleEvents();
			_app = new Application(consolePlugins, _events);
			_events.Application = _app;
			foreach(var plugin in consolePlugins)
				plugin.Init(_app);
			_app.NotificationReceived += OnNotificationReceived;
			_app.ExtensionNotificationReceived += OnExtensionNotificationReceived;
		}

		void OnNotificationReceived(string logLevel, string source, string message)
		{
			WriteNotification("Service Manager", ConsoleColor.Cyan, logLevel, source, message);
		}

		void OnExtensionNotificationReceived(string extID, string extName, string logLevel, string source, string message)
		{
			WriteNotification(extName, ConsoleColor.Magenta, logLevel, source, message);
		}

		void WriteNotification(string label, ConsoleColor labelColor, string logLevel, string source, string message)
		{
			var level = LogLevel.FromString(logLevel);
			if(!string.IsNullOrWhiteSpace(source))
			{
				var n = source.LastIndexOf('.');
				if(n < source.Length - 1)
					source = source.Substring(n + 1);
				source = string.Concat("%*[", source, "]%* ");
			}
			if(_app.DisplayServerNotifications)
				ColorConsole.WriteLinesLabelled(label, label.Length, labelColor, ColorConsole.GetColor(level), source + message);
		}

		public void Run(string[] args)
		{
			//ColorConsole.WriteLinesLabelled("Test", 4, ConsoleColor.Yellow, ColorConsole.GetColor(LogLevel.Trace), "LogLevel.Trace");
			//ColorConsole.WriteLinesLabelled("Test", 4, ConsoleColor.Yellow, ColorConsole.GetColor(LogLevel.Debug), "LogLevel.Debug");
			//ColorConsole.WriteLinesLabelled("Test", 4, ConsoleColor.Yellow, ColorConsole.GetColor(LogLevel.Info), "LogLevel.Info");
			//ColorConsole.WriteLinesLabelled("Test", 4, ConsoleColor.Yellow, ColorConsole.GetColor(LogLevel.Warn), "LogLevel.Warn");
			//ColorConsole.WriteLinesLabelled("Test", 4, ConsoleColor.Yellow, ColorConsole.GetColor(LogLevel.Error), "LogLevel.Error");
			//ColorConsole.WriteLinesLabelled("Test", 4, ConsoleColor.Yellow, ColorConsole.GetColor(LogLevel.Fatal), "LogLevel.Fatal");

			_events.NotifyReady();
			while(true)
			{
				WritePrompt();
				var input = Console.ReadLine();
				_app.Execute(input);
				if(_app.ExitRequested)
					return;
			}
		}

		void WritePrompt()
		{
			Console.ForegroundColor = ConsoleColor.Gray;
			Console.WriteLine();
			Console.Write("> ");
		}

		void IDisposable.Dispose()
		{
			_app.Dispose();
		}
	}
}
