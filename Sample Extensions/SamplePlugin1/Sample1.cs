using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServerX.Common;

namespace SamplePlugin1
{
	public class Sample1 : IServerExtension
	{
		public string ID
		{
			get { return "Sample1"; }
		}

		public string Name
		{
			get { return "Sample Plugin 1"; }
		}

		public string Description
		{
			get { return "This is the first of three sample plugin projects"; }
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
			Console.WriteLine("Sample1 is running.");
		}

		public bool IsRunning
		{
			get { return true; }
		}
	}
}
