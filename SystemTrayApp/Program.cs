using Common;
using Common.MessageRepositories;

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
			List<Entry> fakeMsgs = [
				new (DateTime.UtcNow.AddMinutes(-5), new Message("Start", "Shell", "A"), null),
				new (DateTime.UtcNow.AddMinutes(-4), new Message("Focus", "Shell", "B"), null),
				new (DateTime.UtcNow.AddMinutes(-3), new Message("Focus", "Shell", "C"), null),
				new (DateTime.UtcNow.AddMinutes(-2), new Message("Focus", "Shell", "D"), null),
				new (DateTime.UtcNow.AddMinutes(-1), new Message("Stop", "Shell", "E"), null),
				];
			
			var logFile = @"C:\Users\JonasBeckeman\AppData\Roaming\activity.log";
			var fi = new FileInfo(logFile);
			IMessageReadOnlyRepository repo = fi.Exists ? new ReadonlyFileMessageRepository(new(fi, false)) : new InMemoryMessageRepository(() => fakeMsgs);

			ApplicationConfiguration.Initialize();
			IMessageGetLastestRepository messageRepo = new CachingReadonlyRepository(repo);
			Application.Run(new CustomApplicationContext(new MainForm(messageRepo)));
		}
	}
}