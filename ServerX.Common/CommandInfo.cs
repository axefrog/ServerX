using System.Runtime.Serialization;

namespace ServerX.Common
{
	public interface ICommandInfo
	{
		/// <summary>
		/// A descriptive title for the command (e.g. "List Commands") - must be unique
		/// </summary>
		string Title { get; }

		/// <summary>
		/// A list of strings (case insensitive, no spaces) that can be used to invoke the command
		/// </summary>
		string[] CommandAliases { get; }

		/// <summary>
		/// A short sentence for the command list, describing the command
		/// </summary>
		string ShortDescription { get; }

		/// <summary>
		/// A one-line string indicating the format in which to call the command e.g. "mycmd [args...] {A|B|C}"
		/// </summary>
		string HelpUsage { get; }

		/// <summary>
		/// A list of optional arguments allowed
		/// </summary>
		string HelpOptions { get; }

		/// <summary>
		/// The description that comes immediately after the command title. If null, <see cref="ShortDescription"/> will be used.
		/// </summary>
		string HelpDescription { get; }

		/// <summary>
		/// Further help text displayed below the other help text for elaboration and clarification
		/// </summary>
		string HelpRemarks { get; }
	}

	[DataContract]
	public class CommandInfo : ICommandInfo
	{
		/// <summary>
		/// A descriptive title for the command (e.g. "List Commands") - must be unique
		/// </summary>
		[DataMember]
		public virtual string Title { get; set; }
		/// <summary>
		/// A list of strings (case insensitive, no spaces) that can be used to invoke the command
		/// </summary>
		[DataMember]
		public virtual string[] CommandAliases { get; set; }
		/// <summary>
		/// A short sentence for the command list, describing the command
		/// </summary>
		[DataMember]
		public virtual string ShortDescription { get; set; }
		/// <summary>
		/// A one-line string indicating the format in which to call the command e.g. "mycmd [args...] {A|B|C}"
		/// </summary>
		[DataMember]
		public virtual string HelpUsage { get; set; }
		/// <summary>
		/// A list of optional arguments allowed
		/// </summary>
		[DataMember]
		public virtual string HelpOptions { get; set; }
		/// <summary>
		/// The description that comes immediately after the command title. If null, <see cref="ShortDescription"/> will be used.
		/// </summary>
		[DataMember]
		public virtual string HelpDescription { get; set; }
		/// <summary>
		/// Further help text displayed below the other help text for elaboration and clarification
		/// </summary>
		[DataMember]
		public virtual string HelpRemarks { get; set; }

		public CommandInfo()
		{
		}

		public CommandInfo(ICommandInfo cmd)
		{
			Title = cmd.Title;
			CommandAliases = cmd.CommandAliases;
			ShortDescription = cmd.ShortDescription;
			HelpDescription = cmd.HelpDescription;
			HelpUsage = cmd.HelpUsage;
			HelpOptions = cmd.HelpOptions;
			HelpRemarks = cmd.HelpRemarks;
		}
	}
}