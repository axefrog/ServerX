namespace ServerX.ServiceConsole
{
	public interface IConsolePlugin
	{
		string Name { get; }
		string Description { get; }
		void Init(Application app);
	}
}
