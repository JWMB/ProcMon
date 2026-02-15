namespace Common.MessageRepositories
{
	public class FileMessageRepository : IMessageRepository, IDisposable
    {
        public record Config(FileInfo File);

        private readonly Config config;

		private SemaphoreSlim semaphore = new(1, 1);
		private FileStream? fs;
		private StreamWriter? sw;

		private bool disposed = false;

		public FileMessageRepository(Config config)
        {
            this.config = config;
        }

        private async Task Init()
        {
            if (fs != null)
                return;

            //await GetEntriesFromFile(config.File);

			fs = new FileStream(config.File.FullName, FileMode.Append, FileAccess.Write);
			sw = new StreamWriter(fs);
		}

        public static async Task<List<Entry>> GetEntriesFromFile(FileInfo logFile)
        {
			try
			{
				return Message.ParseLog(await File.ReadAllTextAsync(logFile.FullName)).Select(o => new Entry(o.Item1, o.Item2, null)).ToList();
			}
			catch
            {
                return [];
            }
			// File.ReadAllLines(logFile.FullName)
			//.Select(parser.Parse)
			//.Select(dict =>
			//	System.Text.Json.JsonSerializer.Deserialize<ProcessEntry>(System.Text.Json.JsonSerializer.Serialize(dict)))
			//.OfType<ProcessEntry>()
			//.ToList();
		}

		public async Task Add(Entry entry)
        {
            if (entry.Message == null)
                return;

            await semaphore.WaitAsync();

			await Init();

            var str = $">{entry.Time:yyyy-MM-dd HH:mm:ss}: {System.Text.Json.JsonSerializer.Serialize(entry.Message)}";
			await sw!.WriteLineAsync(str);

            semaphore.Release();
		}

        public async Task<List<Entry>> Get(DateTime since, string? sender = null)
        {
			await Init();
			throw new NotImplementedException();
        }

        public void Dispose()
        {
            if (disposed)
                return;
			disposed = true;

			sw?.Dispose();
            fs?.Dispose();
        }
    }
}
