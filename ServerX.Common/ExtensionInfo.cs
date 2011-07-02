using System;
using System.Runtime.Serialization;

namespace ServerX.Common
{
	public interface IExtensionInfo
	{
		[DataMember] string ID { get; set; }
		[DataMember] string Name { get; set; }
		[DataMember] string Description { get; set; }
	}

	[DataContract, Serializable]
	public class ExtensionInfo : IExtensionInfo
	{
		[DataMember]
		public string ID { get; set; }

		[DataMember]
		public string Name { get; set; }

		[DataMember]
		public string Description { get; set; }

		internal string AssemblyQualifiedName { get; set; }
	}
}