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
		private IExtensionsActivator _exts;

		public SafeExtensionLoader(string extensionsBaseDir, string subdirName, bool outputToConsole, CancellationTokenSource tokenSrc)
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
			_appdomain = AppDomain.CreateDomain("ExtensionDirectoryLoader." + subdirName, null, setup);
			_exts = (IExtensionsActivator)_appdomain.CreateInstanceAndUnwrap("ServerX.Common", "ServerX.Common.ExtensionsActivator");
			_exts.Init(subdirName, outputToConsole);
		}

		public ExtensionInfo[] AllExtensions
		{
			get { return _exts.Extensions ?? new ExtensionInfo[0]; }
		}

		public void Dispose()
		{
			_exts.Dispose();
			_exts = null;
			AppDomain.Unload(_appdomain);
			_appdomain = null;
		}

		public void RunExtensions(Guid guid, string runDebugMethodOnExtension, params string[] ids)
		{
			_tokenSrc.Token.Register(() => { if(_exts != null) _exts.SignalCancellation(); });
			_exts.RunExtensions(guid, runDebugMethodOnExtension, ids);
		}

		/// <summary>
		/// This method is only for specialised extensions that have processes requiring execution in the main thread.
		/// In general, these sorts of extensions should be run in isolation from other extensions. If multiple active
		/// extensions are found that want to run in the main app thread, an exception will be thrown as they should be
		/// run in separate processes. This method will block until extensions are loaded and running.
		/// </summary>
		public void RunMainAppThread()
		{
			_exts.RunMainAppThread();
		}
	}
}
