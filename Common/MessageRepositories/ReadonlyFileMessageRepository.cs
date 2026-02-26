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

		private async Task<long?> FindDateStart(StreamReader sr, DateTime findDate)
		{
			var retry = new RetryStrategy(new(TimeSpan.FromMilliseconds(100), 3));

			var start = 0L;
			var end = sr.BaseStream.Length;
			if (positionToDate.Any())
			{
				var after = positionToDate.FirstOrDefault(o => o.Value >= findDate);
				if (after.Value != default)
				{
					if (after.Value == findDate)
						return after.Key;
					end = after.Key;

					var before = positionToDate.LastOrDefault(o => o.Value < findDate);
					if (before.Value != default)
						start = before.Key;
				}
			}

			while (true)
			{
				var pos = (start + end) / 2;
				sr.DiscardBufferedData();
				sr.BaseStream.Seek(pos, SeekOrigin.Begin);
				var date = await retry.ExecuteOrDefault(async () =>
				{
					for (int i = 0; i <= 1; i++)
					{
						var p1 = sr.BaseStream.Position;
						var l1 = await sr.ReadLineAsync();
						if (l1?.Any() == true)
						{
							var tmp = Message.ParseDate(l1);
							if (tmp != null)
								return tmp;
						}
					}
					return null;
				}, null);

				if (date == null)
					return null;

				if (end - start <= 300)
					return start;

				positionToDate[pos] = date.Value;

				if (findDate < date)
					end = pos;
				else if (findDate > date)
					start = pos;
				else
					return pos; // dict.Keys.First(o => o < pos);
			}
		}

		private SortedDictionary<long, DateTime> positionToDate = new();
		private List<Entry> cached = new();

		private SemaphoreSlim semaphore = new SemaphoreSlim(1);
		public async Task<List<Entry>> Get(DateTime since, string? sender = null)
		{
			await semaphore.WaitAsync();
			var retry = new RetryStrategy(new(TimeSpan.FromMilliseconds(100), 3));

			var wasOpen = sr != null;
			//await retry.ExecuteNoReturn(async () => await Open());
			await Òpen();

			List<Entry>? result = null;
			if (sr != null)
			{
				if (sr.BaseStream.CanSeek)
				{
					long? startPos = null;
					if (cached.Any())
					{
						if (cached.First().Time > since)
						{
							// TODO:
						}
						else
						{
							startPos = positionToDate.Last().Key;
						}
					}

					startPos ??= await FindDateStart(sr, since);

					if (startPos != null && startPos.Value < sr.BaseStream.Length)
					{
						sr.BaseStream.Seek(startPos.Value, SeekOrigin.Begin);
						sr.DiscardBufferedData();
						var str = await retry.ExecuteOrDefault(async () => await sr.ReadToEndAsync() ?? "", null);
						if (str?.Any() == true)
						{
							var entries = Message.ParseLog(str).Select(o => new Entry(o.Item1, o.Item2, null));
							if (entries.Any())
							{
								positionToDate[sr.BaseStream.Position] = entries.Last().Time;
								cached.AddRange(entries.Except(cached));
							}
						}
					}
					result = cached.Where(o => o.Time >= since).ToList(); // entries.ToList();
				}
			}

			if (!wasOpen && !config.KeepFileOpen)
				Close();

			semaphore.Release();

			return result ?? [];
		}
	}
}
