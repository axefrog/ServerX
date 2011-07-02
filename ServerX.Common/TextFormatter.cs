using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServerX.Common
{
	public static class TextFormatter
	{
		public static void Tokenize(string text)
		{
		}
	}

	public enum TextCategory
	{
		/// <summary>
		/// Plain text - no specific colorization
		/// </summary>
		Normal,
		/// <summary>
		/// Brighter/enlarged text for headlines
		/// </summary>
		Headline,
		/// <summary>
		/// Example text indicating input a user can type in
		/// </summary>
		InputExample,
		/// <summary>
		/// Example text indicating a possible output result
		/// </summary>
		OutputExample,
		/// <summary>
		/// Important information that is neither positive nor negative
		/// </summary>
		Important,
		/// <summary>
		/// Indicates a successful result or notification
		/// </summary>
		Success,
		/// <summary>
		/// Indicates a problematic result or notification
		/// </summary>
		Problem,
		/// <summary>
		/// Sets the current text position as the indent 
		/// </summary>
		Indent,
		/// <summary>
		/// Drops back to the previous indent position
		/// </summary>
		Outdent
	}
}
