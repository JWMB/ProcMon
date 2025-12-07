using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Windows;

namespace ProcMon
{
    public class Startup
    {
        private StartupLogging logging;
        public Startup(IHostEnvironment? host)
        {
			logging = new StartupLogging();
		}

		public void AddServices(IServiceCollection services, IConfiguration configuration)
        {
			services.AddHttpClient();
			services.AddSingleton<Func<HttpClient>>(sp => () => sp.GetRequiredService<IHttpClientFactory>().CreateClient());

			services.AddSingleton<IDirectiveListener, DirectiveListener>();

			logging.AddServices(services, configuration);
		}

		public void ConfigureApp(IServiceProvider serviceProvider) // IHost, IDisposable, IApplicationBuilder
		{
			logging.ConfigureApp(serviceProvider);
		}
	}
}
