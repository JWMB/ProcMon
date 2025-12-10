using Microsoft.AspNetCore.Authentication.Cookies;
using ProcServer.Services;

namespace ProcServer
{
    public class Startup
    {
		public void ConfigureServices(IServiceCollection services, IConfiguration config)
		{
			services.AddRazorPages(options => {
				//options.Conventions.AuthorizePage("/Processes");
			}).AddMvcOptions(options => options.Filters.Add<AuthorizePageHandlerFilter>());

			services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
				.AddCookie();

			services.AddControllers();

			var hurc = new HardcodedUserRepository.Config([]);
			config.GetSection("HardcodedUserRepository").Bind(hurc);
			services.AddSingleton(hurc);

			var preexistingEntries = new List<(DateTime, Message)>();
			var logFile = new FileInfo("log.log");
			if (logFile.Exists)
			{
				//var parser = new LogItemParser();
				preexistingEntries = Message.ParseLog(File.ReadAllText(logFile.FullName));
					// File.ReadAllLines(logFile.FullName)
					//.Select(parser.Parse)
					//.Select(dict =>
					//	System.Text.Json.JsonSerializer.Deserialize<ProcessEntry>(System.Text.Json.JsonSerializer.Serialize(dict)))
					//.OfType<ProcessEntry>()
					//.ToList();
			}

			services.AddSingleton<Func<IEnumerable<(DateTime, Message)>>>(sp => () => preexistingEntries);
			services.AddSingleton<IMessageRepository, InMemoryMessageRepository>();
			services.AddSingleton<ILogSink>(sp => new FileLogWriter(logFile));
			services.AddSingleton<ILogItemParser, LogItemParser>();
			services.AddSingleton<IUserRepository, HardcodedUserRepository>();
		}

		public void Configure(WebApplication app)
		{
			// Configure the HTTP request pipeline.
			if (!app.Environment.IsDevelopment())
			{
				app.UseExceptionHandler("/Error");
				// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
				app.UseHsts();
				app.UseHttpsRedirection();
			}

			app.UseRouting();

			//app.UseAuthentication();
			app.UseAuthorization();

			app.MapControllers();

			app.MapStaticAssets();
			app.MapRazorPages()
				.WithStaticAssets();
		}
	}
}
