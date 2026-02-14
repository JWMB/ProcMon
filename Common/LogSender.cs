using Common;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace Common
{
    // TODO: implement as OLTP
    public interface ILogSender
    {
        Task Send(IEnumerable<string> messages);
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
			retryStrategy = new RetryStrategy(new(DisableAfterNumConsecutiveFailures: 3, IntervalBeforeRetry: TimeSpan.FromMinutes(2)));
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
					// TODO: Maybe add as an unsent message - interesting for server to know about it
					//unsent.Add()
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
				throw new HttpRequestException(message: content, null, statusCode: res.StatusCode);
				//throw new Exception($"{res.StatusCode}: {content}");
			}
		}
	}
}
