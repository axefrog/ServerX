using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Options;
using ServerX.Common;

namespace ServerX
{
	public class CommandRunner
	{
		private readonly ServiceManager _svc;
		private readonly Dictionary<string, ServerCommand> _commandsByAlias = new Dictionary<string, ServerCommand>();
		private readonly Dictionary<string, ServerCommand> _commandsByTitle = new Dictionary<string, ServerCommand>();

		public CommandRunner(ServiceManager svc)
		{
			_svc = svc;
		}

		OptionSet GetCommandOptions(string cmd)
		{
			switch(cmd)
			{
				case "":

				default:
					return null;
			}
		}

		public bool Execute(string command, out string response)
		{
			response = null;

			if(string.IsNullOrWhiteSpace(command))
				return true;

			var arr = command.Split(new[] { ' ', '\t', '\r', '\n', '\f' }, StringSplitOptions.RemoveEmptyEntries);
			var args = arr.Length > 1 ? arr.Skip(1).ToArray() : new string[0];
			var cmd = args[0].ToLower();

			switch(cmd)
			{
				case "cplugins":
				case "script":
				//case "listplugindirs":
					break;
			}
			return false;
		}


	}
}