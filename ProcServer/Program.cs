using Common;
using ProcServer;

var builder = WebApplication.CreateBuilder(args);
var conf = builder.Configuration;
var env = conf["ASPNETCORE_ENVIRONMENT"].IfNullOrEmpty(conf["ENVIRONMENT"] ?? "");
conf.Sources.Clear();
conf.AddJsonFile("appsettings.json");
conf.AddJsonFile($"appsettings.{env}.json", optional: true);
conf.AddJsonFile($"appsettings.{env}-secrets.json", optional: true);
conf.AddEnvironmentVariables(); // prefix: "ASPNETCORE_"
conf.AddCommandLine(args);

var startup = new Startup();

startup.ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

startup.Configure(app);

app.Run();
