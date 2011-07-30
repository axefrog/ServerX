using System;
using System.Runtime.Serialization;

namespace ServerX.Common
{
	[DataContract, Serializable]
	public class ExtensionInfo
	{
		[DataMember]
		public string ExtensionID { get; set; }

		[DataMember]
		public string Name { get; set; }

		[DataMember]
		public string Description { get; set; }

		[DataMember]
		public CommandInfo[] Commands { get; set; }

		[IgnoreDataMember]
		internal string AssemblyQualifiedName { get; set; }

		public ExtensionInfo Clone()
		{
			return new ExtensionInfo
			{
				ExtensionID = ExtensionID,
				Name = Name,
				AssemblyQualifiedName = AssemblyQualifiedName,
				Description = Description,
				Commands = Commands
			};
		}
	}
}