using Common;
using System.Text.RegularExpressions;


public record Session(DateTime Start, TimeSpan Duration, List<Session.AppTime> AppTimes)
{
	public record AppTime(string Application, TimeSpan Duration);
	public static List<Session> GetSessions(IEnumerable<Entry> entries, TimeSpan? skipSessionsShorterThan = null)
	{
		var tsRounding = TimeSpanExtensions.TimeSpanPart.Minutes;

		var result = entries
			.AsSessions(TimeSpan.FromHours(0.5))
			.Where(o => skipSessionsShorterThan == null ? true : o.Duration > skipSessionsShorterThan)
			.Select(o => new Session(
				o.Start,
				o.Duration,
				ApplicationStats.Create(o.Entries.Select(p => (p.Time, p.Message)))
					.Select(p => new AppTime(p.Application, ApplicationStats.GetTotalTime(p.Events).Modulo(TimeSpanExtensions.CreateFullModuloUntil(tsRounding)))).ToList()
				))
			.OrderByDescending(o => o.Start)
			.ToList();
		return result;
	}
}

public record Message(string Action, string Application, string Title, int Id = 0)
{
	public static List<(DateTime, Message)> ParseLog(string log)
	{
		return log.Split('\n').Select(o => o.Trim()).Where(o => o.Any())
			.Select(ParseLogLine)
			.OfType<(DateTime, Message)>().ToList();
	}

	public static (DateTime, Message)? ParseLogLine(string line)
	{
		//>2025-11-28 18:25:37: {"Action":"Focus","Application":"Taskmgr","Title":"","Id":9000}
		var m = Regex.Match(line, @"\d{4}-\d{2}-\d{2}(\s|T)\d{2}:\d{2}:\d{2}(\.\d+)?");
		if (!m.Success)
			return null;
		var timestamp = DateTime.Parse(m.Value);
		var jsonStart = line.IndexOf("{");
		Message? message = null;
		if (jsonStart >= 0)
		{
			message = System.Text.Json.JsonSerializer.Deserialize<Message>(line.Substring(jsonStart));
		}
		else
		{
			if (line.Contains("Sending HTTP request POST"))
			{
				message = new Message("Start", "APP", "");
			}
		}
		if (message == null)
			return null;
		return (timestamp, message);
	}

}

public class ApplicationStats
{
	public required string Application { get; set; }
	public List<Event> Events { get; set; } = new();

	public override string ToString() => $"{Application} {Events.Count}";


	public class Event
	{
		public DateTime Time { get; set; }
		public required string Action { get; set; }
		public string? Title { get; set; }

		public override string ToString() => $"{Time:dd/MM HH:mm:ss.fff} {Action} {Title}";
	}

	public static TimeSpan GetTotalTime(IEnumerable<Event> events)
	{
		var total = TimeSpan.Zero;
		var firstIndex = events.Index().FirstOrDefault(o => o.Item.Action != "Defocus").Index;
		var lastTime = events.Skip(firstIndex).First().Time;
		var lastAction = "";
		foreach (var e in events.Skip(firstIndex + 1))
		{
			if (lastAction != "Defocus")
				total += e.Time - lastTime;
			lastAction = e.Action;
			lastTime = e.Time;
		}
		return total;
	}

	public static List<ApplicationStats> Create(IEnumerable<(DateTime Timestamp, Message Message)> messages)
	{
		var focusEvents = messages.Where(o => o.Message.Action == "Focus")
			.Select((o, i) => new { o.Timestamp, o.Message.Application, Index = i })
			//.OrderBy(o => o.Timestamp)
			.ToList();

		return messages.GroupBy(o => o.Message.Application).Select(byApp =>
		{
			var appDefocusTimestamps = new List<DateTime>();
			var appFocusIndices = focusEvents.Where(o => o.Application == byApp.Key).Select(o => o.Index).ToList();
			if (appFocusIndices.Any())
			{
				var appDefocusIndices = appFocusIndices.Select(o => o + 1).ToList();
				if (appDefocusIndices.Last() >= focusEvents.Count)
					appDefocusIndices.RemoveAt(appDefocusIndices.Count - 1);
				appDefocusTimestamps = appDefocusIndices.Select(o => focusEvents[o].Timestamp).ToList();
			}

			var events = byApp
				.Select(o => new Event { Time = o.Timestamp, Action = o.Message.Action, Title = o.Message.Title })
				.Concat(appDefocusTimestamps.Select(o => new Event { Time = o, Action = "Defocus" }))
				.OrderBy(o => o.Time)
				.ToList();


			return new ApplicationStats
			{
				Application = byApp.Key,
				Events = events
			};
		}).ToList();
	}
};

