using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Options;
using ServerX.Common;
using ServerX.ServiceManagerCommands;

namespace ServerX
{
	internal class CommandRunner
	{
		private readonly ServiceManager _svc;
		private readonly ServerExtensionClientManager _extmgr;
		private readonly Dictionary<string, ServiceManagerCommand> _commandsByAlias = new Dictionary<string, ServiceManagerCommand>();
		private readonly List<ServiceManagerCommand> _commands = new List<ServiceManagerCommand>();

		public CommandRunner(ServiceManager svc, ServerExtensionClientManager extmgr)
		{
			_svc = svc;
			_extmgr = extmgr;
			Add(ExecCommand.Get());
			Add(RestartCommand.Get());
		}

		void Add(ServiceManagerCommand cmd)
		{
			_commands.Add(cmd);
			foreach(var alias in cmd.Details.CommandAliases)
				_commandsByAlias.Add(alias, cmd);
		}

		public string Execute(string cmd, string[] args)
		{
			ServiceManagerCommand smc;
			if(!_commandsByAlias.TryGetValue(cmd, out smc))
			{
				if(_extmgr.IsCommandAvailable(cmd))
					return _extmgr.ExecuteCommand(cmd, args);
				return "%!Unrecognized command: " + cmd;
			}

			return smc.Handler(_svc, args);
		}

		public Command[] ListCommands()
		{
			return _commands.Select(c => c.Details).ToArray();
		}
	}
}