using ProcMon;

namespace WorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> logger;
        private ProcessMonitor processMonitor;
        public Worker(ProcessMonitor processMonitor, ILogger<Worker> logger)
        {
            this.processMonitor = processMonitor;

			this.logger = logger;
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
                            logger.LogInformation(str, DateTimeOffset.Now);
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
