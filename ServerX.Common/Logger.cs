using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ServerX.Common
{
	public class Logger : IDisposable
	{
		private readonly string _name;
		private readonly string _baseDirectory;
		private readonly bool _preventDateSplitting;
		private readonly Logger[] _innerLoggers;
		private readonly Mutex _mutex;
		private string _path;

		public delegate void LogNotificationHandler(string source, string message);
		public event LogNotificationHandler MessageLogged;

		public bool WriteToConsole { get; set; }

		/// <summary>
		/// Constructs a new Logger instance
		/// </summary>
		/// <param name="name">The filename-friendly name of this particular logger</param>
		/// <param name="baseDirectory">An optional alternate location in which the log file should be stored</param>
		/// <param name="preventDateSplitting">Prevents the log from being split into separate files when transitioning from one day to the next</param>
		/// <param name="innerLoggers">Any additional <see cref="Logger" /> objects that should also be written to when logging</param>
		public Logger(string name, string baseDirectory = null, bool preventDateSplitting = false, params Logger[] innerLoggers)
		{
			_mutex = new Mutex(false, "Global.ServerX.Logger." + name.GetHashCode());
			_name = name;
			_baseDirectory = baseDirectory;
			_preventDateSplitting = preventDateSplitting;
			_innerLoggers = innerLoggers.Where(l => l != null).ToArray();
			UpdatePath();
		}

		void UpdatePath()
		{
			try
			{
				var ext = _name.EndsWith(".txt") ? "" : ".log";
				var dirpath = _baseDirectory ?? Path.Combine(ConfigurationManager.AppSettings["DataDirectory"] ?? Environment.CurrentDirectory, "Logs");
				if(_preventDateSplitting)
					_path = Path.Combine(dirpath, _name + ext);
				else
					_path = Path.Combine(dirpath, DateTime.UtcNow.ToString("yyyy-MM-dd"), _name + ext);
				var dir = new FileInfo(_path).Directory;
				using(var mutex = new Mutex(false, "Global.ServerX.Logger." + dir.FullName.GetHashCode()))
				{
					mutex.WaitOne();
					if(!dir.Exists)
						dir.Create();
					mutex.ReleaseMutex();
				}
			}
			catch(UnauthorizedAccessException ex)
			{
				var src = "ServerX";
				var log = "Application";
				if(!EventLog.SourceExists(src))
				    EventLog.CreateEventSource(src, log);
				EventLog.WriteEntry(src, "UnauthorizedAccessException in UpdatePath() in Logger (Account: " + WindowsIdentity.GetCurrent().Name + "): " + ex, EventLogEntryType.Error);
				throw;
			}
		}

		public string LogPath
		{
			get { return _path; }
		}

		static Regex _colorRx = new Regex(@"(\%[\*\@\!\?\~\>\#])");
		DateTime _lastWrite = DateTime.MinValue;
		public void Write(object o, string source = null)
		{
			foreach(var logger in _innerLoggers)
				logger.Write(o);
			if(MessageLogged != null)
			{
				var eventhandler = MessageLogged;
				if(eventhandler != null)
					eventhandler(source, o.ToString());
			}
			o = _colorRx.Replace(o.ToString(), "");
			_mutex.WaitOne();
			if(!_preventDateSplitting && DateTime.UtcNow.Day != _lastWrite.Day)
				UpdatePath();
			try
			{
				if(!string.IsNullOrWhiteSpace(source))
					o = string.Concat("[", source, "] ", o);
				if(DateTime.UtcNow > _lastWrite.AddMinutes(5))
					o = "-------------- " + DateTime.UtcNow + " --------------\r\n" + o;
				if(WriteToConsole)
					Console.Write(o);
				File.AppendAllText(_path, o.ToString());
			}
			catch
			{
			}
			_lastWrite = DateTime.UtcNow;
			_mutex.ReleaseMutex();
		}
		public void WriteLine(object o)
		{
			Write(o + Environment.NewLine);
		}
		public void WriteLine(string source, object o)
		{
			Write(o + Environment.NewLine, source);
		}
		public void WriteLines(params object[] o)
		{
			var sb = new StringBuilder();
			foreach(var k in o)
				sb.AppendLine((k ?? "").ToString());
			Write(sb);
		}

		public void Dispose()
		{
			_mutex.Dispose();
		}
	}
}
