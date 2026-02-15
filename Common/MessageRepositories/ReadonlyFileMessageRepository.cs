namespace Common.MessageRepositories
{
	public class ReadonlyFileMessageRepository : IMessageReadOnlyRepository, IDisposable
	{
		private readonly Config config;
		private FileStream? fs;
		private StreamReader? sr;

		public record Config(FileInfo File, bool KeepFileOpen);
		public ReadonlyFileMessageRepository(Config config)
		{
			this.config = config;
		}

		private async Task Òpen()
		{
			if (fs != null)
				return;
			fs = new FileStream(config.File.FullName, FileMode.Open, FileAccess.Read);
			sr = new StreamReader(fs);
		}
		private void Close()
		{
			sr?.Dispose();
			sr = null;

			fs?.Dispose();
			fs = null;
		}

		public void Dispose()
		{
			Close();
		}

		public async Task<List<Entry>> Get(DateTime since, string? sender = null)
		{
			var wasOpen = sr != null;
			await Òpen();
			if (sr != null)
			{
				var pos = sr.BaseStream.Position;
				var str = await sr.ReadLineAsync();
				if (str != null)
				{
					var entry = Message.ParseLogLine(str);
				}
			}

			if (!wasOpen && !config.KeepFileOpen)
				Close();

			return [];
		}
	}
}
