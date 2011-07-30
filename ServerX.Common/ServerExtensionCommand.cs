using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Options;
using ServerX.Common;

namespace ServerX
{
	public class ServerExtensionCommand
	{
		public CommandInfo Details { get; set; }
		public CommandExecuteHandler Handler { get; set; }

		public delegate string CommandExecuteHandler(ServerExtension ext, string[] args);
	}
}
