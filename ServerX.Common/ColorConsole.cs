using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NLog;

namespace ServerX.Common
{
	public static class ColorConsole
	{
		public static void Write(string str, ConsoleColor color)
		{
			Console.ForegroundColor = color;
			Console.Write(str);
			Console.ResetColor();
		}

		public static void Write(string str, LogLevel level)
		{
			Console.ForegroundColor = GetColor(level);
			Console.Write(str);
			Console.ResetColor();
		}

		public static ConsoleColor GetColor(LogLevel level)
		{
			switch(level.Name)
			{
				case "Trace": return ConsoleColor.DarkGray;
				case "Debug": return ConsoleColor.DarkCyan;
				case "Info": return ConsoleColor.Gray;
				case "Warn": return ConsoleColor.Yellow;
				case "Error": return ConsoleColor.Red;
				case "Fatal": return ConsoleColor.Red;
			}
			return ConsoleColor.Gray;
		}

		public static void WriteLine(string str, ConsoleColor color)
		{
			Console.ForegroundColor = color;
			Console.WriteLine(str);
			Console.ResetColor();
		}

		public static void WriteLine(string str, LogLevel level)
		{
			Console.ForegroundColor = GetColor(level);
			Console.WriteLine(str);
			Console.ResetColor();
		}

		public static void WriteLines(string str, ConsoleColor color)
		{
			Console.ForegroundColor = color;
			foreach(var line in str.BreakLines())
				WriteLine(line);
			Console.ResetColor();
		}

		public static void Write(params string[] str)
		{
			var colorStack = new Stack<string>();
			var sb = new StringBuilder();
			foreach(var s in str)
				sb.Append(s);
			WriteColorCodedString(sb.ToString(), colorStack);
		}

		public static void WriteLine(params string[] str)
		{
			Write(str);
			Console.WriteLine();
		}

		public static void WriteLines(string str)
		{
			var colorStack = new Stack<string>();
			foreach(var line in str.BreakLines())
			{
				WriteColorCodedString(line, colorStack);
				Console.WriteLine();
			}
			Console.ResetColor();
		}

		public static void WriteLinesLabelled(string label, int labelWidthChars, ConsoleColor labelColor, ConsoleColor textColor, string text)
		{
			var linelen = Console.BufferWidth - labelWidthChars - 2;
			var textlines = text.BreakLines(linelen);
			bool isFirst = true;
			var padded = new string(' ', labelWidthChars + 2);
			label = label.PadRight(labelWidthChars + 2);
			Write(label, labelColor);
			StringBuilder sb = new StringBuilder();
			foreach(var line in textlines)
			{
				if(!isFirst)
					sb.Append(padded);
				sb.AppendLine(line);
				isFirst = false;
			}
			WriteLines(sb.ToString(), textColor);
		}

		static void WriteColorCodedString(string str, Stack<string> colorStack)
		{
			var baseColor = Console.ForegroundColor;
			int len = 0;
			foreach(string chunk in _colorRx.Split(str))
			{
				if(chunk.Length == 2 && _colorRx.IsMatch(chunk))
				{
					if(chunk != "%>") // indents are written like colour codes but are not colour codes
					{
						if(colorStack.Count > 0 && colorStack.Peek() == chunk)
						{
							colorStack.Pop();
							if(colorStack.Count > 0)
								SetColorByCode(colorStack.Peek());
							else
								Console.ForegroundColor = baseColor;
						}
						else
						{
							colorStack.Push(chunk);
							SetColorByCode(chunk);
						}
					}
				}
				else
				{
					len += chunk.Length;
					Console.Write(chunk);
				}
			}
		}

		static void SetColorByCode(string code)
		{
			switch(code[1])
			{
				case '!': Console.ForegroundColor = ConsoleColor.Red; break; //failure
				case '~': Console.ForegroundColor = ConsoleColor.Green; break; //success
				case '?': Console.ForegroundColor = ConsoleColor.Cyan; break; //special information
				case '@': Console.ForegroundColor = ConsoleColor.Yellow; break; //command
				case '*': Console.ForegroundColor = ConsoleColor.White; break; //heading
				case '#': Console.ForegroundColor = ConsoleColor.DarkMagenta; break; //example
				default: Console.ResetColor(); break;
			}
		}

		public static List<string> BreakLines(this string str)
		{
			return str.BreakLines(Console.BufferWidth);
		}

		static Regex _colorRx = new Regex(@"(\%[\*\@\!\?\~\>\#])");

		public static List<string> BreakLines(this string str, int linelen)
		{
			str = (str ?? "").Replace("\t", "        ");
			// 1. break and trim long lines
			// 2. the first chunk of any explicit line should not be trimmed
			// 3. explicit blank lines should be preserved
			// 4. color codes (_colorRx) should not count towards the line length
			List<string> list = new List<string>();
			using(var reader = new StringReader(str ?? ""))
			{
				var line = reader.ReadLine();
				while(line != null)
				{
					var adjlinelen = linelen;
					var nextlinelen = adjlinelen;
					bool isFirst = true;
					var indent = 0;
					while(line.Length >= 0)
					{
						int? nextindent = null;
						int len;
						if(line.Length > adjlinelen)
						{
							var sublen = Math.Min(adjlinelen, line.Length);
							int c = 0; // adjust character count
							int n = 0; // actual character count
							len = -1; // last space position
							while(c < sublen && n < line.Length)
							{
								if(n < line.Length - 1 && _colorRx.IsMatch(line.Substring(n, 2)))
								{
									if(line.Substring(n, 2) == "%>")
									{
										nextlinelen = linelen - c;
										nextindent = c;
									}
									n += 2;
								}
								else
								{
									if(line[n] == ' ' && n > 0)
										len = n;
									n++;
									c++;
								}
							}
							//len = line.LastIndexOf(' ', sublen - 1, sublen);
							if(len == -1)
								len = adjlinelen;
							adjlinelen = nextlinelen;
						}
						else
							len = line.Length;

						var newstr = line.Substring(0, len);
						line = line.Substring(newstr.Length);
						if(newstr.Length > 0 && !isFirst)
						{
							newstr = newstr.Trim();
							if(newstr.Length == 0)
								newstr = null;
						}
						if(newstr != null)
							list.Add(new string(' ', indent) + newstr);

						isFirst = false;
						if(nextindent.HasValue)
							indent = nextindent.Value;

						if(line.Length == 0)
							break;
					}
					line = reader.ReadLine();
				}
			}
			return list;
		}
	}
}
