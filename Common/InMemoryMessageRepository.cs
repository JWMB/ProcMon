namespace Common
{
    public interface IMessageRepository
    {
        Task Add(Entry entry);
        Task<List<Entry>> Get(DateTime since, string? sender = null);
    }

    public record Entry(DateTime Time, Message Message, string? Sender);

    public static class EntryExtensions
    {
        public static List<(DateTime Start, TimeSpan Duration, List<Entry> Entries)> AsSessions(this IEnumerable<Entry> entries, TimeSpan threshold)
        {
			var ordered = entries.OrderBy(o => o.Time);

            var tmp = ordered
                .Paired(firstEntryIsDouble: true)
                .Select(pair => new { Diff = pair.Item2.Time - pair.Item1.Time, Item = pair.Item1 })
                .ToList();

            return tmp.SplitBy(o => o.Diff > threshold).Select(o => (o.First().Item.Time, o.Last().Item.Time - o.First().Item.Time, o.Select(p => p.Item).ToList())).ToList();
        }
    }

    public class CompositeMessageRepository : IMessageRepository, IDisposable
    {
        private readonly List<IMessageRepository> inner;
        private readonly IMessageRepository innerGet;

        public CompositeMessageRepository(IEnumerable<IMessageRepository> innerAdd, IMessageRepository innerGet)
        {
            this.inner = innerAdd.ToList();
            this.innerGet = innerGet;
        }

        public async Task Add(Entry entry)
        {
            foreach (var item in inner)
                await item.Add(entry);
        }

        public void Dispose()
        {
            foreach (var item in inner.Concat([innerGet]).Distinct().OfType<IDisposable>())
                item.Dispose();
		}

		public Task<List<Entry>> Get(DateTime since, string? sender = null)
            => innerGet.Get(since, sender);
	}

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

    public class InMemoryMessageRepository : IMessageRepository
	{
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private List<Entry> entries = new();
		public InMemoryMessageRepository(Func<IEnumerable<Entry>> init)
        {
			entries = init().ToList();
		}

        public async Task Add(Entry entry)
        {
			await semaphore.WaitAsync();
			entries.Add(entry);
            semaphore.Release();
		}

        public Task<List<Entry>> Get(DateTime since, string? sender = null)
		{
			return Task.FromResult(entries.Where(o => o.Time >= since && (sender != null ? sender == o.Sender : true)).ToList());
		}
    }
}
