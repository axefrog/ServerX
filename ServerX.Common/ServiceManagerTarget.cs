using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using NLog.Targets;

namespace ServerX.Common
{
	public class ServiceManagerTarget : TargetWithLayout
	{
		protected override void Write(LogEventInfo logEvent)
		{
			lock(this)
			{
				var ext = Extension;
				if(ext != null)
					ext.Notify(logEvent.Level, logEvent.LoggerName, Layout.Render(logEvent));
			}
		}

		public static ServerExtension Extension { get; set; }
	}
}
