using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ServerX.Common;

namespace ServerX
{
	public class NotificationLogBuffer
	{
		private static long _notificationNumber;
		private static List<NotificationLog> _logHistory = new List<NotificationLog>();

		public const int CountThreshold = 10000;
		public static readonly TimeSpan DateThreshold = new TimeSpan(2, 0, 0);

		public static void Add(Guid? procID, string extensionDirName, string extensionId, string extensionName, string logLevel, string logSource, string message)
		{
			lock(_logHistory)
			{
				_logHistory.Add(new NotificationLog(Interlocked.Increment(ref _notificationNumber), procID, extensionDirName, extensionId, extensionName, logLevel, logSource, message));
				var dateThreshold = DateTime.UtcNow.Subtract(DateThreshold);
				_logHistory.RemoveAll(n => n.LogDate < dateThreshold);
				if(_logHistory.Count > CountThreshold)
					_logHistory.RemoveRange(0, _logHistory.Count - CountThreshold);
			}
		}

		public static NotificationLog[] List(long afterLogNumber, int maxToReturn, bool fromStart)
		{
			var list = new List<NotificationLog>();
			lock(_logHistory)
				for(var i = _logHistory.Count - 1; i > 0 && _logHistory[i].LogNumber > afterLogNumber && (fromStart || list.Count < maxToReturn); i--)
					list.Add(_logHistory[i]);
			list.Reverse();
			return list.Take(maxToReturn).ToArray();
		}
	}
}