using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Options;
using ServerX.Common;

namespace ServerX.ServiceManagerCommands
{
	static class RestartCommand
	{
		public static ServiceManagerCommand Get()
		{
			var options = new OptionSet
			{
			};

			return new ServiceManagerCommand
			{
				Details = new Command
				{
					Title = "Restart Extension",
					Description = "Reloads and restarts one or more extension processes",
					CommandAliases = new[] { "restart" },
					HelpUsage = "restart [args] {extensionID} {extensionID} ...",
					HelpDescription = null,
					HelpOptions = options.WriteOptionDescriptions(),
					HelpRemarks = null
				},
				Options = options,
				Handler = (svc, args) =>
				{
					return null;
				}
			};
		}
	}
}
