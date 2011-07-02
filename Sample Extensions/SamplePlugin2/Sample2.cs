using System;
using System.Configuration;
using System.Threading;
using ServerX.Common;

namespace SamplePlugin2
{
	public class Sample2 : IServerExtension, IDisposable
	{
		public string ID
		{
			get { return "Sample2"; }
		}

		public string Name
		{
			get { return "Sample Plugin 2"; }
		}

		public string Description
		{
			get { return "This is the second of two sample plugin projects"; }
		}

		bool _isRunning;
		public void Run(CancellationToken token)
		{
			_isRunning = true;
			Console.WriteLine("Sample2 is running.");
			Console.WriteLine("Test: " + ConfigurationManager.AppSettings["Test"]);
			Thread.Sleep(5000);
			_isRunning = false;
		}

		public bool IsRunning
		{
			get { return _isRunning; }
		}

		public void Dispose()
		{
			
		}
	}
}
