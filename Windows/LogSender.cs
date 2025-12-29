using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace ProcMon
{
    // TODO: implement as OLTP
    public interface ILogSender
    {
        Task Send(IEnumerable<string> messages);
	}

    public class LogSender : ILogSender
	{
		public record Config(Uri Endpoint, int MinimumIntervalSeconds, string? Sender = null);
		
        private readonly Config config;
        private readonly Func<HttpClient> clientFactory; // IHttpClientFactory
        private readonly ILogger<LogSender> log;
        private List<string> unsent = new();
		private DateTime lastSent = DateTime.MinValue;
		private TimeSpan minimumInterval;
		private TimeSpan timeout;
		private SemaphoreSlim semaphore = new SemaphoreSlim(1);

		private int numConsecutiveFailures = 0;
		private readonly int disableAfterNumConsecutiveFailures = 3;
		private bool enabled = true;

		public LogSender(Config config, Func<HttpClient> clientFactory, ILogger<LogSender> log) //IHttpClientFactory 
		{
            this.config = config;
            this.clientFactory = clientFactory;
            this.log = log;
            minimumInterval = TimeSpan.FromSeconds(config.MinimumIntervalSeconds);
			timeout = TimeSpan.FromSeconds(2);
		}

		public async Task Send(IEnumerable<string> messages)
		{
			if (!enabled)
				return;
			await semaphore.WaitAsync();
			unsent.AddRange(messages);

			DateTime now = DateTime.UtcNow;
			if (now - lastSent > minimumInterval)
			{
				try
				{
					await SendInternal(unsent);
					lastSent = now;
					unsent.Clear();
					numConsecutiveFailures = 0;
				}
				catch (TaskCanceledException tcEx)
				{
					numConsecutiveFailures++;
					if (numConsecutiveFailures >= disableAfterNumConsecutiveFailures)
					{
						enabled = false;
						if (numConsecutiveFailures == disableAfterNumConsecutiveFailures)
							log.LogError($"Disabled after {numConsecutiveFailures} timeouts");
					}
				}
				catch (NotSupportedException nsEx)
				{
					log.LogError(nsEx, $"{messages.Count()} messages");
					Console.WriteLine(nsEx);
					enabled = false;
				}
				catch (Exception ex)
				{
					log.LogError(ex, $"{numConsecutiveFailures}");
					numConsecutiveFailures++;
					if (ex.Message.Contains("InternalServerError"))
					{
						enabled = false;
					}
					else
					{
					}
					Console.WriteLine(ex);
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
