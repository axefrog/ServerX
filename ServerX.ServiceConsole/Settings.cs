using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServerX.ServiceConsole
{
	public class Settings
	{
		public Settings()
		{
			Macros = new List<Macro>();
		}

		public List<Macro> Macros { get; set; }

		public void AddMacro(string name, string command)
		{
			name = name.ToLower();
			var macro = Macros.FirstOrDefault(m => m.Name == name);
			if(macro == null)
				Macros.Add(new Macro { Name = name, Command = command });
			else
				macro.Command = command;
		}

		public void DeleteMacro(string name)
		{
			Macros = Macros.Where(m => m.Name != name).ToList();
		}
	}
}
