using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace ServerX.Common
{
	[ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.Single, UseSynchronizationContext = true, IncludeExceptionDetailInFaults = true, AddressFilterMode = AddressFilterMode.Any)]
	public abstract class ServerExtension : IServerExtensionHost
	{
		public abstract string ID { get; }
		public virtual string CommandID { get { return ID; } }
		public abstract string Name { get; }
		public abstract string Description { get; }

		public abstract bool SupportsCommandLine { get; }
		public abstract string Command(string[] args);

		private class JsonCallErr
		{
			public string JsonCallError { get; set; }
			public JsonCallErr(string jsonCallError)
			{
				JsonCallError = jsonCallError;
			}
		}

		public string JsonCall(string name, string[] jsonArgs)
		{
			var jss = new JavaScriptSerializer();
			var type = GetType();
			var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.FlattenHierarchy);
			if(method == null)
				return jss.Serialize(new JsonCallErr("Method not found"));
			if(!GetOperationContractMethods(type).Any(m => m.Name == name))
				return jss.Serialize(new JsonCallErr("Access to this method is not permitted"));
			var prms = method.GetParameters();
			if(prms.Length != jsonArgs.Length)
				return jss.Serialize(new JsonCallErr("The method takes " + prms.Length + " arguments but " + jsonArgs.Length + " arguments were supplied"));
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
					return jss.Serialize(new JsonCallErr("Unable to deserialize argument " + i + " to parameter type " + prmType.FullName + ": " + ex.Message));
				}
				args[i] = val;
			}
			object returnVal = null;
			try
			{
				if(method.ReturnType == typeof(void))
					method.Invoke(this, args);
				else
					returnVal = method.Invoke(this, args);
			}
			catch(Exception ex)
			{
				return jss.Serialize(new JsonCallErr("Method call failed: " + ex.Message));
			}
			try
			{
				return jss.Serialize(returnVal);
			}
			catch(Exception ex)
			{
				return jss.Serialize(new JsonCallErr("Failed to serialize return value: " + ex.Message));
			}
		}

		internal bool RunCalled { get; private set; }
		public void Run(CancellationTokenSource tokenSource, Logger logger)
		{
			Logger = logger;
			Logger.WriteLine("[" + ID + "] Run() called.");
			RunCalled = true;
			Run(tokenSource);
		}

		public abstract void Run(CancellationTokenSource tokenSource);
		public abstract bool IsRunning { get; }

		/// <summary>
		/// If additional OperationContracts are present, return the WCF contract type of the implemented extension.
		/// </summary>
		public virtual Type ContractType
		{
			get { return typeof(IServerExtensionHost); }
		}

		protected Logger Logger { get; private set; }
		ConcurrentDictionary<Guid, OperationContext> _clients = new ConcurrentDictionary<Guid, OperationContext>();

		readonly string[] _excludedJsMethodNames = new[] { "RegisterClient", "KeepAlive", "JsonCall", "Command", "get_SupportsCommandLine" };
		string GenerateJavaScriptWrapper()
		{
			var list = new List<string>();
			foreach(var method in GetOperationContractMethods(GetType()))
			{
				var args = new StringBuilder();
				var prms = method.GetParameters();
				for(int i = 0; i < prms.Length; i++)
				{
					if(i > 0) args.Append(", ");
					args.Append(prms[i].Name);
				}

				var isGet = method.Name.StartsWith("get_");
				var isVoid = method.ReturnType == typeof(void);
				var name = isGet ? method.Name.Substring(4) : method.Name;
				list.Add(string.Concat('\t', name, ": function(", args, ") { ", (isVoid ? "" : "return "), "ServiceManager.CallExtension('", ID, "', '", method.Name, "'", ", arguments); }"));
			}

			return string.Concat("Extensions.", ID, " = {", Environment.NewLine, list.Concat("," + Environment.NewLine), Environment.NewLine, "}", Environment.NewLine);
		}

		List<MethodInfo> GetOperationContractMethods(Type type)
		{
			var list = new List<MethodInfo>();
			foreach(var t in type.GetInterfaces().Where(i => Attribute.GetCustomAttribute(i, typeof(ServiceContractAttribute)) != null))
				foreach(var method in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.FlattenHierarchy))
					if(Attribute.GetCustomAttributes(method, typeof(OperationContractAttribute)).Any() && !_excludedJsMethodNames.Contains(method.Name))
						list.Add(method);
			return list;
		}

		public void RegisterClient(Guid id)
		{
			Logger.WriteLine("[" + ID + "] Incoming client registration: " + id);
			if(OperationContext.Current != null)
				_clients.AddOrUpdate(id, OperationContext.Current, (k,v) => OperationContext.Current);
			else
				Logger.WriteLine("[" + ID + "] CLIENT REGISTRATION FAILED (OperationContext is null)");
		}

		public void KeepAlive()
		{
			// this method contains no code - the very act of calling it prevents a connected session from timing out
		}

		protected void Notify(string message)
		{
			CallbackEachClient(cb => cb.Notify(message));
		}

		private void CallbackEachClient(Action<IServerExtensionCallback> callback)
		{
			lock(_clients)
			{
				foreach(var kvp in _clients.ToList())
				{
					OperationContext ctx;
					if(kvp.Value.Channel.State == CommunicationState.Faulted)
					{
						Logger.WriteLine("[" + ID + "] Can't callback client - state is faulted");
						_clients.TryRemove(kvp.Key, out ctx);
					}
					else if(kvp.Value.Channel.State == CommunicationState.Opened)
					{
						try
						{
							var cb = kvp.Value.GetCallbackChannel<IServerExtensionCallback>();
							callback(cb);
							//Logger.WriteLine("[" + ID + "] Callback sent.");
						}
						catch(Exception ex)
						{
							Logger.WriteLine("[" + ID + "] Can't callback client - " + ex.Message);
							_clients.TryRemove(kvp.Key, out ctx);
						}
					}
				}
			}
		}
	}
}
