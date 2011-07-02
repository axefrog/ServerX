using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IronJS.Hosting;

namespace ServerX
{
	/* 
	 */
	public class ScriptManager
	{
		private CSharp.Context _js;

		public ScriptManager()
		{
			_js = new CSharp.Context();
			//IronJS.Native.Utils.CreateFunction(_js.Environment, 
			_js.Execute("a = 25;");
		}

		public void ExecuteJavaScript(string js)
		{
			
		}
	}

	public enum ScriptType
	{
		CommandList,
		JavaScript
	}
}
