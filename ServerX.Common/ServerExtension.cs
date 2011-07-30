using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;

namespace ServerX.Common
{
	[ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.Single, UseSynchronizationContext = true, IncludeExceptionDetailInFaults = true, AddressFilterMode = AddressFilterMode.Any)]
	public abstract class ServerExtension : ServiceXBase, IServerExtension
	{
		public abstract string ID { get; }
		public abstract string Name { get; }
		public abstract string Description { get; }
		public abstract bool SingleInstanceOnly { get; }

		public CommandInfo[] GetCommands()
		{
			return _commands.Select(cmd => new CommandInfo(cmd)).ToArray();
		}

		private List<IServerExtensionCommand> _commands = new List<IServerExtensionCommand>();
		protected void RegisterCommand<T>()
			where T : IServerExtensionCommand, new()
		{
			_commands.Add(new T());
		}

		protected void RegisterCommand(IServerExtensionCommand cmd)
		{
			_commands.Add(cmd);
		}

		public abstract void Init();

		public string Command(string cmdAlias, string[] args)
		{
			cmdAlias = cmdAlias.ToLower();
			var cmd = _commands.FirstOrDefault(c => c.CommandAliases != null && c.CommandAliases.Any(a => a.ToLower() == cmdAlias));
			if(cmd == null)
				return "%!The command %@" + cmdAlias + "%@ is not valid for server extension \"" + ID + "\".%!";

			try
			{
				return cmd.Execute(this, args);
			}
			catch(Exception ex)
			{
				Logger.WriteLine("COMMAND EXCEPTION: " + args.Concat(" ") + Environment.NewLine + ex);
				throw;
			}
		}

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

		protected void Notify(string source, string message, LogLevel level)
		{
			CallbackEachClient<IServerExtensionCallback>(cb => cb.Notify(source, message, level));
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
