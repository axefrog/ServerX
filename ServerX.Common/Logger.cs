using System;
using System.Configuration;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ServerX.Common
{
	public class Logger : IDisposable
	{
		private readonly string _name;
		private readonly bool _preventDateSplitting;
		private readonly Mutex _mutex;
		private string _path;
		/// <summary>
		/// Constructs a new Logger instance
		/// </summary>
		/// <param name="name">The filename-friendly name of this particular logger</param>
		/// <param name="preventDateSplitting">Prevents the log from being split into separate files when transitioning from one day to the next</param>
		public Logger(string name, bool preventDateSplitting = false)
		{
			_mutex = new Mutex(false, "Global.ServerX.Logger." + name.GetHashCode());
			_name = name;
			_preventDateSplitting = preventDateSplitting;
			UpdatePath();
		}

		void UpdatePath()
		{
			var dirpath = ConfigurationManager.AppSettings["DataDirectory"] ?? Environment.CurrentDirectory;
			_path = Path.Combine(dirpath, "Logs", DateTime.UtcNow.ToString("yyyy-MM-dd"), _name + ".log");
			var dir = new FileInfo(_path).Directory;
			using(var mutex = new Mutex(false, "Global.ServerX.Logger." + dir.FullName.GetHashCode()))
			{
				mutex.WaitOne();
				if(!dir.Exists)
					dir.Create();
				mutex.ReleaseMutex();
			}
		}

		public string LogPath
		{
			get { return _path; }
		}

		static Regex _colorRx = new Regex(@"(\%[\*\@\!\?\~\>\#])");
		DateTime _lastWrite = DateTime.MinValue;
		public void Write(object o)
		{
			o = _colorRx.Replace(o.ToString(), "");
			_mutex.WaitOne();
			if(!_preventDateSplitting && DateTime.UtcNow.Day != _lastWrite.Day)
				UpdatePath();
			try
			{
				if(DateTime.UtcNow > _lastWrite.AddMinutes(5))
					o = "-------------- " + DateTime.UtcNow + " --------------\r\n" + o;
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
