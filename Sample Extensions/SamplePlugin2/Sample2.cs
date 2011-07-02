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

		public string JsonCall(string name, string data)
		{
			throw new NotImplementedException();
		}

		public string Command(string name, string[] args)
		{
			throw new NotImplementedException();
		}

		public void Run()
		{
			Console.WriteLine("Sample2 is running.");
			Console.WriteLine("Test: " + ConfigurationManager.AppSettings["Test"]);
			Thread.Sleep(5000);
		}

		public bool IsRunning
		{
			get { return true; }
		}

		public void Dispose()
		{
			
		}
	}
}
