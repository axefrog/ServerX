using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using Mono.Options;
using Server.ClientConsole;

namespace ServerX.ServiceConsole.Plugins
{
	[Export(typeof(IConsolePlugin))]
	public class CorePlugin : IConsolePlugin
	{
		public string Name
		{
			get { return "Core"; }
		}

		public string Description
		{
			get { return "Contains the core console functionality"; }
		}

		public void Init(Application application)
		{
			application.RegisterCommand(new ConsoleCommand
			{
				Title = "Quit",
				CommandAliases = new [] { "quit", "exit", "e", "q" },
				Description = "Exits the console",
				HelpDescription = "Exits the console. If a windows service is running, it will stay running and can be shut down by starting up the console again and calling %@svc stop%@.",
				Handler = (app, command, args) =>
				{
					app.ExitRequested = true;
					return null;
				}
			});

			application.RegisterCommand(new ConsoleCommand
			{
				Title = "Help",
				CommandAliases = new [] { "help", "h", "?" },
				Description = "Displays helpful information",
				HelpUsage = "help [command]",
				HelpDescription = "Displays the help text for a given command. If no command is specified, general help is displayed.",
				Handler = (app, command, args) =>
				{
					switch(args.Length)
					{
						case 0:
							return "\r\nType %@list%@ for a list of commands. You can abbreviate any command by typing one or more of the first characters "
									+ "of that command as long as those characters are not the start of more than one command. The server application can "
									+ "run commands directly from the command line by executing the application in the form %@server.exe [args]%@, where "
									+ "args is the command name followed by any command arguments.";

						case 1:
							ConsoleCommand cmd;
							if(app.CommandsByAlias.TryGetValue(args[0].ToLower(), out cmd))
							{
								var sb = new StringBuilder();
								sb.AppendLine().AppendFormat("%*** {0} **%*", cmd.Title).AppendLine();
								if(!string.IsNullOrWhiteSpace(cmd.HelpDescription))
									sb.AppendLine().AppendLine("%?Description:%?").Append("    %>").AppendLine(cmd.HelpDescription.TrimEnd());
								else if(!string.IsNullOrWhiteSpace(cmd.Description))
									sb.AppendLine(cmd.Description.TrimEnd());
								sb.AppendLine().AppendLine("%?Aliases%?").Append("    ").AppendLine(cmd.CommandAliases.Concat(", ", s => string.Concat("%@", s, "%@")));
								if(!string.IsNullOrWhiteSpace(cmd.HelpUsage))
									sb.AppendLine().AppendLine("%?Usage:%?").Append("    %>%@").Append(cmd.HelpUsage.TrimEnd()).AppendLine("%@");
								if(!string.IsNullOrWhiteSpace(cmd.HelpOptions))
									sb.AppendLine().AppendLine("%?Options:%?").AppendLine(cmd.HelpOptions.TrimEnd());
								if(!string.IsNullOrWhiteSpace(cmd.HelpRemarks))
									sb.AppendLine().AppendLine(cmd.HelpRemarks.TrimEnd());
								return sb.ToString();
							}
							return "%!A command handler for %@" + args[0] + "%@ was not found.";
		
						default:
							return "%!" + command.HelpDescription;
					}
				}
			});

			application.RegisterCommand(new ConsoleCommand
			{
				Title = "List Commands",
				CommandAliases = new [] { "list", "l" },
				Description = "Lists all commands and their descriptions",
				HelpDescription = "Displays a list of available commands. This command has no arguments.",
				Handler = (app, command, args) =>
				{
					var cmdlen = app.CommandsByTitle.Values.Max(c => c.CommandAliases.First().Length);

					Console.WriteLine();
					ColorConsole.WriteLine("Console Commands:", ConsoleColor.White);
					Console.WriteLine();
					foreach(var cmd in app.CommandsByTitle.Values.OrderBy(p => p.CommandAliases.First()))
						ColorConsole.WriteLinesLabelled(cmd.CommandAliases.First(), cmdlen, ConsoleColor.Yellow, cmd.Description);

					Console.WriteLine();
					ColorConsole.WriteLine("%?Type %@help [command]%@ for help on specific commands%?");

					return null;
				}
			});

			application.RegisterCommand(new ConsoleCommand
			{
				Title = "List Console Plugins",
				CommandAliases = new [] { "consoleplugins", "cplugins", "cp" },
				Description = "Displays a list of console plugins",
				HelpDescription = "Displays a list of active console plugins. This command has no arguments.",
				Handler = (app, command, args) =>
				{
					var cmdlen = app.Plugins.Values.Max(c => c.Name.Length);

					Console.WriteLine();
					ColorConsole.WriteLine("Active console plugins:", ConsoleColor.White);
					Console.WriteLine();
					foreach(var cmd in app.Plugins.Values.OrderBy(p => p.Name))
						ColorConsole.WriteLinesLabelled(cmd.Name, cmdlen, ConsoleColor.Yellow, cmd.Description);

					return null;
				}
			});

			application.RegisterCommand(new ConsoleCommand
			{
				Title = "Create/Set Macro",
				CommandAliases = new [] { "#set" },
				Description = "Creates or updates a command macro",
				HelpUsage = "#set [name] [command and args...]",
				HelpDescription = "Creating a macro allows you to use a brief string as shorthand for a console command and its arguments. No spaces allowed.",
				HelpRemarks = "To run the macro, precede with an exclamation mark; e.g. !foo",
				Handler = (app, command, args) =>
				{
					if(args.Length <= 1 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
						return "%!Must specify a macro name followed by a command and zero or more arguments.";
					app.AddMacro(args[0], args.Skip(1).Concat(" "));
					return "%~Macro set. To run, enter %@!" + args[0].ToLower() + "%@.";
				}
			});

			application.RegisterCommand(new ConsoleCommand
			{
				Title = "List Macros",
				CommandAliases = new [] { "#list" },
				Description = "List all existing macros",
				HelpDescription = "Lists all existing macros. This command has no arguments.",
				Handler = (app, command, args) =>
				{
					//foreach(var m in app.ListMacros())
					return null;
				}
			});

			application.ConsoleReady += app =>
			{
				const string title = "Service Manager Console";
				ColorConsole.WriteLine(title, ConsoleColor.White);
				ColorConsole.WriteLine(new string('-', title.Length), ConsoleColor.White);
				Console.WriteLine();
				ColorConsole.WriteLine("%?Type %@list%@ for a list of available commands%?");
				ColorConsole.WriteLine("%?Type %@help [command]%@ for help on specific commands%?");
			};
		}
	}
}
