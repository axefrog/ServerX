using System;
using System.Configuration;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Web.Script.Serialization;

namespace ServerX.Common
{
	public class PersistenceManager<T> : IDisposable
	{
		private readonly Func<T> _createConfigObject;
		private readonly IPersistenceSerializer<T> _serializer;
		private readonly string _settingsPath;
		private readonly Mutex _mutex;
		private readonly Mutex _mutexCfgFile;
		private readonly string _cfgFilePath;
		private readonly FileInfo _fileInfo;
		private static MutexSecurity _mutexsecurity;

		static PersistenceManager()
		{
			var sid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
			_mutexsecurity = new MutexSecurity();
			_mutexsecurity.AddAccessRule(new MutexAccessRule(sid, MutexRights.FullControl, AccessControlType.Allow));
			_mutexsecurity.AddAccessRule(new MutexAccessRule(sid, MutexRights.ChangePermissions, AccessControlType.Allow));
			_mutexsecurity.AddAccessRule(new MutexAccessRule(sid, MutexRights.Delete, AccessControlType.Allow));
		}

		/// <summary>
		/// Constructs a new persistence manager object
		/// </summary>
		/// <param name="configName">A filename-friendly string that will be used as the basis of the configuration filename (usually a plugin name or other simple string with no special characters)</param>
		/// <param name="settingsPath">The directory that will contain the saved file</param>
		public PersistenceManager(string configName, string settingsPath = null)
			: this(configName, Activator.CreateInstance<T>, new JSONPersistenceSerializer<T>(), settingsPath: settingsPath)
		{
		}

		/// <summary>
		/// Constructs a new persistence manager object using a custom serializer
		/// </summary>
		/// <param name="configName">A filename-friendly string that will be used as the basis of the configuration filename (usually a plugin name or other simple string with no special characters)</param>
		/// <param name="createConfigObject">A delegate that allows control over how the persistence object is constructed and initialized</param>
		/// <param name="settingsPath">The directory that will contain the saved file</param>
		public PersistenceManager(string configName, Func<T> createConfigObject, string settingsPath = null)
			: this(configName, createConfigObject, new JSONPersistenceSerializer<T>(), settingsPath: settingsPath)
		{
		}

		/// <summary>
		/// Constructs a new persistence manager object using a custom serializer
		/// </summary>
		/// <param name="configName">A filename-friendly string that will be used as the basis of the configuration filename (usually a plugin name or other simple string with no special characters)</param>
		/// <param name="serializer">The object that will handle serialization of the config object to and from a string</param>
		/// <param name="settingsPath">The directory that will contain the saved file</param>
		public PersistenceManager(string configName, IPersistenceSerializer<T> serializer, string settingsPath = null)
			: this(configName, Activator.CreateInstance<T>, serializer, settingsPath: settingsPath)
		{
		}

		/// <summary>
		/// Constructs a new persistence manager object using a custom serializer
		/// </summary>
		/// <param name="configName">A filename-friendly string that will be used as the basis of the configuration filename (usually a plugin name or other simple string with no special characters)</param>
		/// <param name="serializer">The object that will handle serialization of the config object to and from a string</param>
		/// <param name="createConfigObject">A delegate that allows control over how the persistence object is constructed and initialized</param>
		/// <param name="settingsPath">The directory that will contain the saved file</param>
		public PersistenceManager(string configName, Func<T> createConfigObject, IPersistenceSerializer<T> serializer, string settingsPath = null)
		{
			_createConfigObject = createConfigObject;
			_serializer = serializer;
			_settingsPath = settingsPath ?? ConfigurationManager.AppSettings["DataDirectory"];
			bool isNew;
			_mutex = new Mutex(false, "Global.ServerX.PersistenceManager", out isNew, _mutexsecurity);
			_mutexCfgFile = new Mutex(false, "Global.ServerX.PersistenceManager." + configName, out isNew, _mutexsecurity);

			_mutex.WaitOne();
			try
			{
				var dir = new DirectoryInfo(_settingsPath ?? string.Format("{0}\\Config", ConfigurationManager.AppSettings["DataDirectory"] ?? Environment.CurrentDirectory));
				if(!dir.Exists)
					dir.Create();
				_cfgFilePath = Path.Combine(dir.FullName, configName + ".cfg");
				_fileInfo = new FileInfo(_cfgFilePath);
			}
			finally
			{
				_mutex.ReleaseMutex();
			}
			Reload();
		}

		public class SettingsUpdate : IDisposable
		{
			private readonly PersistenceManager<T> _pm;

			internal SettingsUpdate(PersistenceManager<T> pm)
			{
				_pm = pm;
				_pm.Lock();
			}

			public void Dispose()
			{
				_pm.Save();
				_pm.Unlock();
			}
		}

		/// <summary>
		/// Provides a simple mechanism to safely lock and update the values of the settings object. Disposal saves the values
		/// and unlocks the settings object. This method is desiged to be used from within a "using" block.
		/// </summary>
		/// <returns>A context object which saves and unlocks the settings object upon disposal</returns>
		public SettingsUpdate BeginUpdate()
		{
			return new SettingsUpdate(this);
		}

		bool _locked;

		public void Lock()
		{
			_mutexCfgFile.WaitOne();
			_locked = true;
		}

		public void Unlock()
		{
			_mutexCfgFile.ReleaseMutex();
			_locked = false;
		}

		public void Save()
		{
			var alreadyLocked = _locked;
			try
			{
				if(!alreadyLocked)
					Lock();
				var content = _serializer.Serialize(Values);
				File.WriteAllText(_cfgFilePath, content);
			}
			finally
			{
				if(!alreadyLocked)
					Unlock();
			}
		}

		public void Reload()
		{
			var alreadyLocked = _locked;
			try
			{
				if(!alreadyLocked)
					Lock();
				if(File.Exists(_cfgFilePath))
				{
					var content = File.ReadAllText(_cfgFilePath);
					Values = _serializer.Deserialize(content);
				}
				else
					Values = _createConfigObject();
			}
			finally
			{
				if(!alreadyLocked)
					Unlock();
			}
		}

		public void Dispose()
		{
			_mutex.Dispose();
			_mutexCfgFile.Dispose();
			if(_serializer is IDisposable)
				((IDisposable)_serializer).Dispose();
		}

		public T Values { get; private set; }

		public DateTime FileDate
		{
			get
			{
				_fileInfo.Refresh();
				return _fileInfo.LastWriteTime;
			}
		}
	}

	public interface IPersistenceSerializer<T>
	{
		string Serialize(T @object);
		T Deserialize(string @serialized);
	}

	public class JSONPersistenceSerializer<T> : IPersistenceSerializer<T>
	{
		JavaScriptSerializer _jss = new JavaScriptSerializer();
		public string Serialize(T @object)
		{
			return _jss.Serialize(@object);
		}

		public T Deserialize(string serialized)
		{
			return _jss.Deserialize<T>(serialized);
		}
	}
}
