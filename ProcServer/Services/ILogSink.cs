namespace ProcServer.Services
{
    public interface ILogSink
    {
        Task Write(IEnumerable<string> messages);
    }

	public class FileLogWriter : ILogSink, IDisposable
	{
		private readonly Stream stream;
		private readonly StreamWriter writer;

		private DateTime lastWrite = DateTime.MinValue;
		public FileLogWriter(FileInfo file)
		{
			stream = file.OpenWrite();
			writer = new StreamWriter(stream);
		}

		public void Dispose()
		{
			writer.Dispose();
			stream.Dispose();
		}

		public async Task Write(IEnumerable<string> messages)
		{
			foreach (var item in messages)
			{
				await writer.WriteLineAsync(item);
			}
			if (DateTime.UtcNow - lastWrite > TimeSpan.FromMinutes(1))
				await writer.FlushAsync();
			lastWrite = DateTime.UtcNow;
		}
	}
}
