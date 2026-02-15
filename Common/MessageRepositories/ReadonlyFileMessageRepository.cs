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

			var rx = new System.Text.RegularExpressions.Regex(@"(?<=>)(?<date>\d{4}-\d{2}-\d{2})[T\s]?(?<time>\d{2}:\d{2}:\d{2}(\.\d{1,4})?)");

			var dict = new SortedDictionary<long, DateTime>();
			while (true)
			{
				var pos = (start + end) / 2;
				sr.BaseStream.Position = pos;
				var s = await retry.ExecuteOrDefault(async () =>
				{
					var p1 = sr.BaseStream.Position;
					var l1 = await sr.ReadLineAsync();
					if (l1?.Any() == true)
					{
						var m = rx.Match(l1);
						if (m.Success)
						{
							return l1;
						}
					}

					var p2 = sr.BaseStream.Position;
					var l2 = await sr.ReadLineAsync();
					var p3 = sr.BaseStream.Position;

					if (l2 != null)
					{
						if (System.Text.RegularExpressions.Regex.Matches(l2, @"\{").Count > 1 || l2.Contains("\n"))
						{ }
						var tmp = Message.ParseLogLine(l2);
						return l2;
					}
					return string.Empty;
				}, null);

				if (s == null)
					return null;

				if (end - start < 50)
					return start;

				var parsed = Message.ParseLogLine(s);
				DateTime date;
				if (parsed.HasValue)
				{
					date = parsed.Value.Item1;
				}
				else
				{
					var dateMatch = rx.Match(s);
					if (dateMatch.Success)
						date = DateTime.Parse(dateMatch.Value);
					else
						return null;
				}
				dict.Add(pos, date);

				if (findDate < date)
					end = pos;
				else if (findDate > date)
					start = pos;
				else
					return pos; // dict.Keys.First(o => o < pos);
			}
		}

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
					var pos = await FindDateStart(sr, since);

					if (pos != null)
					{
						sr.BaseStream.Position = pos.Value;
						var str = await retry.ExecuteOrDefault(async () => await sr.ReadToEndAsync() ?? "", null);
						if (str?.Any() == true)
						{
							var entries = Message.ParseLog(str).Select(o => new Entry(o.Item1, o.Item2, null));
							result = entries.ToList();
						}
					}
				}
			}

			if (!wasOpen && !config.KeepFileOpen)
				Close();

			semaphore.Release();

			return result ?? [];
		}
	}
}
