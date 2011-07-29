using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using Jint;
using Jint.Expressions;
using Jint.Native;
using ServerX.Common;
using ServerX.Properties;

namespace ServerX
{
	internal class ScriptRunner
	{
		private readonly ServiceManager _svc;
		private readonly ExtensionClientManager _clientManager;
		string _preScript;
		JintEngine _engine = new JintEngine();

		public ScriptRunner(ServiceManager svc, ExtensionClientManager clientManager)
		{
			_svc = svc;
			_clientManager = clientManager;
			_engine.DisableSecurity();
			_engine.SetFunction("__svc_jsonCall", new Func<string, string, string>(ServiceManagerJsonCall));
			_engine.SetFunction("__ext_jsonCall", new Func<string, string, string, string>(ExtensionJsonCall));

			_preScript = string.Concat(
				Resources.json2,
				Resources.scriptrunner,
				JavaScriptInterface.GenerateJavaScriptWrapper(svc),
				clientManager.GetJavaScriptWrappers()
			);
			try
			{
				_engine.Run(_preScript);
			}
			catch(Exception ex)
			{
				LastException = ex;
			}
		}

		public Exception LastException { get; private set; }

		public Result ExecuteJavaScript(string js)
		{
			try
			{
				var response = _engine.Run(js);
				if(response is JsString || response is string)
					return new Result(true, response.ToString());
				return Result.Succeeded;
			}
			catch(JintException ex)
			{
				Exception e = ex;
				while(e.InnerException != null)
					e = e.InnerException;
				LastException = ex;
				return new Result(false, e is JsException ? ((JsException)e).Value.ToString() : e.Message);
			}
			catch(Exception ex)
			{
				return new Result(false, "An exception was thrown while trying to execute the script: " + ex.Message);
			}
		}

		public Result ExecuteJavaScriptFile(string filename)
		{
			var info = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", filename));
			if(!info.Exists)
				return new Result(false, "Specified file does not exist");
			string js;
			try
			{
				js = File.ReadAllText(info.FullName);
			}
			catch(Exception ex)
			{
				return new Result(false, "Unable to read file: " + ex.Message);
			}
			return ExecuteJavaScript(js);
		}

		string ServiceManagerJsonCall(string methodName, string jsonArgs)
		{
			return _svc.JsonCall(methodName, new JavaScriptSerializer().Deserialize<string[]>(jsonArgs));
		}

		string ExtensionJsonCall(string extID, string methodName, string jsonArgs)
		{
			return _clientManager.JsonCall(extID, methodName, new JavaScriptSerializer().Deserialize<string[]>(jsonArgs));
		}
	}

	public enum ScriptType
	{
		CommandList,
		JavaScript
	}
}
