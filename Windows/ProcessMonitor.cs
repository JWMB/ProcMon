using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ProcMon
{
	public class ProcessMonitor
	{
		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

		[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
		static extern IntPtr GetForegroundWindow();

		//private readonly Action<string> log; //ILogger<ProcessMonitor> log;
		private readonly ILogger<ProcessMonitor> log;
		private readonly IMetricsHandler? metrics;
		private readonly ILogSender logSender;
		private DateTime lastScreenshot = DateTime.MinValue;
		private DirectoryInfo? screenshotFolder;

		private Dictionary<int, ProcessEntry> dict = new();
		private ProcessEntry? activeProcess = null; // new ProcessEntry("", "", DateTime.MinValue, 0);
		private string? lastWindowTitle = null;

		public ProcessMonitor(ILogger<ProcessMonitor> log, ILogSender logSender, IMetricsHandler? metrics = null)
		{
			this.log = log;
			this.metrics = metrics;
			this.logSender = logSender;

			//var userinfo = $"{Process.GetCurrentProcess().MachineName} {Environment.UserName}";
			//screenshotFolder = new DirectoryInfo(@"C:\Users\JonasBeckeman\Desktop\Screenshots");
		}

		public async Task Run(CancellationToken cancellationToken)
		{
			//var logged = false;

			//Log("START APP");
			Log(new Message("Start", "APP", ""));
			while (true)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					//Log("CANCELLING...");
					Log(new Message("Stop", "APP", ""));
					break;
				}

				var messages = await Collect();
				foreach (var message in messages)
					Log(message);

				try
				{
					await Task.Delay(1000, cancellationToken);
				}
				catch (Exception ex)
				{
				}
			}
			//Console.WriteLine("Exited loop");
		}

		public async Task<List<Message>> Collect()
		{
			var result = new List<Message>();

			var processes = Process.GetProcesses();
			var withWindowHandle = processes.Where(o => o.MainWindowHandle > 0).ToArray();

			var procDict = withWindowHandle.ToDictionary(o => o.Id, ProcessEntry.From);

			var added = procDict.Where(o => dict.ContainsKey(o.Key) == false).Select(o => o.Value).ToList();
			var removed = dict.Where(o => procDict.ContainsKey(o.Key) == false).Select(o => o.Value).ToList();

			var forMetrics = added.Select(o => new { Added = true, Value = o }).Concat(removed.Select(o => new { Added = false, Value = o }));
			foreach (var item in forMetrics)
			{
				AddMessage(new Message(item.Added ? "Start" : "Stop", item.Value.ProcessName ?? "", item.Value.MainWindowTitle, item.Value.Id));
				if (metrics != null)
					metrics.AddToMetric("AppRunning", item.Added ? 1 : -1, "ProcessName", item.Value.ProcessName);
			}

			var currentActiveHandle = GetForegroundWindow();
			var currentActiveProcess = withWindowHandle.FirstOrDefault(o => o.MainWindowHandle == currentActiveHandle);
			if (currentActiveProcess == null)
			{
				var _ = GetWindowThreadProcessId(currentActiveHandle, out var processId);
				currentActiveProcess = withWindowHandle.FirstOrDefault(o => o.Id == processId);
			}

			if (screenshotFolder != null && (DateTime.Now - lastScreenshot).TotalSeconds > 10)
			{
				var filename = $"{currentActiveProcess?.ProcessName ?? ""}_{DateTime.Now:yyyyMMddHHmmss}.png";
				Screenshot.Save(Path.Join(screenshotFolder.FullName, filename), currentActiveProcess);
				lastScreenshot = DateTime.Now;
			}

			var c = currentActiveProcess == null ? null : ProcessEntry.From(currentActiveProcess);
			if (activeProcess != c)
			{
				if (metrics != null)
				{
					metrics.AddToMetric("Focus", -1, "ProcessName", activeProcess?.DisplayName);
					metrics.AddToMetric("Focus", 1, "ProcessName", c?.DisplayName);
				}
				activeProcess = c;
				AddMessage(new Message("Focus", activeProcess?.ProcessName ?? "", activeProcess?.MainWindowTitle ?? "", activeProcess?.Id ?? 0));
				lastWindowTitle = activeProcess?.MainWindowTitle;
				//Log($"Focus: {activeProcess?.DisplayName ?? "N/A"}");
			}
			else if (activeProcess != null && activeProcess.MainWindowTitle != lastWindowTitle)
			{
				AddMessage(new Message("Focus", activeProcess.ProcessName ?? "", activeProcess.MainWindowTitle ?? "", activeProcess.Id));
				lastWindowTitle = activeProcess.MainWindowTitle;
			}

			dict = procDict;

			return result;

			void AddMessage(Message m)
			{
				//Log(m);
				result.Add(m);
			}
		}
		//string ProcsToString(IEnumerable<ProcessEntry> entries)
		//	=> string.Join("\n", entries.Select(o => $"{o.DisplayName} ({o.StartTime:yyyy-MM-dd HH:mm})"));

		void Log(Message message)
		{
			var timestamp = $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}Z";

			logSender.Send([$"{timestamp} {System.Text.Json.JsonSerializer.Serialize(message)}"]);
			//message = message with { Title = string.Empty };
			log.Log(LogLevel.Information, $"{System.Text.Json.JsonSerializer.Serialize(message)}");
		}
	}

	public class Hider
	{
		[DllImport("user32.dll")]
		static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

		public static void HideMainWindow()
		{
			var current = Process.GetCurrentProcess();
			
			var h = current.MainWindowHandle;
			if (h == 0)
			{
				Console.WriteLine($"problemo!!! {current.Handle}: {current.ProcessName} {current.MainWindowTitle}");
				var result = ShowWindow(current.Handle, 0);
			}
			else
			{
				var result = ShowWindow(h, 0);
			}
		}
	}
}
