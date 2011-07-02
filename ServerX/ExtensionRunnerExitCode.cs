using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServerX
{
	public enum ExtensionRunnerExitCode
	{
		Success = 0,
		Exception = 1,
		InvalidArguments = 2,
		ParentExited = 3,
		UnspecifiedError = 99,
	}
}
