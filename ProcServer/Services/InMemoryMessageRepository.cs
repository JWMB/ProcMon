namespace ProcServer.Services
{
    public interface IMessageRepository
    {
        Task<List<(DateTime, Message)>> Get(DateTime since);
    }

    public class InMemoryMessageRepository : IMessageRepository
	{
        private List<(DateTime, Message)> entries = new();
		public InMemoryMessageRepository(Func<IEnumerable<(DateTime, Message)>> init)
        {
			entries = init().ToList();
		}

        public Task<List<(DateTime, Message)>> Get(DateTime since)
		{
			return Task.FromResult(entries.Where(o => o.Item1 >= since).ToList());
		}
    }
}
