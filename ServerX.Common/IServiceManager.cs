using System;
using System.ServiceModel;
using NLog;

namespace ServerX.Common
{
	[ServiceContract(Name = "ServiceManager", Namespace = "http://ServerX/ServiceManager", CallbackContract = typeof(IServiceManagerCallback), SessionMode = SessionMode.Allowed)]
	public interface IServiceManager : IServiceXBase, IDisposable
	{
		[OperationContract]
		DateTime GetServerTime();

		//[OperationContract]
		//Result SetExtensionDirectoryIncluded(string name, bool include);

		//[OperationContract]
		//Result SetExtensionsEnabledInDirectory(string name, bool enabled);

		//[OperationContract]
		//Result SetExtensionEnabled(string name, bool enabled);

		[OperationContract]
		Result RestartExtensions(string subdirName, params string[] extensionIDs);

		[OperationContract]
		string[] ListExtensionDirectories();

		//[OperationContract]
		//string[] ListIncludedExtensionDirectories();

		//[OperationContract]
		//ExtensionInfo[] ListAvailableExtensions();

		[OperationContract]
		ExtensionInfo[] ListExtensionsInDirectory(string name);

		[OperationContract]
		string ExecuteCommand(string command, string[] args);

		[OperationContract]
		CommandInfo[] ListExtensionCommands();

		[OperationContract]
		CommandInfo[] ListServiceManagerCommands();

		[OperationContract]
		CommandInfo GetCommandInfo(string cmdAlias);

		[OperationContract]
		ScriptInfo[] ListScripts();

		[OperationContract]
		Result ExecuteScriptFile(string filename);

		[OperationContract]
		ScriptInfo GetScript(string name);

		[OperationContract]
		string SaveScript(ScriptInfo script);

		[OperationContract]
		string DeleteScript(string name);

		[OperationContract]
		ScheduledCommand[] ListScheduledCommands();

		[OperationContract]
		ScheduledCommand AddScheduledCommand(string cron, string command);

		[OperationContract]
		Result DeleteScheduledCommand(int id);

		[OperationContract]
		void KeepExtensionProcessAlive(Guid id);

		[OperationContract]
		void NotifyExtensionServiceReady(Guid extProcID, string address);
	}

	//[ServiceContract(Name = "ServiceManager", Namespace = "http://ServerX/ServiceManager", CallbackContract = typeof(IServiceManagerCallback), SessionMode = SessionMode.Allowed)]
	//public interface IServiceManagerHost : IServiceManager, IServiceXBase
	//{
	//}

	public interface IServiceManagerCallback : IServiceCallbackBase
	{
		[OperationContract(IsOneWay = true)]
		void ServerExtensionNotify(string extID, string extName, string logLevel, string source, string message);
	}
}
