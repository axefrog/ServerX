using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using NLog.Targets;

namespace ServerX
{
	public class ServiceManagerNotificationTarget : TargetWithLayout
	{
		protected override void Write(LogEventInfo logEvent)
		{
			lock(this)
			{
				var sm = ServiceManager;
				if(sm != null)
					sm.CreateNotification(logEvent.Level.Name, logEvent.LoggerName, Layout.Render(logEvent));
			}
		}

		public static ServiceManager ServiceManager { get; set; }
	}
}
