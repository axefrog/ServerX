using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ServerX.Common
{
	internal interface IExtensionsActivator : IDisposable
	{
		ExtensionInfo[] Extensions { get; }
		void RunExtensions(params string[] ids);
	}

	[Serializable]
	internal class ExtensionsActivator : MarshalByRefObject, IExtensionsActivator
	{
		private ExtensionInfo[] _infos;

		public ExtensionsActivator()
		{
			var list = new List<ExtensionInfo>();
			var files = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).GetFiles("*.dll");
			foreach(var file in files)
			{
				var asm = Assembly.Load(Path.GetFileNameWithoutExtension(file.Name));
				var typeMap = (from t in asm.GetTypes()
				                   where t.GetInterfaces().Any(i => i == typeof(IServerExtension)) && t.IsClass && !t.IsAbstract
				                   select new { Ext = (IServerExtension)Activator.CreateInstance(t) }).ToDictionary(k => k.Ext.ID, v => v.Ext);
				list.AddRange(
					typeMap.Values.Select(ext => new ExtensionInfo {
						ID = ext.ID,
						Name = ext.Name,
						Description = ext.Description,
						AssemblyQualifiedName = ext.GetType().AssemblyQualifiedName
					})
				);
			}
			_infos = list.ToArray();
		}

		public ExtensionInfo[] Extensions
		{
			get { return _infos; }
		}

		private Dictionary<string, IServerExtension> _runningExtensions = new Dictionary<string, IServerExtension>();
		public void RunExtensions(params string[] ids)
		{
			if(ids.Length == 0)
				ids = _infos.Select(i => i.ID).ToArray();
			foreach(var id in ids)
			{
				var info = _infos.FirstOrDefault(i => i.ID == id);
				if(info == null)
					throw new Exception("Cannot run extension with ID [" + id + "] - extension does not exist");
				var type = Type.GetType(info.AssemblyQualifiedName);
				var ext = (IServerExtension)Activator.CreateInstance(type);
				ext.Run();
				_runningExtensions.Add(ext.ID, ext);
			}
		}

		public void Dispose()
		{
			if(_runningExtensions != null)
			{
				foreach(var id in _runningExtensions.Keys.ToArray()) // ToArray => prevents exceptions being thrown due to modification of the original collection during iteration
				{
					var ext = _runningExtensions[id];
					_runningExtensions.Remove(id);
					if(ext is IDisposable)
						((IDisposable)ext).Dispose();
				}
				_runningExtensions = null;
			}
		}
	}
}