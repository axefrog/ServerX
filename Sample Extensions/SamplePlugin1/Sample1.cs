using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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

		public void Run(CancellationToken token)
		{
			while(!token.IsCancellationRequested)
			{
				Console.WriteLine("Sample1 is running. " + DateTime.UtcNow);
				Thread.Sleep(5000);
			}
		}

		public bool IsRunning
		{
			get { return true; }
		}
	}
}
