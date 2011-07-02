using System.Runtime.Serialization;

namespace ServerX.Common
{
	[DataContract]
	public class CommandInfo
	{
		[DataMember]
		public string ID { get; set; }

		[DataMember]
		public string Name { get; set; }

		[DataMember]
		public string Description { get; set; }
	}
}