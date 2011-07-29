using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ServerX.Common;

namespace ServerX.ServiceConsole
{
	[Export]
	class Program : IDisposable
	{
		static Logger _logger = new Logger("console-exceptions");
		static void Main(string[] args)
		{
			Console.BufferWidth = 120;
			Console.WindowWidth = 120;
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
			_logger.Write("UNTRAPPED TASK EXCEPTION:\r\n" + e.Exception);
		}

		static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			_logger.Write("UNTRAPPED SERVICE EXCEPTION:\r\n" + e.ExceptionObject);
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
			//_app.MessagesReceived += OnMessagesReceived;
			//_app.StatusChanged += OnStatusChanged;
			//_app.DisplayServerNotificationsChanged += display =>{ if(display) FlushMessageBuffer(); };
		}

		void OnNotificationReceived(string source, string message, LogLevel level)
		{
			if(_app.DisplayServerNotifications && level == LogLevel.Normal)
				ColorConsole.WriteLinesLabelled("Service Manager", 15, ConsoleColor.Cyan, (!string.IsNullOrWhiteSpace(source) ? "%*[" + source + "]%* " : "") + message);
		}

		void OnExtensionNotificationReceived(string extID, string extName, string source, string message, LogLevel level)
		{
			if(_app.DisplayServerNotifications && level == LogLevel.Normal)
				ColorConsole.WriteLinesLabelled(extName, extName.Length, ConsoleColor.Magenta, (!string.IsNullOrWhiteSpace(source) ? "%*[" + source + "]%* " : "") + message);
		}

		//void OnStatusChanged(string plugin, string status)
		//{
		//    OnMessagesReceived(LogMode.Normal, plugin, new[] { "%?Status changed:%? " + status });
		//}

		//class BufferedMessages
		//{
		//    public LogMode Mode { get; set; }
		//    public string[] Messages { get; set; }
		//    public string Plugin { get; set; }
		//}
		//LinkedList<BufferedMessages> _msgBuffer = new LinkedList<BufferedMessages>();
		//void OnMessagesReceived(LogMode mode, string plugin, string[] messages)
		//{
		//    lock(_msgBuffer)
		//    {
		//        if(_msgBuffer.Count > 3)
		//            _msgBuffer.RemoveFirst();
		//        _msgBuffer.AddLast(new BufferedMessages { Mode = mode, Messages = messages, Plugin = plugin });
		//        if(_app.DisplayServerNotifications)
		//            FlushMessageBuffer();
		//    }
		//}
		//void FlushMessageBuffer()
		//{
		//    lock(_msgBuffer)
		//        if(_msgBuffer.Count > 0)
		//            while(_msgBuffer.Count > 0)
		//            {
		//                var m = _msgBuffer.First.Value;
		//                if((_app.PluginsToWatch.Count == 0 || _app.PluginsToWatch.Contains(m.Plugin)) && m.Mode <= _app.WatchMode)
		//                    foreach(var msg in m.Messages)
		//                        WriteBufferedMessage(m.Plugin, msg);
		//                _msgBuffer.RemoveFirst();
		//            }
		//}
		//private string _lastPluginFlushed;
		//void WriteBufferedMessage(string plugin, string message)
		//{
		//    var name = plugin;
		//    if(name == _lastPluginFlushed)
		//        name = "";
		//    else
		//        _lastPluginFlushed = name;
		//    if(_app.PluginsToWatch.Count == 1)
		//        ColorConsole.WriteLines(message);
		//    else
		//        ColorConsole.WriteLinesLabelled(name, _app.MaxCommandLength, ConsoleColor.Magenta, message);
		//}

		public void Run(string[] args)
		{
			//if(args.Length > 0)
			//    _app.Execute("run " + args[0]);
			//else
			//	ColorConsole.WriteLine("%?Type %@help%@ to get started.%?");
			//match all arguments including quoted arguments, with quotes escapeable with a backslash

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
