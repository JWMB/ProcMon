using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using ProcMon;

var builder = Host.CreateApplicationBuilder(args);

var name = "PrcMn";
var logName = "myLog";
var logging = builder.Logging;
// dotnet publish WorkerService.csproj -r win-x64 -p:PublishSingleFile=true --self-contained false
// sc.exe create "Svc Name" binpath= "C:\Path\To\App.exe --contentRoot C:\Other\Path"
// sc.exe create "PrcMon" binPath= "C:\Program Files\Prcmon\WorkerService.exe" // --contentRoot C:\Other\Path
// --LogFile:"C:\Users\JonasBeckeman\source\repos\JWMB\ProcMon\WorkerService\bin\Release\net10.0\win-x64\publish\l.log"

//if (string.IsNullOrEmpty(builder.Configuration["LogFile"]))
//	builder.Configuration["LogFile"] = "Activity.log";
builder.Configuration["LogFile"] = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "Activity.log");

//throw new Exception($"dir '{Directory.GetCurrentDirectory()}' aa:{AppDomain.CurrentDomain.BaseDirectory} ss:{System.Reflection.Assembly.GetAssembly(typeof(Worker))?.Location}");
//Environment.GetEnvironmentVariable(Environment.SpecialFolder.C)


////builder.Logging.Auto
//if (!EventLog.SourceExists(name))
//{
//	var srcData = new EventSourceCreationData(name, logName);
//	srcData.MessageResourceFile = messageFile;
//	srcData.ParameterResourceFile = messageFile;
//	srcData.CategoryResourceFile = messageFile;
//	srcData.CategoryCount = 1;
//	EventLog.CreateEventSource(srcData);
//}

//builder.ConfigureLogging((hostingContext, logging) =>
{
	logging.ClearProviders();
	logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
	logging.AddEventLog(new EventLogSettings
	{
		SourceName = name,
		LogName = logName,
		Filter = (_, level) => level >= LogLevel.Information //.Warning
	});
	logging.AddConsole();
}
//);

var services = builder.Services;
services.AddWindowsService(options =>
{
	options.ServiceName = name;
});

services.AddHostedService<Windows.Worker>();
LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(services);


services.AddHttpClient();
services.AddSingleton<Func<HttpClient>>(sp => () => sp.GetRequiredService<IHttpClientFactory>().CreateClient());
services.AddSingleton<ProcessMonitor>();

var sl = new StartupLogging();
sl.AddServices(builder.Services, builder.Configuration);

var host = builder.Build();
host.Run();
