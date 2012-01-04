using System;

namespace ServerX.Common
{
	public class NotificationLog
	{
		public long LogNumber { get; set; }
		public DateTime LogDate { get; set; }
		public Guid? ProcID { get; set; }
		public string ExtDir { get; set; }
		public string ExtID { get; set; }
		public string ExtName { get; set; }
		public string LogLevel { get; set; }
		public string LogSource { get; set; }
		public string Message { get; set; }

		public NotificationLog()
		{
		}

		public NotificationLog(long logNum, Guid? procID, string extensionDirName, string extensionId, string extensionName, string logLevel, string logSource, string message)
		{
			LogNumber = logNum;
			LogDate = DateTime.UtcNow;
			ProcID = procID;
			ExtDir = extensionDirName;
			ExtID = extensionId;
			ExtName = extensionName;
			LogLevel = logLevel;
			LogSource = logSource;
			Message = message;
		}
	}
}
