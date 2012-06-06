using System;
using System.IO;
using System.Threading;

namespace ServerX.Common
{
	public class SafeExtensionLoader : IDisposable
	{
		private readonly CancellationTokenSource _tokenSrc;
		private readonly string _extensionsPath;
		private AppDomain _appdomain;
		private IExtensionActivator _extActivator;

		public SafeExtensionLoader(string extensionsBaseDir, string subdirName, string parentProcessID, CancellationTokenSource tokenSrc)
		{
			_tokenSrc = tokenSrc;
			_extensionsPath = Path.Combine(extensionsBaseDir, subdirName);
			var setup = new AppDomainSetup
						{
							ApplicationBase = _extensionsPath,
							PrivateBinPath = _extensionsPath,
							ShadowCopyFiles = "true",
							ShadowCopyDirectories = _extensionsPath
						};
			_appdomain = AppDomain.CreateDomain("ExtensionDirectoryLoader." + subdirName + "." + Guid.NewGuid(), null, setup);
			_extActivator = (IExtensionActivator)_appdomain.CreateInstanceAndUnwrap("ServerX.Common", "ServerX.Common.ExtensionActivator");
			_extActivator.Init(subdirName, parentProcessID);
		}

		public ExtensionInfo[] AvailableExtensions
		{
			get { return _extActivator.Extensions ?? new ExtensionInfo[0]; }
		}

		public void Dispose()
		{
			_extActivator.Dispose();
			_extActivator = null;
			AppDomain.Unload(_appdomain);
			_appdomain = null;
		}

		public void RunExtension(Guid guid, bool runDebugMethodOnExtension, string id)
		{
			_tokenSrc.Token.Register(() => { if(_extActivator != null) _extActivator.SignalCancellation(); });
			_extActivator.RunExtension(guid, runDebugMethodOnExtension, id);
		}

		/// <summary>
		/// This method is only for specialised extensions that have processes requiring execution in the main thread.
		/// In general, these sorts of extensions should be run in isolation from other extensions. If multiple active
		/// extensions are found that want to run in the main app thread, an exception will be thrown as they should be
		/// run in separate processes. This method will block until extensions are loaded and running.
		/// </summary>
		public void RunMainAppThread()
		{
			_extActivator.RunMainAppThread();
		}
	}
}
