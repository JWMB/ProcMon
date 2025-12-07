using System.Text.RegularExpressions;

namespace ProcServer.Services
{
    public interface ILogItemParser
	{
		Dictionary<string, object> Parse(string message);
	}

	public class LogItemParser : ILogItemParser
	{
		private Dictionary<int, Application> applications = new();
		private int LastIdFocus = -1;

		public Dictionary<string, object> Parse(string message)
		{
			var rx = new Regex(@"(?<timestamp>[\w:-]+)\s*(?<message>.+)");
			var m = rx.Match(message);
			var result = new Dictionary<string, object>();
			if (m.Success)
			{
				var timestamp = DateTime.Parse(m.Groups["timestamp"].Value);
				result.Add("timestamp", timestamp);
				var msg = m.Groups["message"].Value;
				var parsed = System.Text.Json.JsonSerializer.Deserialize<Message>(msg);
				if (parsed != null)
				{
					if (!applications.TryGetValue(parsed.Id, out var app))
					{
						app = new Application { Name = parsed.Application, Start = timestamp, Title = parsed.Title, Id = parsed.Id };
						applications.Add(parsed.Id, app);
						Console.WriteLine($"Started {app.Name} ({app.Id})");
					}
					if (parsed.Action == "Focus")
					{
						if (LastIdFocus > -1)
						{
							if (applications.TryGetValue(parsed.Id, out var lastApp) && lastApp.Focus.Any())
							{
								lastApp.Focus.Last().Duration = timestamp - lastApp.Focus.Last().Start;
								Console.WriteLine($"{lastApp.Name} {lastApp.Focus.Last().Duration} ({lastApp.TotalDuration})");
							}
							else
							{
								Console.WriteLine($"No app found {LastIdFocus}");
							}
						}
						app.Focus.Add(new Application.StartDuration { Start = timestamp });
						LastIdFocus = app.Id;
					}
					result.Add("message", parsed);
				}
				else
					result.Add("message", msg);
			}
			else
			{
				Console.WriteLine("no rx match");
			}
			return result;
		}


		public class Application
		{
			public int Id { get; set; }
			public required string Name { get; set; }
			public required string Title { get; set; }
			public required DateTime Start { get; set; }

			public List<StartDuration> Focus { get; set; } = new();
			public TimeSpan TotalDuration => Focus.Any() ? TimeSpan.FromSeconds(Focus.Select(o => o.Duration?.TotalSeconds ?? 0).Sum()) : TimeSpan.Zero;
			public class StartDuration
			{
				public DateTime Start { get; set; }
				public TimeSpan? Duration { get; set; }
			}
		}
	}
}
