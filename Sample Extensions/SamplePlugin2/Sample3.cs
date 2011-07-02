using System;
using System.Threading;
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

		public void Run(CancellationToken token)
		{
			Console.WriteLine("Sample3 is running.");
		}

		public bool IsRunning
		{
			get { return true; }
		}
	}
}