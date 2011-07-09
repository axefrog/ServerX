using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Options;
using ServerX.Common;

namespace ServerX
{
	internal class ServiceManagerCommand
	{
		public Command Details { get; set; }
		public CommandExecuteHandler Handler { get; set; }
		public OptionSet Options { get; set; }

		public delegate string CommandExecuteHandler(ServiceManager svc, string[] args);
	}
}
