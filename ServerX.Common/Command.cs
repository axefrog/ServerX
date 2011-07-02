using System.Runtime.Serialization;

namespace ServerX.Common
{
	[DataContract]
	public class Command
	{
		/// <summary>
		/// A descriptive title for the command (e.g. "List Commands") - must be unique
		/// </summary>
		[DataMember]
		public string Title { get; set; }
		/// <summary>
		/// A list of strings (case insensitive, no spaces) that can be used to invoke the command
		/// </summary>
		[DataMember]
		public string[] CommandAliases { get; set; }
		/// <summary>
		/// A short sentence for the command list, describing the command
		/// </summary>
		[DataMember]
		public string Description { get; set; }
		/// <summary>
		/// A one-line string indicating the format in which to call the command e.g. "mycmd [args...] {A|B|C}"
		/// </summary>
		[DataMember]
		public string HelpUsage { get; set; }
		/// <summary>
		/// A list of optional arguments allowed
		/// </summary>
		[DataMember]
		public string HelpOptions { get; set; }
		/// <summary>
		/// The description that comes immediately after the command title
		/// </summary>
		[DataMember]
		public string HelpDescription { get; set; }
		/// <summary>
		/// Further help text displayed below the other help text for elaboration and clarification
		/// </summary>
		[DataMember]
		public string HelpRemarks { get; set; }

		public Command()
		{
		}

		public Command(Command cmd)
		{
			Title = cmd.Title;
			CommandAliases = cmd.CommandAliases;
			Description = cmd.Description;
			HelpDescription = cmd.HelpDescription;
			HelpUsage = cmd.HelpUsage;
			HelpOptions = cmd.HelpOptions;
			HelpRemarks = cmd.HelpRemarks;
		}
	}
}