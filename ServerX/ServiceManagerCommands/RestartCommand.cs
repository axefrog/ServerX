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
		private static OptionSet GetOptions(Options options = null, ServiceManager svc = null)
		{
			return new OptionSet
			{
				{ "subdir|s=", "Restricts the extensions to be restarted to a specific extension subdirectory", v => options.Subdirectory = v },
				{ "<>", v => options.ExtensionIDs.Add(v) },
			};
		}

		private class Options
		{
			public string Subdirectory { get; set; }
			public List<string> ExtensionIDs { get; private set; }

			public Options()
			{
				ExtensionIDs = new List<string>();
			}
		}

		public static ServiceManagerCommand Get()
		{
			return new ServiceManagerCommand
			{
				Details = new Command
				{
					Title = "Restart Extension",
					Description = "Reloads and restarts one or more extension processes",
					CommandAliases = new[] { "restart" },
					HelpUsage = "restart [args] {extensionID} {extensionID} ...",
					HelpDescription = null,
					HelpOptions = GetOptions().WriteOptionDescriptions(),
					HelpRemarks = null
				},
				Handler = (svc, args) =>
				{
					var options = new Options();
					var p = GetOptions(options, svc);
					try
					{
						p.Parse(args);
					}
					catch(OptionException ex)
					{
						return "%!" + ex.Message;
					}

					var result = svc.RestartExtensions(options.Subdirectory, options.ExtensionIDs.ToArray());
					if(!result.Success)
						return "%!" + result.Message;
					return "%~" + (result.Message ?? "Restart request issued.");
				}
			};
		}
	}
}
