using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Text;
using System.Web.Script.Serialization;

namespace ServerX.Common
{
	public static class JavaScriptInterface
	{
		private class JsonCallErr
		{
			public string JsonCallError { get; set; }
			public JsonCallErr(string jsonCallError)
			{
				JsonCallError = jsonCallError;
			}
		}

		public static string JsonErrorResponse(string message)
		{
			return new JavaScriptSerializer().JsonErrorResponse(message);
		}

		public static string JsonErrorResponse(this JavaScriptSerializer jss, string message)
		{
			return jss.Serialize(new JsonCallErr(message));
		}

		public static string JsonCall(object source, string name, string[] jsonArgs, string[] excludedMethodNames)
		{
			var jss = new JavaScriptSerializer();
			var type = source.GetType();
			var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.FlattenHierarchy);
			if(method == null)
				return jss.JsonErrorResponse("Method not found");
			if(!GetOperationContractMethods(type, excludedMethodNames).Any(m => m.Name == name))
				return jss.JsonErrorResponse("Access to this method is not permitted");
			var prms = method.GetParameters();
			if(prms.Length != jsonArgs.Length)
				return jss.JsonErrorResponse("The method takes " + prms.Length + " arguments but " + jsonArgs.Length + " arguments were supplied");
			object[] args = new object[prms.Length];
			for(var i=0; i < args.Length; i++)
			{
				var prmType = prms[i].ParameterType;
				object val;
				try
				{
					val = jss.Deserialize(jsonArgs[i], prmType);
				}
				catch(Exception ex)
				{
					return jss.JsonErrorResponse("Unable to deserialize argument " + i + " to parameter type " + prmType.FullName + ": " + ex.Message);
				}
				args[i] = val;
			}
			object returnVal = null;
			try
			{
				if(method.ReturnType == typeof(void))
					method.Invoke(source, args);
				else
					returnVal = method.Invoke(source, args);
			}
			catch(Exception ex)
			{
				return jss.JsonErrorResponse("Method call failed: " + ex.Message);
			}
			try
			{
				return jss.Serialize(returnVal);
			}
			catch(Exception ex)
			{
				return jss.JsonErrorResponse("Failed to serialize return value: " + ex.Message);
			}
		}

		public static readonly string[] ExcludedServiceManagerJsMethodNames = new[] { "KeepExtensionProcessAlive", "NotifyExtensionServiceReady", "Command", "GetCommandHelp" };
		public static string GenerateJavaScriptWrapper(IServiceManager svc)
		{
			var methods = (from method in GetOperationContractMethods(svc.GetType(), ExcludedServiceManagerJsMethodNames)
			 let friendlyName = method.Name.StartsWith("get_") ? method.Name.Substring(4) : method.Name
			 select string.Format("\t{0}: function({1}) {{ {2}__processJsonResponse(__svc_jsonCall('{3}', __getJsonArgs(arguments))); }}",
				friendlyName, WriteArgumentsList(method), method.ReturnType == typeof(void) ? "" : "return ", method.Name))
				.Concat("," + Environment.NewLine);
			return string.Concat("var ServiceManager = {", Environment.NewLine, methods, Environment.NewLine, "};", Environment.NewLine);
		}

		public static readonly string[] ExcludedExtensionJsMethodNames = new[] { "RegisterClient", "KeepAlive", "JsonCall", "GetJavaScriptWrapper", "Command", "get_SupportsCommandLine" };
		public static string GenerateJavaScriptWrapper(IServerExtension ext)
		{
			var methods = (from method in GetOperationContractMethods(ext.GetType(), ExcludedExtensionJsMethodNames)
			 let friendlyName = method.Name.StartsWith("get_") ? method.Name.Substring(4) : method.Name
			 select string.Format("\t{0}: function({1}) {{ {2}__processJsonResponse(__ext_jsonCall('{3}', '{4}', __getJsonArgs(arguments))); }}",
				friendlyName, WriteArgumentsList(method), method.ReturnType == typeof(void) ? "" : "return ", ext.ID, method.Name))
				.Concat("," + Environment.NewLine);
			return string.Concat("Extensions.", ext.ID , " = {", Environment.NewLine, methods, Environment.NewLine, "};", Environment.NewLine);
		}

		static string WriteArgumentsList(MethodInfo method)
		{
			var args = new StringBuilder();
			var prms = method.GetParameters();
			for(int i = 0; i < prms.Length; i++)
			{
				if(i > 0) args.Append(", ");
				args.Append(prms[i].Name);
			}
			return args.ToString();
		}

		static List<MethodInfo> GetOperationContractMethods(Type type, string[] excludedMethodNames)
		{
			if(excludedMethodNames == null)
				excludedMethodNames = new string[0];
			var list = new List<MethodInfo>();
			foreach(var t in type.GetInterfaces().Where(i => Attribute.GetCustomAttribute(i, typeof(ServiceContractAttribute)) != null))
				foreach(var method in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.FlattenHierarchy))
					if(Attribute.GetCustomAttributes(method, typeof(OperationContractAttribute)).Any() && !excludedMethodNames.Contains(method.Name))
						list.Add(method);
			return list;
		}
	}
}
