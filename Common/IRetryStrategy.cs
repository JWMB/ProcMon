namespace Common
{
	public interface IRetryStrategy
	{
		Task<TResult> Execute<TResult>(Func<Task<TResult>> action, Func<List<(TResult?, Exception?)>, bool>? retryable = null);
		Task<TResult?> ExecuteOrDefault<TResult>(Func<Task<TResult>> action, TResult? defaultValue, Func<List<(TResult?, Exception?)>, bool>? retryable = null);
	}

	public static class IRetryStrategyExtensions
	{
		public static async Task ExecuteNoReturn(this IRetryStrategy strategy, Func<Task> action, Func<List<Exception>, bool>? retryable = null)
		{
			Func<List<(bool, Exception?)>, bool>? adaptedRetryable = retryable == null ? null
				: lst => {
					return retryable(lst.Select(o => o.Item2).OfType<Exception>().ToList());
				};
			await strategy.Execute(async () => {
				await action();
				return true;
			}, adaptedRetryable);
		}

	}

	public class RetryStrategy : IRetryStrategy
	{
		private readonly Config config;

		public record Config(TimeSpan Pause, int MaxTries);
		public RetryStrategy(Config config)
		{
			this.config = config;
		}

		public async Task<TResult?> ExecuteOrDefault<TResult>(Func<Task<TResult>> action, TResult? defaultValue, Func<List<(TResult?, Exception?)>, bool>? retryable = null)
		{
			try
			{
				return await Execute(action, retryable);
			}
			catch
			{
				return defaultValue; 
			}
		}

		public async Task<TResult> Execute<TResult>(Func<Task<TResult>> action, Func<List<(TResult?, Exception?)>, bool>? retryable = null)
		{
			List<(TResult?, Exception?)> history = new();

			for (int i = 0; i < config.MaxTries; i++)
			{
				TResult result;
				//bool canRetry = true;
				try
				{
					result = await action();
					if (retryable == null)
						return result;

					if (!ShouldRetry(result, null))
						return result;
				}
				catch (Exception ex)
				{
					if (!ShouldRetry(default, ex))
						throw;
				}

				await Task.Delay(config.Pause);
			}

			throw new Exception("Never succeeded");

			bool ShouldRetry(TResult? result, Exception? ex)
			{
				if (retryable == null)
					return true;

				history.Add((result, ex));
				return retryable(history);
			}
		}
	}
}
