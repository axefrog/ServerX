using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ServerX.Common;

namespace ServerX.ServiceConsole
{
	public class ConsoleCommand : CommandInfo
	{
		public ConsoleCommandExecuteHandler Handler { get; set; }
	}

	public delegate string ConsoleCommandExecuteHandler(Application app, CommandInfo cmd, string[] args);
}
