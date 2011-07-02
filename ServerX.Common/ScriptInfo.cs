using System.Runtime.Serialization;

namespace ServerX.Common
{
	[DataContract]
	public class ScriptInfo
	{
		[DataMember]
		public string Filename { get; set; }

		[DataMember]
		public string Description { get; set; }

		[DataMember]
		public string Script { get; set; }
	}
}