using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using Mono.Options;
using ServerX.Common;

namespace ServerX.ServiceManagerCommands
{
	static class CronCommand
	{
		private static OptionSet GetOptions(Options options = null, ServiceManager svc = null)
		{
			return new OptionSet
			{
				{ "status|s=", "Displays status information for all cron jobs", v => options.Subdirectory = v },
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
				Details = new CommandInfo
				{
					Title = "Cron",
					CommandAliases = new [] { "cron" },
					ShortDescription = "Updates or queries the cron scheduler",
					HelpDescription = "Used for interacting with the cron scheduler. You can create, modify or delete existing cron jobs and/or retrieve the status of the existing cron jobs.",
					HelpOptions = GetOptions().WriteOptionDescriptions(),
					HelpUsage = "cron [args]",
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


					return null;
					//if(!result.Success)
					//    return "%!" + result.Message;
					//return "%~" + (result.Message ?? "Restart request issued.");
				}
			};
		}
	}
}
