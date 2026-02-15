namespace Common.MessageRepositories
{
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
