// See https://aka.ms/new-console-template for more information
using Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProcMon;
using Windows;

// // dotnet publish -r win-x64 -p:PublishSingleFile=true --self-contained false
// dotnet publish ProcMon.csproj -r win-x64 -p:PublishSingleFile=true --self-contained false
// 

// sc.exe create PrcMon binPath= "C:\Program Files\Prcmon\ProcMon.exe"

//var file = @"C:\Users\JonasBeckeman\OneDrive\Dokument\act.log";
//file = @"C:\Users\JonasBeckeman\source\repos\JWMB\ProcMon\ProcMon\bin\Release\net10.0\win-x64\publish\activity.log";
//var aaa = ApplicationStats.Create(ApplicationStats.ParseLog(File.ReadAllText(file)));
//var tmp = System.Text.Json.JsonSerializer.Serialize(aaa, new System.Text.Json.JsonSerializerOptions() { WriteIndented = true });
var config = new FileMessageRepository.Config(new FileInfo(@"C:\Users\JonasBeckeman\Downloads\log.log"));
//var aoa = new FileMessageRepository(config);
var sessions = (await FileMessageRepository.GetEntriesFromFile(config.File))
	.AsSessions(TimeSpan.FromHours(0.5))
	.Where(o => o.Duration > TimeSpan.FromMinutes(5))
	.ToList();
var task = new WindowsEventLog().Listen(e => Console.WriteLine($"EEE {e.TaskDisplayName} {e.LogName} {e.ProviderName}"), CancellationToken.None);

var start = new WindowsEventLog().GetLatestStart(DateTime.UtcNow.AddDays(-1));

HostApplicationBuilder? hostBuilder = null;

IServiceCollection serviceCollection;
IConfiguration configuration;
if (true)
{
	hostBuilder = Host.CreateApplicationBuilder();
	ConfigureConfig(hostBuilder.Configuration);
	serviceCollection = hostBuilder.Services;
	configuration = hostBuilder.Configuration;
}
else
{
	var builder = new ConfigurationBuilder();
	ConfigureConfig(builder);
	serviceCollection = new ServiceCollection();
	configuration = builder.Build();
}


var startup = new Startup(null);
startup.AddServices(serviceCollection, configuration);

serviceCollection.AddHostedService<Worker>();

var host = hostBuilder?.Build();
//var config = services.GetRequiredService<IConfigurationRoot>();
//throw new Exception($"OK {config["LogFile"]} {string.Join(", ", args)}");

//var config = builder.Build();
//IServiceCollection services = new ServiceCollection();


var services = serviceCollection.BuildServiceProvider();
//IServiceProvider services = host.Services;

startup.ConfigureApp(services);

var logger = services.GetRequiredService<ILogger<Program>>();
var exiting = false;

var cancelSource = new CancellationTokenSource();
AppDomain.CurrentDomain.ProcessExit += (sender, e) => ProcessExit("ProcessExit");
Console.CancelKeyPress += (sender, e) => ProcessExit("Cancel");

Task mainTask;
if (host == null)
{
	var monitor = services.GetRequiredService<ProcessMonitor>();
	mainTask = monitor.Run(cancelSource.Token);
}
else
{
	mainTask = host.RunAsync(cancelSource.Token);
}

//args = ["a"];
if (args.Any())
{
	if (args.First() == "a")
	{
		//var monitor = services.GetRequiredService<ProcessMonitor>();
		var sender = services.GetRequiredService<ILogSender>();

		var msg = $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.ff} {System.Text.Json.JsonSerializer.Serialize(new Message("Start", "Test", "MyTest"))}";
		await sender.Send([msg]);
	}
}

await Task.Run(async () =>
{
	await Task.Delay(100);
	Hider.HideMainWindow();
});

await mainTask;

Console.WriteLine("Final exit");

void ConfigureConfig(IConfigurationBuilder builder)
{
	//	//.SetBasePath(env.ContentRootPath)
	builder
		.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
		.AddUserSecrets<Program>()
		.AddCommandLine(options =>
		{
			if (args.Length == 0)
				return;
			if (args.Length == 1)
				options.Args = ["-f", args[0]];

			options.SwitchMappings = new Dictionary<string, string>
			{
				["-f"] = "LogFile"
			};
		});
}

void ProcessExit(string message)
{
	message = $"Exit with {message} {(exiting ? "already exiting" : "")}";
	Console.WriteLine(message);
	logger.Log(LogLevel.Information, message);
	cancelSource.Cancel();
	if (exiting)
		Environment.Exit(1);
	//	message += " - already exiting";
	exiting = true;

	//logger.Log(LogLevel.Information, $"CLOSE APP {message}");
	//logger.Dispose();
	//Log($"CLOSE APP {message}").Wait();
	//sw.FlushAsync().Wait();
}
