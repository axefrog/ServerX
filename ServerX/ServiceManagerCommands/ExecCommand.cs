using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using Mono.Options;
using ServerX.Common;

namespace ServerX.ServiceManagerCommands
{
	static class ExecCommand
	{
		public static ServiceManagerCommand Get()
		{
			return new ServiceManagerCommand
			{
				Details = new CommandInfo
				{
					Title = "Execute Script",
					CommandAliases = new [] { "exec" },
					ShortDescription = "Executes a script stored on the server",
					HelpDescription = "Executes a script which has been stored on the server in the Scripts folder.",
					HelpUsage = "exec {filename}",
				},
				Handler = (svc, args) =>
				{
					if(args.Length != 1)
						return "%!You must specify the name of a script file (e.g. %@exec myscript.js%@)";
					var result = svc.ExecuteScriptFile(args[0]);
					return (result.Success ? "%~" : "%!") + (result.Message ?? "Done.");
				}
			};
		}
	}
}
