using Common;

namespace SystemTrayApp
{
	internal static class Program
	{
		/// <summary>
		///  The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			var logFile = "";

			List<Entry> msgs = [
				new (DateTime.UtcNow.AddMinutes(-5), new Message("Start", "Shell", "A"), null),
				new (DateTime.UtcNow.AddMinutes(-4), new Message("Focus", "Shell", "B"), null),
				new (DateTime.UtcNow.AddMinutes(-3), new Message("Focus", "Shell", "C"), null),
				new (DateTime.UtcNow.AddMinutes(-2), new Message("Focus", "Shell", "D"), null),
				new (DateTime.UtcNow.AddMinutes(-1), new Message("Stop", "Shell", "E"), null),
				];

			ApplicationConfiguration.Initialize();
			IMessageGetLastestRepository messageRepo = new CachingReadonlyRepository(new InMemoryMessageRepository(() => msgs));
			Application.Run(new CustomApplicationContext(new Form1(messageRepo)));
		}
	}
}