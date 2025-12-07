using ProcServer.Services;

namespace ProcServer
{
    public class Startup
    {
		public void ConfigureServices(IServiceCollection services)
		{
			// Add services to the container.
			services.AddRazorPages();
			services.AddControllers();

			services.AddSingleton<ILogSink>(sp => new FileLogWriter(new FileInfo("log.log")));
			services.AddSingleton<ILogItemParser, LogItemParser>();
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

			app.UseAuthorization();

			app.MapControllers();

			app.MapStaticAssets();
			app.MapRazorPages()
			   .WithStaticAssets();
		}
	}
}
