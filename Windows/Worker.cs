using Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProcMon;

namespace Windows
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> logger;
        private readonly ILogSender logSender;
        private ProcessMonitor processMonitor;
        public Worker(ProcessMonitor processMonitor, ILogger<Worker> logger, ILogSender logSender)
        {
            this.processMonitor = processMonitor;

			this.logger = logger;
            this.logSender = logSender;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var messages = await processMonitor.Collect();
                if (true || logger.IsEnabled(LogLevel.Information))
                {
                    if (messages.Any())
                    {
                        foreach (var message in messages)
                        {
                            var str = System.Text.Json.JsonSerializer.Serialize(message);
                            var timestamp = DateTimeOffset.Now;
							logger.LogInformation(str);
							await logSender.Send([$"{timestamp:yyyy-MM-ddTHH:mm:ss.ff} {System.Text.Json.JsonSerializer.Serialize(message)}"]);
						}
					}
                    else
                    {
						//logger.LogInformation("N/A");
					}
				}
                try
                {
					await Task.Delay(1000, stoppingToken);
				}
                catch { }
			}
        }
    }
}
