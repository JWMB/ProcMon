using Common;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace ProcMon
{
    // TODO: implement as OLTP
    public interface ILogSender
    {
        Task Send(IEnumerable<string> messages);
	}

	public interface IRetryStrategy
	{
		bool ShouldTry { get; }
		void RegisterResult(Exception? exception);
	}
	public class RetryStrategy : IRetryStrategy
	{
		private int disableAfterNumConsecutiveFailures;
		private List<(DateTime, Exception?)> history = new();

		public RetryStrategy(int disableAfterNumConsecutiveFailures = 3)
		{
			this.disableAfterNumConsecutiveFailures = disableAfterNumConsecutiveFailures;
		}

		private int NumConsecutiveFailures => history.TakeLastWhile(o => o.Item2 != null).Count();

		private bool IsRetryable(Exception ex)
		{
			return ex is TaskCanceledException; // TODO: timeout
		}

		public bool ShouldTry
		{
			get
			{
				if (NumConsecutiveFailures < disableAfterNumConsecutiveFailures)
					return true;

				var last = history.Last();
				if (last.Item2 == null)
					return true;

				if (IsRetryable(last.Item2))
				{
					var timeSinceLast = DateTime.UtcNow - last.Item1;
					if (timeSinceLast > TimeSpan.FromMinutes(2))
						return true;
				}
				return false;
			}
		}

		public void RegisterResult(Exception? exception)
		{
			history.Add((DateTime.UtcNow, exception));
			if (history.Count > 10)
				history.RemoveAt(1);

			if (exception == null)
			{
				//numConsecutiveFailures = 0;
			}
			else
			{
				if (exception is TaskCanceledException tcEx)
				{
					//numConsecutiveFailures++;
					if (NumConsecutiveFailures >= disableAfterNumConsecutiveFailures)
					{
						//if (numConsecutiveFailures == disableAfterNumConsecutiveFailures)
							//log.LogError($"Disabled after {numConsecutiveFailures} timeouts");
					}
				}
				else if (exception is NotSupportedException nsEx)
				{
					//log.LogError(nsEx, $"{messages.Count()} messages");
					Console.WriteLine(nsEx);
				}
				else
				{
					//log.LogError(ex, $"{numConsecutiveFailures}");
					if (exception.Message.Contains("InternalServerError"))
					{
					}
					else
					{
						// TODO: Ignore this exception?
					}
					Console.WriteLine(exception);
				}
			}
		}
	}


	public class LogSender : ILogSender
	{
		public record Config(Uri Endpoint, int MinimumIntervalSeconds, string? Sender = null, bool Active = true);
		
        private readonly Config config;
        private readonly Func<HttpClient> clientFactory; // IHttpClientFactory
        private readonly ILogger<LogSender> log;
        private List<string> unsent = new();
		private DateTime lastSent = DateTime.MinValue;
		private TimeSpan minimumInterval;
		private TimeSpan timeout;
		private SemaphoreSlim semaphore = new SemaphoreSlim(1);

		private bool enabled = true;
		private readonly IRetryStrategy retryStrategy;

		public LogSender(Config config, Func<HttpClient> clientFactory, ILogger<LogSender> log)
		{
            this.config = config;
            this.clientFactory = clientFactory;
            this.log = log;
            minimumInterval = TimeSpan.FromSeconds(config.MinimumIntervalSeconds);
			timeout = TimeSpan.FromSeconds(2);

			enabled = config.Active;

			log.LogInformation($"Enabled:{enabled} Timeout:{timeout}");
			retryStrategy = new RetryStrategy();
		}

		public async Task Send(IEnumerable<string> messages)
		{
			if (!enabled)
				return;
			if (!retryStrategy.ShouldTry)
				return;

			await semaphore.WaitAsync();
			unsent.AddRange(messages);

			DateTime now = DateTime.UtcNow;
			if (now - lastSent > minimumInterval)
			{
				try
				{
					await SendInternal(unsent);
					retryStrategy.RegisterResult(null);
					lastSent = now;
					unsent.Clear();
				}
				catch (Exception ex)
				{
					retryStrategy.RegisterResult(ex);
					//log.LogError("", ex);
					Console.WriteLine(ex);
					if (!retryStrategy.ShouldTry)
						log.LogError($"Disabled");
				}
			}
			semaphore.Release();
		}

		private async Task SendInternal(IEnumerable<string> messages)
        {
			messages = messages.Select(o => o.Trim()).Where(o => o.Any());
			if (!messages.Any())
				return;
			var client = clientFactory();
			var request = new HttpRequestMessage(HttpMethod.Post, config.Endpoint);

			var body = new
			{
				Messages = messages,
				config.Sender
			};
			request.Content = JsonContent.Create(body);

			var src = new CancellationTokenSource();
			src.CancelAfter(timeout);
			var res = await client.SendAsync(request, src.Token);

			if (!res.IsSuccessStatusCode)
			{
				var content = await res.Content.ReadAsStringAsync();
				throw new Exception($"{res.StatusCode}: {content}");
			}
		}
	}
}
