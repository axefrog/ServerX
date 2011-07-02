using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace ServerX.Common
{
	[DataContract]
	public class ScheduledCommand
	{
		[DataMember]
		public int ID { get; set; }

		[DataMember]
		public string Cron { get; set; }

		[DataMember]
		public string Command { get; set; }
	}
}
