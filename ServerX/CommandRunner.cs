using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Options;
using ServerX.Common;
using ServerX.ServiceManagerCommands;

namespace ServerX
{
	internal class CommandRunner
	{
		private readonly ServiceManager _svc;
		private readonly ExtensionClientManager _extmgr;
		private readonly Dictionary<string, ServiceManagerCommand> _commandsByAlias = new Dictionary<string, ServiceManagerCommand>();
		private readonly List<ServiceManagerCommand> _commands = new List<ServiceManagerCommand>();

		public CommandRunner(ServiceManager svc, ExtensionClientManager extmgr)
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
				_commandsByAlias.Add(alias.ToLower(), cmd);
		}

		private static readonly Regex RxCommandName = new Regex(@"^((?<extid>[^:.]{1,255})(\:(?<extnum>[0-9]{1,3})?)?\.)?(?<cmdalias>[^:.]{1,255})$", RegexOptions.ExplicitCapture);
		private bool ParseCommandName(string cmd, out string extid, out int extnum, out string cmdalias)
		{
			cmd = cmd.ToLower();
			var match = RxCommandName.Match(cmd);
			if(match.Success)
			{
				extid = match.Groups["extid"].Success ? match.Groups["extid"].Value : null;
				extnum = match.Groups["extnum"].Success ? int.Parse(match.Groups["extnum"].Value) : 1;
				cmdalias = match.Groups["cmdalias"].Success ? match.Groups["cmdalias"].Value : null;
				return true;
			}
			extid = null;
			extnum = 1;
			cmdalias = null;
			return false;
		}

		public string Execute(string cmd, string[] args)
		{
			string extid, cmdalias;
			int extnum;
			if(ParseCommandName(cmd, out extid, out extnum, out cmdalias))
			{
				if(extid == null)
				{
					ServiceManagerCommand smc;
					if(_commandsByAlias.TryGetValue(cmd, out smc))
						return smc.Handler(_svc, args);
				}

				return _extmgr.ExecuteCommand(extid, extnum, cmdalias, args);
			}
			return new[] {
				"%!Invalid command format. Valid formats are:%!",
				"    %@[cmdalias]%@",
				"    %@[extensionID].[cmdalias]%@",
				"    %@[extensionID]:[instanceNumber].[cmdalias]%@",
				"",
				"For example, to run %@search%@ on \"DomainIndex\", instance #2:",
				"    %#domainindex:2.search [args]%#"
			}.Concat(Environment.NewLine);
		}

		public CommandInfo[] ListCommands()
		{
			return _commands.Select(c => c.Details).ToArray();
		}

		public CommandInfo GetCommandInfo(string cmd)
		{
			string extid, cmdalias;
			int extnum;
			if(!ParseCommandName(cmd, out extid, out extnum, out cmdalias))
				return null;
			if(extid == null)
			{
				ServiceManagerCommand smc;
				if(_commandsByAlias.TryGetValue(cmd, out smc))
					return smc.Details;
			}
			return _extmgr.GetCommandInfo(extid, extnum, cmdalias);
		}
	}
}