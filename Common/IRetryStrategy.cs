using System.Net;

namespace Common
{
	public interface IRetryStrategy
	{
		bool ShouldTry { get; }
		void RegisterResult(Exception? exception);
	}

	public class RetryStrategy : IRetryStrategy
	{
		private readonly Config config;
		private List<(DateTime, Exception?)> history = new();

		public record Config(
			int DisableAfterNumConsecutiveFailures = 3,
			TimeSpan? IntervalBeforeRetry = null);

		public RetryStrategy(Config config)
		{
			this.config = config;
		}

		private int NumConsecutiveFailures => history.TakeLastWhile(o => o.Item2 != null).Count();

		private bool IsRetryable(Exception ex)
		{
			return ex is TaskCanceledException ||
				(ex is HttpRequestException hre 
					&& hre.StatusCode.HasValue 
					&& new[] { HttpStatusCode.BadGateway, HttpStatusCode.ServiceUnavailable }.Contains(hre.StatusCode.Value));
		}

		public bool ShouldTry
		{
			get
			{
				if (NumConsecutiveFailures < config.DisableAfterNumConsecutiveFailures)
					return true;

				var last = history.Last();

				if (last.Item2 == null)
					return true;

				if (IsRetryable(last.Item2))
				{
					if (config.IntervalBeforeRetry != null)
					{
						var timeSinceLast = DateTime.UtcNow - last.Item1;
						if (timeSinceLast > config.IntervalBeforeRetry)
							return true;
					}
				}
				return false;
			}
		}

		public void RegisterResult(Exception? exception)
		{
			history.Add((DateTime.UtcNow, exception));
			if (history.Count > 10)
				history.RemoveAt(1);

			//if (exception is TaskCanceledException tcEx)
			//{
			//	if (NumConsecutiveFailures >= config.DisableAfterNumConsecutiveFailures)
			//	{
			//		//if (numConsecutiveFailures == disableAfterNumConsecutiveFailures)
			//		//log.LogError($"Disabled after {numConsecutiveFailures} timeouts");
			//	}
			//}
			//else if (exception is NotSupportedException nsEx)
			//{
			//	//log.LogError(nsEx, $"{messages.Count()} messages");
			//	Console.WriteLine(nsEx);
			//}
			//else
			//{
			//	//log.LogError(ex, $"{numConsecutiveFailures}");
			//	if (exception.Message.Contains("InternalServerError"))
			//	{
			//	}
			//	else
			//	{
			//		// TODO: Ignore this exception?
			//	}
			//	Console.WriteLine(exception);
			//}
		}
	}
}
