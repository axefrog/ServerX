using System;
using ServerX.Common;

namespace SamplePlugin2
{
	public class Sample3 : IServerExtension
	{
		public string ID
		{
			get { return "Sample3"; }
		}

		public string Name
		{
			get { return "Sample Plugin 3"; }
		}

		public string Description
		{
			get { return "This is the third of three sample plugin projects"; }
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
			Console.WriteLine("Sample3 is running.");
		}

		public bool IsRunning
		{
			get { return true; }
		}
	}
}