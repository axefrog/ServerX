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

		internal string AssemblyQualifiedName { get; set; }
	}
}