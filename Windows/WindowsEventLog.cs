using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.CompilerServices;

namespace Windows
{
	public class WindowsEventLogWatcher : IDisposable
	{
		private EventLogWatcher watcher;
		public WindowsEventLogWatcher(EventLogQuery query)
		{
			// 			var query = new EventLogQuery("Security", PathType.LogName, "*[System/EventID=4624]");
			watcher = new EventLogWatcher(query);
			watcher.EventRecordWritten += new EventHandler<EventRecordWrittenEventArgs>(EventLogEventRead);
			watcher.Enabled = true;
		}

		public void Dispose()
		{
			watcher.Enabled = false;
			watcher.Dispose();
		}

		private void EventLogEventRead(object? obj, EventRecordWrittenEventArgs arg)
		{
			// Make sure there was no error reading the event.
			if (arg.EventRecord == null)
				return;

			// XPath reference strings to select the properties that we want to display
			var xPathRefs = new[]
			{
				"Event/System/TimeCreated/@SystemTime",
				"Event/System/Computer",
				"Event/EventData/Data[@Name=\"TargetUserName\"]",
				"Event/EventData/Data[@Name=\"TargetDomainName\"]"
			};
			var logPropertyContext = new EventLogPropertySelector(xPathRefs);

			var logEventProps = ((EventLogRecord)arg.EventRecord).GetPropertyValues(logPropertyContext);
			//Log("Time: ", logEventProps[0]);
			//Log("Computer: ", logEventProps[1]);
			//Log("TargetUserName: ", logEventProps[2]);
			//Log("TargetDomainName: ", logEventProps[3]);
			//Log("---------------------------------------");
			//Log("Description: ", arg.EventRecord.FormatDescription());
			//catch (EventLogReadingException e)
			//{
			//}
			//finally
			//{
			//	if (watcher != null)
			//	{
			//		watcher.Enabled = false;
			//		watcher.Dispose();
			//	}
			//}
		}
	}

	public class WindowsEventLog
	{
		public IEnumerable<EventLogEntry> GetFromSource(ILogSource source)
		{
			return new EventLog("System", Environment.MachineName, source.Name).ToEnumerable(); //.Where(o => o.Source == source.Name);
		}

		public IEnumerable<EventRecord> ReadFromLog(ILogSource? source, string query = "*")
		{
			var q = new EventLogQuery("System", PathType.LogName, query);
			using var reader = new EventLogReader(q);
			EventRecord ev;
			var list = new List<EventRecord>();
			while ((ev = reader.ReadEvent()) != null)
				yield return ev;
		}

		public async Task Listen(Action<EventRecord> callback, CancellationToken cancellation)
		{
			using var system = new EventLogReader(new EventLogQuery("System", PathType.LogName));
			using var application = new EventLogReader(new EventLogQuery("Application", PathType.LogName));

			await foreach (var e in IAsyncEnumerableExtensions.Interleave(cancellation, new[] { system, application }.Select(o => StreamLog(o, cancellation)).ToArray()))
				callback(e);

			Console.WriteLine("EEENNNNNNDDDD!");
		}

		private async IAsyncEnumerable<EventRecord> StreamLog(EventLogReader reader, [EnumeratorCancellation] CancellationToken cancellation)
		{
			while (cancellation.IsCancellationRequested == false)
			{
				var e = await Task.Run(() => reader.ReadEvent(), cancellation);
				yield return e;
			}
		}

		private string CreateQueryString(DateTime? createdSince = null)
		{
			//var q = $"*[System/Provider/@Name=\"{KernelPowerLogSource.SourceName}\"]";
			// https://learn.microsoft.com/en-us/previous-versions/bb671200(v=vs.90)
			if (createdSince != null)
			{
				return $"*[System[TimeCreated[@SystemTime>='{createdSince.Value.ToUniversalTime().ToString("o")}']]]";
			}
			return "*";
		}

		public DateTime? GetLatestStart(DateTime since)
		{
			try
			{
				return ReadFromLog(new KernelPowerLogSource(), CreateQueryString(since))
					.Where(o => o.ProviderName == KernelPowerLogSource.SourceName
						&& (o.Task == (int)KernelPowerLogSource.Categories.ExitStandby || o.Id == (int)KernelPowerLogSource.Instance.ExitStandby)
						&& o.TimeCreated != null)
					.LastOrDefault()?.TimeCreated;
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
				return null;
			}
			//var eventLog = GetFromSource(new KernelPowerLogSource())
			//	.Convert()
			//	.OfType<KernelPowerLogEventEntry>()
			//	.Where(o => o.TimeGenerated >= since);

			//var tmp = new EventLog("System").ToEnumerable()
			//	.Where(o => o.TimeGenerated >= since).Convert()
			//	.GroupBy(o => o.Source)
			//	.ToDictionary(o => o.Key, o => o.GroupBy(p => p.CategoryNumber).ToDictionary(p => p.Key, p => p.ToList()));

			//var mostRecentWake = eventLog
			//	.Where(o => o.Category == KernelPowerLogSource.Categories.ExitStandby)
			//	.LastOrDefault();

			//var taoa = eventLog.GroupBy(o => o.InstanceId).ToDictionary(o => o.Key, o => o.ToList());
		}
	}

	public interface ILogSource
	{
		string Name { get; }
	}
	public class KernelPowerLogSource : ILogSource
	{
		public const string SourceName = "Microsoft-Windows-Kernel-Power";
		public string Name => SourceName;

		// Id 566 / Category 268 / Reason SessionUnlock 


		// https://stackoverflow.com/questions/62210954/the-description-for-event-id-x-in-source-microsoft-windows-kernel-power-cann
		public enum Categories
		{
			PowerSourceChange = 100,
			EnterStandby = 157,
			ExitStandby = 158,
			ConnectivityState = 203,
			Transition = 268
		}

		public enum Instance
		{
			PowerSourceChange = 105,
			ConnectivityState = 172,
			EnterStandby = 506,
			ExitStandby = 507,
			Transition = 566
		}
	}

	public class EventLogEntryEx
	{
		private readonly EventLogEntry entry;

		public EventLogEntryEx(EventLogEntry entry)
		{
			this.entry = entry;
		}
		public string Message => entry.Message;
		public string Source => entry.Source;
		public short CategoryNumber => entry.CategoryNumber;
		public DateTime TimeWritten => entry.TimeWritten;
		public DateTime TimeGenerated => entry.TimeGenerated;
		public string UserName => entry.UserName;
		//[Obsolete()]
		//public int EventId => entry.EventID;
		public long InstanceId => entry.InstanceId;

		//public required string Message { get; set; }
		//public required string Source { get; set; }
		//public short CategoryNumber { get; set; }
		//public DateTime TimeWritten { get; set; }
		//public DateTime TimeGenerated { get; set; }
		//public required string UserName { get; set; }

		public static EventLogEntryEx Create(EventLogEntry entry)
		{
			//return new EventLogEntryEx
			//{
			//	Source = entry.Source,
			//	Message = entry.Message,
			//	CategoryNumber = entry.CategoryNumber,
			//	TimeGenerated = entry.TimeGenerated,
			//	TimeWritten = entry.TimeWritten,
			//	UserName = entry.UserName
			//};
			return entry.Source switch
			{
				KernelPowerLogSource.SourceName => new KernelPowerLogEventEntry(entry),
				_ => new EventLogEntryEx(entry),
			};
		}

		public override string ToString() => $"{Source} {CategoryNumber} {TimeGenerated} {InstanceId} {Message}";
	}

	public class KernelPowerLogEventEntry : EventLogEntryEx
	{
		public KernelPowerLogEventEntry(EventLogEntry entry) : base(entry) { }
		public KernelPowerLogSource.Categories Category => (KernelPowerLogSource.Categories)CategoryNumber;

		public override string ToString() => $"{Source} {Category} {TimeGenerated} {InstanceId} {Message}";
	}


	public static class EventLogExtensions
	{
		public static IEnumerable<EventLogEntryEx> Convert(this IEnumerable<EventLogEntry> items)
			=> items.Select(EventLogEntryEx.Create);

		public static IEnumerable<EventLogEntry> ToEnumerable(this EventLog log)
		{
			foreach (EventLogEntry entry in log.Entries)
				yield return entry;
		}
	}
	public static class IAsyncEnumerableExtensions
	{
		public static async IAsyncEnumerable<T> Interleave<T>([EnumeratorCancellation] CancellationToken token, IAsyncEnumerable<T>[] sources)
		{
			// https://stackoverflow.com/questions/70710153/how-to-merge-multiple-asynchronous-sequences-without-left-side-bias
			if (sources.Length == 0)
				yield break;
			var enumerators = new List<(IAsyncEnumerator<T> e, Task<bool> t)>(sources.Length);
			try
			{
				for (var i = 0; i < sources.Length; i++)
				{
					var e = sources[i].GetAsyncEnumerator(token);
					enumerators.Add((e, e.MoveNextAsync().AsTask()));
				}

				do
				{
					var taskResult = await Task.WhenAny(enumerators.Select(tuple => tuple.t));
					var ind = enumerators.FindIndex(tuple => tuple.t == taskResult);
					var tuple = enumerators[ind];
					enumerators.RemoveAt(ind);
					if (taskResult.Result)
					{
						yield return tuple.e.Current;
						enumerators.Add((tuple.e, tuple.e.MoveNextAsync().AsTask()));
					}
					else
					{
						try
						{
							await tuple.e.DisposeAsync();
						}
						catch
						{ //
						}
					}
				} while (enumerators.Count > 0);
			}
			finally
			{
				for (var i = 0; i < enumerators.Count; i++)
				{
					try
					{
						await enumerators[i].e.DisposeAsync();
					}
					catch
					{ //
					}
				}
			}
		}
	}
}
	
