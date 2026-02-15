namespace Common.MessageRepositories
{
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
}
