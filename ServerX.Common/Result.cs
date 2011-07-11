using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace ServerX.Common
{
	[DataContract, Serializable]
	public class Result
	{
		[DataMember]
		public bool Success { get; set; }

		[DataMember]
		public string Message { get; set; }

		public Result()
		{
		}

		public Result(bool success = true, string message = null)
		{
			Success = success;
			Message = message;
		}

		public static bool operator ==(Result a, bool b)
		{
			return a.Success == b;
		}

		public static bool operator !=(Result a, bool b)
		{
			return a.Success != b;
		}

		public static bool operator ==(bool a, Result b)
		{
			return a == b.Success;
		}

		public static bool operator !=(bool a, Result b)
		{
			return a != b.Success;
		}

		public bool Equals(Result other)
		{
			if(ReferenceEquals(null, other)) return false;
			if(ReferenceEquals(this, other)) return true;
			return other.Success.Equals(Success);
		}

		public override bool Equals(object obj)
		{
			if(ReferenceEquals(null, obj)) return false;
			if(ReferenceEquals(this, obj)) return true;
			return Equals((Result)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return Success.GetHashCode();
			}
		}
	}
}
