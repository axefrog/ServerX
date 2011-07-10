using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Options;

namespace ServerX.Common
{
	public static class ExtensionMethods
	{
		public static string WriteOptionDescriptions(this OptionSet options)
		{
			using(var sw = new StringWriter())
			{
				options.WriteOptionDescriptions(sw);
				return sw.ToString();
			}
		}

		public static List<string> Split(this string delimitedList, char delimiter, bool trimValues, bool stripDuplicates, bool caseSensitiveDuplicateMatch)
		{
			if(delimitedList == null)
				return new List<string>();
			var lcc = new LowerCaseComparer();
			var list = new List<string>();
			var arr = delimitedList.Split(delimiter);
			foreach(string t in arr)
			{
				var val = trimValues ? t.Trim() : t;
				if(val.Length > 0)
				{
					if(stripDuplicates)
					{
						if(caseSensitiveDuplicateMatch)
						{
							if(!list.Contains(val))
								list.Add(val);
						}
						else if(!list.Contains(val, lcc))
							list.Add(val);
					}
					else
						list.Add(val);
				}
			}
			return list;
		}

		public class LowerCaseComparer : IEqualityComparer<string>
		{
			public bool Equals(string x, string y)
			{
				return string.Compare(x, y, true) == 0;
			}

			public int GetHashCode(string obj)
			{
				return obj.ToLower().GetHashCode();
			}
		}

		public static string Concat<T>(this IEnumerable<T> values, string delimiter)
		{
			StringBuilder sb = new StringBuilder();
			int c = 0;
			if(values == null) values = new T[0];
			foreach(T k in values)
			{
				if(c++ > 0)
					sb.Append(delimiter);
				sb.Append(k);
			}
			return sb.ToString();
		}

		public delegate string StringEncodeHandler<T>(T input);
		public static string Concat<T>(this IEnumerable<T> values, StringEncodeHandler<T> encodeValue)
		{
			return values.Concat("", encodeValue);
		}

		public static string Concat<T>(this IEnumerable<T> values, string delimiter, StringEncodeHandler<T> encodeValue)
		{
			StringBuilder sb = new StringBuilder();
			int c = 0;
			if(values == null) values = new T[0];
			foreach(T k in values)
			{
				if(c++ > 0)
					sb.Append(delimiter);
				sb.Append(encodeValue(k));
			}
			return sb.ToString();
		}
	}
}
