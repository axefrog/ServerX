using System;
using System.ServiceModel;
using System.Threading;

namespace ServerX.Common
{
	[ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.Single, UseSynchronizationContext = true, IncludeExceptionDetailInFaults = true, AddressFilterMode = AddressFilterMode.Any)]
	public abstract class ServerExtension : ServiceXBase, IServerExtension
	{
		public abstract string ID { get; }
		public virtual string CommandID { get { return ID; } }
		public abstract string Name { get; }
		public abstract string Description { get; }

		public abstract bool SupportsCommandLine { get; }

		public abstract string Command(string[] args);

		public string JsonCall(string name, string[] jsonArgs)
		{
			return JavaScriptInterface.JsonCall(this, name, jsonArgs, JavaScriptInterface.ExcludedExtensionJsMethodNames);
		}

		public string GetJavaScriptWrapper()
		{
			return JavaScriptInterface.GenerateJavaScriptWrapper(this);
		}

		public bool RunCalled { get; private set; }
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
			get { return typeof(IServerExtension); }
		}

		protected Logger Logger { get; private set; }

		protected override void OnRegisterClient(Guid id)
		{
			Logger.WriteLine("[" + ID + "] Client connected -> registration ID: " + id);
		}

		protected void Notify(string source, string message)
		{
			CallbackEachClient<IServerExtensionCallback>(cb => cb.Notify(source, message));
		}

		public virtual bool HasMainLoop
		{
			get { return false; }
		}

		/// <summary>
		/// This method is only for specialised extensions that have processes requiring execution in the main thread.
		/// In general, these sorts of extensions should be run in isolation from other extensions. If multiple active
		/// extensions are found that want to run in the main app thread, an exception will be thrown as they should be
		/// run in separate processes. <see cref="HasMainLoop"/> should return true in order for this method to run.
		/// </summary>
		public virtual void RunMainAppThreadLoop(CancellationTokenSource cancelSource, Logger logger)
		{
		}

		public virtual void Debug()
		{
			Console.WriteLine("test");
		}
	}
}
