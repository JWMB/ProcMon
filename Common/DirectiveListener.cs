using Common.Models;

namespace Common
{
    public interface IDirectiveListener
    {
        Task<List<Directive>> Poll();
	}

    public class DirectiveConsumer
    {
        public async Task Consume(List<Directive> directives)
        {
            foreach (var directive in directives)
            {
                try
                {
					var cmd = ICommand.Create(directive.CommandData);
					await cmd.Execute();
				}
                catch (Exception ex)
                {
					// Log error
				}
			}
        }
    }

	public class DirectiveListener : IDirectiveListener
	{
        public record Config(Uri Endpoint);

        private readonly Config config;
        private readonly Func<HttpClient> clientFactory;

        public DirectiveListener(Config config, Func<HttpClient> clientFactory)
        {
            this.config = config;
            this.clientFactory = clientFactory;
        }

        public async Task<List<Directive>> Poll()
        {
            var client = clientFactory();

            var req = new HttpRequestMessage(HttpMethod.Get, config.Endpoint);
            var res = await client.SendAsync(req);
            var content = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                return [];
            }
            var directives = System.Text.Json.JsonSerializer.Deserialize<List<Directive>>(content);
            if (directives == null)
            {
                return [];
            }

            var latest = directives.Max(o => o.Created);
			req = new HttpRequestMessage(HttpMethod.Delete, $"{config.Endpoint}?until={latest}");
			await client.SendAsync(req);

            return directives;
		}
	}
}
