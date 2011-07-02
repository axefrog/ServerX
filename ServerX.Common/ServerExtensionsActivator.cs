using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Server;

namespace ServerX.Common
{
	internal interface IExtensionsActivator : IDisposable
	{
		ExtensionInfo[] Extensions { get; }
		void RunExtensions(Guid guid, params string[] ids);
	}

	[Serializable]
	internal class ExtensionsActivator : MarshalByRefObject, IExtensionsActivator
	{
		private ExtensionInfo[] _infos;
		private Logger _logger = new Logger("extension-process-" + Process.GetCurrentProcess().Id);

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
			_logger.WriteLine("Obtained info for " + _infos.Length + " available extensions");
		}

		public ExtensionInfo[] Extensions
		{
			get { return _infos; }
		}

		class RunningExtension
		{
			public IServerExtension Extension { get; set; }
			public Task Task { get; set; }
		}
		private Dictionary<string, RunningExtension> _runningExtensions = new Dictionary<string, RunningExtension>();
		private CancellationTokenSource _cancelSource;
		public void RunExtensions(Guid guid, params string[] ids)
		{
			if(ids.Length == 0)
				ids = _infos.Select(i => i.ID).ToArray();
			_logger.WriteLines(
				"Starting extensions:",
				" => " + string.Join(", ", ids),
				" => process monitor ID: " + guid
			);
			lock(this)
			{
				foreach(var id in ids)
				{
					var id1 = id;
					var info = _infos.FirstOrDefault(i => i.ID == id1);
					if(info == null)
						throw new Exception("Cannot run extension with ID [" + id + "] - extension does not exist");
					var type = Type.GetType(info.AssemblyQualifiedName);
					var ext = (IServerExtension)Activator.CreateInstance(type);
					_runningExtensions.Add(ext.ID, new RunningExtension { Extension = ext });
				}
				_cancelSource = new CancellationTokenSource();
			}

			const TaskCreationOptions atp = TaskCreationOptions.AttachedToParent;
			var task = Task.Factory.StartNew(() =>
			{
				_logger.WriteLine("Extension activation/monitoring task starting...");
				lock(this)
					foreach(var ext in _runningExtensions.Values)
					{
						var extension = ext.Extension;
						ext.Task = Task.Factory.StartNew(() => extension.Run(_cancelSource.Token), _cancelSource.Token, atp, TaskScheduler.Current);
					}

				ServiceManagerClient client = null;
				if(guid != Guid.Empty)
				{
					client = new ServiceManagerClient("ServiceManagerClient");
					client.Disconnected += c =>
					{
						_cancelSource.Cancel(); // process manager will take care of restarting this process
						_logger.WriteLine("Disconnected from service manager. Execution on all extensions will be cancelled now to allow the process to shut down.");
					};
				}
				_logger.WriteLine(_runningExtensions.Count + " extensions now running.");
				while(!_cancelSource.IsCancellationRequested)
				{
					foreach(var ext in _runningExtensions.Values)
						if(!ext.Extension.IsRunning || ext.Task.IsCompleted || ext.Task.IsFaulted)
						{
							_logger.WriteLine("Extension [" + ext.Extension.Name + "] is no longer running. Execution on all extensions will be cancelled now to allow the process to restart.");
							_cancelSource.Cancel();
						}
					if(client != null)
						client.KeepExtensionProcessAlive(guid);
					Thread.Sleep(3000);
				}
			}, _cancelSource.Token, atp, TaskScheduler.Current);

			_logger.WriteLine("Waiting on task threads to finish...");
			try
			{
				task.Wait(_cancelSource.Token);
			}
			catch(OperationCanceledException)
			{
			}
			_logger.WriteLine("Task threads have all ended.");
		}

		public void Dispose()
		{
			lock(this)
				if(_runningExtensions != null)
				{
					foreach(var id in _runningExtensions.Keys.ToArray()) // ToArray => prevents exceptions being thrown due to modification of the original collection during iteration
					{
						var ext = _runningExtensions[id];
						_runningExtensions.Remove(id);
						if(ext.Extension is IDisposable)
							((IDisposable)ext.Extension).Dispose();
					}
					_runningExtensions = null;
				}
		}
	}
}