using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
//using OpenTelemetry.Logs;
//using OpenTelemetry.Metrics;
using System.Diagnostics.Metrics;

namespace ProcMon
{

	public class StartupLogging
	{
		public void AddServices(IServiceCollection services, IConfiguration configuration)
		{
			var c = new LogSender.Config(new("file://"), 0);
			configuration.GetSection("LogSender").Bind(c);
			services.AddSingleton(c);
			services.AddSingleton<ILogSender, LogSender>();

			var filepath = configuration["LogFile"]; //"activity.log";
			if (!string.IsNullOrEmpty(filepath))
			{
				// throw new ArgumentNullException("No log file path provided");
				var path = new FileInfo(filepath);
				var f = path.OpenWrite();
				f.Dispose();
				filepath = path.FullName;
			}

			services.AddSingleton<ProcessMonitor>();

			var otelSection = configuration.GetSection("OTel");
			var otelSettings = new { Endpoint = otelSection["OTEL_EXPORTER_OTLP_ENDPOINT"]!, Headers = otelSection["OTEL_EXPORTER_OTLP_HEADERS"]! };
			services.AddLogging(config =>
			{
				config.AddSystemdConsole(c =>
				{
					c.TimestampFormat = "yyyy-MM-dd HH:mm:ss";
				});
				if (!string.IsNullOrEmpty(filepath))
					config.AddAppendingFileLogger(c => c.Filepath = filepath);

				//config.AddOpenTelemetry(logging =>
				//{
				//	logging.AddOtlpExporter(c =>
				//	{
				//		c.ExportProcessorType = OpenTelemetry.ExportProcessorType.Batch;
				//		c.Endpoint = new Uri(otelSettings.Endpoint);
				//		c.Headers = otelSettings.Headers;
				//	});
				//	logging.AddProcessor(new TestProcessor());
				//	//logging.AddConsoleExporter(c => {
				//	//	c.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Console;
				//	//});
				//});
			});

			// https://www.mytechramblings.com/posts/getting-started-with-opentelemetry-metrics-and-dotnet-part-2/
			var customMeter = new Meter("ProcMon", "1.0");
			services.AddSingleton<IMetricsHandler>(sp => new OtelMetricsHandler(customMeter));

			//services.AddOpenTelemetry()
			//	.WithMetrics(config =>
			//	{
			//		config.AddMeter(customMeter.Name);
			//		//config.AddView();
			//		config.AddOtlpExporter(c =>
			//		{
			//			c.Endpoint = new Uri(otelSettings.Endpoint);
			//			c.Headers = otelSettings.Headers;
			//		});
			//	})
			//	.WithLogging(config =>
			//	{
			//	});
		}

		public void ConfigureApp(IServiceProvider serviceProvider)
		{
		}
	}

	//public class TestProcessor : OpenTelemetry.BaseProcessor<LogRecord>
	//{
 //       public override void OnStart(LogRecord data)
 //       {
 //           base.OnStart(data);
 //       }
 //       public override void OnEnd(LogRecord data)
 //       {
 //           base.OnEnd(data);
 //       }
	//}
}