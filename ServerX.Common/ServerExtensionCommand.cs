using System;
using System.Collections.Generic;
using Mono.Options;

namespace ServerX.Common
{
	public interface IServerExtensionCommand : ICommandInfo
	{
		string Execute(ServerExtension ext, string[] args);
	}

	public abstract class ServerExtensionCommand<TExtension, TParameters> : IServerExtensionCommand
		where TExtension : ServerExtension
		where TParameters : class, new()
	{
		public string Execute(ServerExtension ext, string[] args)
		{
			if(!(ext is TExtension))
				throw new Exception("Wrong extension type passed in. Expected " + typeof(TExtension).FullName + " but got " + ext.GetType().FullName);
			return Execute((TExtension)ext, args);
		}

		protected virtual string[] PreParseArguments(TExtension ext, string[] args)
		{
			return args;
		}

		public string Execute(TExtension ext, string[] args)
		{
			var prms = new TParameters();
			var p = GetOptions(prms);
			try
			{
				var unparsedArgs = p.Parse(PreParseArguments(ext, args));
				return Execute(ext, prms, unparsedArgs);
			}
			catch(OptionException ex)
			{
				return string.Concat("%!", ex.Message, "%!");
			}
			catch(Exception ex)
			{
				return string.Concat("%!EXCEPTION: ", ex, "%!");
			}
		}

		public string HelpOptions
		{
			get
			{
				var str = GetOptions(new TParameters()).WriteOptionDescriptions();
				return string.IsNullOrWhiteSpace(str) ? null : str;
			}
		}

		public abstract string Title { get; }
		public abstract string[] CommandAliases { get; }
		public abstract string ShortDescription { get; }
		public abstract string HelpUsage { get; }
		public abstract string HelpDescription { get; }
		public abstract string HelpRemarks { get; }
		protected abstract OptionSet GetOptions(TParameters prms);
		protected abstract string Execute(TExtension ext, TParameters prms, List<string> unparsedArgs);
	}
}
