namespace Common
{
	public interface IMessageRepository : IMessageReadOnlyRepository
	{
        Task Add(Entry entry);
    }

	public interface IMessageReadOnlyRepository
	{
		Task<List<Entry>> Get(DateTime since, string? sender = null);
	}

	public interface IMessageGetLastestRepository : IMessageReadOnlyRepository
	{
		Task<List<Entry>> Get();
	}
}
