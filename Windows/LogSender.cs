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
		public record Config(Uri Endpoint, int MinimumIntervalSeconds);
		
        private readonly Config config;
        private readonly Func<HttpClient> clientFactory; // IHttpClientFactory

		private List<string> unsent = new();
		private DateTime lastSent = DateTime.MinValue;
		private TimeSpan minimumInterval;
		private TimeSpan timeout;
		private SemaphoreSlim semaphore = new SemaphoreSlim(1);

		private int numFailuresInARow = 0;
		private bool enabled = true;

		public LogSender(Config config, Func<HttpClient> clientFactory) //IHttpClientFactory 
		{
            this.config = config;
            this.clientFactory = clientFactory;
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
					numFailuresInARow = 0;
				}
				catch (TaskCanceledException tcEx)
				{
					numFailuresInARow++;
					if (numFailuresInARow > 3)
						enabled = false;
				}
				catch (NotSupportedException nsEx)
				{
					Console.WriteLine(nsEx);
					enabled = false;
				}
				catch (Exception ex)
				{
					numFailuresInARow++;
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
			var client = clientFactory(); //.CreateClient();
			var request = new HttpRequestMessage(HttpMethod.Post, config.Endpoint);

			var body = new { Messages = messages };
			request.Content = JsonContent.Create(body);

			var src = new CancellationTokenSource();
			src.CancelAfter(timeout);
			var res = await client.SendAsync(request, src.Token);

			var content = await res.Content.ReadAsStringAsync();
			if (!res.IsSuccessStatusCode)
			{
				throw new Exception($"{res.StatusCode}: {content}");
			}
		}
	}
}
