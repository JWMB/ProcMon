namespace Common.MessageRepositories
{
	public class CachingReadonlyRepository : IMessageGetLastestRepository
	{
		private readonly IMessageReadOnlyRepository inner;
		private (DateTime LastRetrieval, DateTime FirstRetrieval, List<Entry> Entries) cached;

		public CachingReadonlyRepository(IMessageReadOnlyRepository inner)
		{
			this.inner = inner;
            cached = (DateTime.MinValue, DateTime.MinValue, []);
		}

        public Task<List<Entry>> Get(DateTime since, string? sender = null) => inner.Get(since, sender);

		public async Task<List<Entry>> Get()
		{
			var when = DateTime.UtcNow;
			var data = await inner.Get(cached.LastRetrieval);
			if (data != null)
				cached.Entries.AddRange(data);
			cached = (when, cached.FirstRetrieval, cached.Entries);

			return cached.Entries;
		}
	}
}
