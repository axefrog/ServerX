using System;
using System.Runtime.Serialization;

namespace ServerX.Common
{
	[DataContract, Serializable]
	public class ExtensionInfo
	{
		[DataMember]
		public string ID { get; set; }

		[DataMember]
		public string CommandID { get; set; }

		[DataMember]
		public string Name { get; set; }

		[DataMember]
		public string Description { get; set; }

		[DataMember]
		public bool SupportsCommandLine { get; set; }

		[IgnoreDataMember]
		internal string AssemblyQualifiedName { get; set; }

		public ExtensionInfo Clone()
		{
			return new ExtensionInfo
			{
				ID = ID,
				Name = Name,
				AssemblyQualifiedName = AssemblyQualifiedName,
				CommandID = CommandID,
				Description = Description,
				SupportsCommandLine = SupportsCommandLine
			};
		}
	}
}