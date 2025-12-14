namespace ProcServer.Services
{
    public interface IMessageRepository
    {
        Task Add(DateTime time, Message message);
        Task<List<(DateTime, Message)>> Get(DateTime since);
    }

    public class InMemoryMessageRepository : IMessageRepository
	{
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private List<(DateTime, Message)> entries = new();
		public InMemoryMessageRepository(Func<IEnumerable<(DateTime, Message)>> init)
        {
			entries = init().ToList();
		}

        public async Task Add(DateTime time, Message message)
        {
			await semaphore.WaitAsync();
			entries.Add((time, message));
            semaphore.Release();
		}

        public Task<List<(DateTime, Message)>> Get(DateTime since)
		{
			return Task.FromResult(entries.Where(o => o.Item1 >= since).ToList());
		}
    }
}
