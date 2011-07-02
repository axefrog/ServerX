using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ServerX.Common
{
	public class ServerCommand : Command
	{
		public ServerCommandExecuteHandler Handler { get; set; }
	}

	public delegate string ServerCommandExecuteHandler(IServiceManager svc, ServerCommand cmd, string[] args);
}
