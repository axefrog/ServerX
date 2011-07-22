using System;
using System.ServiceModel;

namespace ServerX.Common
{
	[ServiceContract(Name = "ServiceManager", Namespace = "http://ServerX/ServiceManager", CallbackContract = typeof(IServiceManagerCallback), SessionMode = SessionMode.Allowed)]
	public interface IServiceManager : IServiceXBase, IDisposable
	{
		[OperationContract]
		DateTime GetServerTime();

		[OperationContract]
		Result SetExtensionDirectoryIncluded(string name, bool include);

		[OperationContract]
		Result SetExtensionsEnabledInDirectory(string name, bool enabled);

		[OperationContract]
		Result SetExtensionEnabled(string name, bool enabled);

		[OperationContract]
		Result RestartExtensions(string subdirName, params string[] extensionIDs);

		[OperationContract]
		string[] ListExtensionDirectories();

		[OperationContract]
		string[] ListIncludedExtensionDirectories();

		[OperationContract]
		ExtensionInfo[] ListAvailableExtensions();

		[OperationContract]
		ExtensionInfo[] ListExtensionsInDirectory(string name);

		[OperationContract]
		string ExecuteCommand(string command, string[] args);

		[OperationContract]
		ExtensionInfo[] ListExtensionCommands();

		[OperationContract]
		Command[] ListServiceManagerCommands();

		[OperationContract]
		string GetCommandHelp(string command);

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
		void NotifyExtensionServiceReady(string address);
	}

	//[ServiceContract(Name = "ServiceManager", Namespace = "http://ServerX/ServiceManager", CallbackContract = typeof(IServiceManagerCallback), SessionMode = SessionMode.Allowed)]
	//public interface IServiceManagerHost : IServiceManager, IServiceXBase
	//{
	//}

	public interface IServiceManagerCallback : IServiceCallbackBase
	{
		[OperationContract(IsOneWay = true)]
		void ServerExtensionNotify(string extID, string extName, string source, string message);
	}
}
