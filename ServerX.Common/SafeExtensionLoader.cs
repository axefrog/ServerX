using System;
using System.IO;

namespace ServerX.Common
{
	public class SafeExtensionLoader : IDisposable
	{
		private readonly string _extensionsPath;
		private AppDomain _appdomain;
		private IExtensionsActivator _exts;

		public SafeExtensionLoader(string extensionsBaseDir, string subdirName, bool outputToConsole)
		{
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
			_exts.RunExtensions(guid, runDebugMethodOnExtension, ids);
		}
	}
}
