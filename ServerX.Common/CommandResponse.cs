using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace ServerX.Common
{
	[DataContract]
	public class CommandResponse
	{
		public string Result { get; set; }
		public Disposition Disposition { get; set; }
	}

	public enum Disposition
	{
		Neutral = 0,
		Positive = 1,
		Negative = 2
	}
}
