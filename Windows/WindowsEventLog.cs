using Common;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Windows
{
	public class WindowsEventLogWatcher : IDisposable
	{
		// TODO: https://learn.microsoft.com/en-us/dotnet/api/system.management.eventquery?view=net-10.0-pp&viewFallbackFrom=net-10.0

		private EventLogWatcher watcher;
		public WindowsEventLogWatcher(EventLogQuery query)
		{
			// var query = new EventLogQuery("Security", PathType.LogName, "*[System/EventID=4624]");
			watcher = new EventLogWatcher(query);
			watcher.EventRecordWritten += new EventHandler<EventRecordWrittenEventArgs>(EventLogEventRead);
			watcher.Enabled = true;
		}

        public WindowsEventLogWatcher(string logName, string? filter = null)
			: this(new EventLogQuery(logName, PathType.LogName, filter))
		{ }

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

			var kvs = xPathRefs.Select((o, i) => $"{Regex.Match(o, @".*\W(\w+)\W*")?.Value.IfNullOrEmpty(o)}:{logEventProps[i]}");
			Console.WriteLine(string.Join(", ", kvs));
			//Log("Description: ", arg.EventRecord.FormatDescription());
		}
	}

	public class EventLogQueryHelper
	{
		public static string CreateQueryString(DateTime? createdSince = null)
		{
			//var q = $"*[System/Provider/@Name=\"{KernelPowerLogSource.SourceName}\"]";
			// https://learn.microsoft.com/en-us/previous-versions/bb671200(v=vs.90)
			if (createdSince != null)
			{
				return $"*[System[TimeCreated[@SystemTime>='{createdSince.Value.ToUniversalTime().ToString("o")}']]]";
			}
			return "*";
		}
	}

	public class MachineAwakeLog
	{
		public enum State
		{
			Off,
			Hibernate,
			Sleep,
			Awake
		}

		private static State? X(EventLogEntryEx record) // EventRecord
		{
			return record.InstanceId switch
			{
				(int)KernelPowerLogSource.Instance.ExitStandby 
					or (int)KernelPowerLogSource.Instance.ResumeFromSleep => State.Awake,
				(int)KernelPowerLogSource.Instance.EnterStandby
					or (int)KernelPowerLogSource.Instance.EnterSleep => State.Sleep,
				(int)KernelPowerLogSource.Instance.ShutdownInitiated
					or (int)KernelPowerLogSource.Instance.PrepareReboot
					or (int)KernelPowerLogSource.Instance.UnexpectedShutdown
					=> State.Off,
				_ => null
			};
			//return record.Task == (int)KernelPowerLogSource.Categories.ExitStandby || record.Id == (int)KernelPowerLogSource.Instance.ExitStandby ? State.Awake : State.Off;
		}

		public static IEnumerable<(DateTime When, State State)> Get(DateTime since)
		{
			var logReader = new WindowsEventLog();
			var records = logReader.ReadFromLog(new KernelPowerLogSource(), EventLogQueryHelper.CreateQueryString(since))
				.Where(o => o.ProviderName == KernelPowerLogSource.SourceName
					//&& (o.Task == (int)KernelPowerLogSource.Categories.ExitStandby || o.Id == (int)KernelPowerLogSource.Instance.ExitStandby)
					&& o.TimeCreated != null)
				.Select(EventLogEntryEx.Create)
				.Select(o => new { Row = o, When = o.TimeGenerated, o.Source, o.InstanceId, NewState = X(o) });
			//.Select(o => new { When = o.TimeCreated!.Value, o.Task, o.Id, NewState = X(o) });

			//var tmp = records.ToList();

			var result = records
				.Where(o => o.NewState != null)
				.Select(o => (o.When, o.NewState!.Value));

			// TODO: after a shutdown (577/109), look for next transition (566) (or powersourcechange, 521?)
			var shutDowns = records.Index()
				.Where(o => 
					o.Item.Row.InstanceId == (int)KernelPowerLogSource.Instance.ShutdownInitiated
					|| o.Item.Row.InstanceId == (int)KernelPowerLogSource.Instance.UnexpectedShutdown
					|| o.Item.Row.InstanceId == (int)KernelPowerLogSource.Instance.PrepareReboot).ToList();
			foreach (var item in shutDowns)
			{
				var nextAwake = records.Skip(item.Index).FirstOrDefault(o => o.InstanceId == (int)KernelPowerLogSource.Instance.Transition || o.InstanceId == (int)KernelPowerLogSource.Instance.PowerSourceChange);
				if (nextAwake !=null)
					result = result.Concat([(nextAwake.When, State.Awake)]);
			}
			return result.OrderBy(o => o.When);
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

		public DateTime? GetLatestStart(DateTime since)
		{
			try
			{
				return ReadFromLog(new KernelPowerLogSource(), EventLogQueryHelper.CreateQueryString(since))
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
			DriverStoppedTransition = 40, //36 The driver \Driver\vpcivsp for device ROOT\VPCIVSP\0000 stopped the power transition.
			UnexpectedShutdown = 41, // The system has rebooted without cleanly shutting down first. https://learn.microsoft.com/en-us/troubleshoot/windows-client/performance/event-id-41-restart
			EnterSleep = 42, //64
			PowerSourceChange = 105, //100
			ResumeFromSleep = 107,
			ShutdownInitiated = 109, //103 The kernel power manager has initiated a shutdown transition
			ThermalZone = 125, //86
			UserModeAttemptedStateChange = 187, // 102 User-mode process attempted to change the system state by calling SetSuspendState or SetSystemPowerState APIs.
			ConnectivityState = 172,
			EnterStandby = 506,
			ExitStandby = 507,
			ActiveBatteryCount = 521, //220
			Transition = 566,
			PrepareReboot = 577 //280 The system has prepared for a system initiated reboot from Active.
			//187 / 243 
		}
	}

	public class EventLogEntryWrapper
	{
		private readonly EventLogEntry entry;

		public EventLogEntryWrapper(EventLogEntry entry)
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
	}

	public class EventLogEntryEx
	{
		public EventLogEntryEx(EventLogEntry entry)
		{
			Message = entry.Message;
			Source = entry.Source;
			CategoryNumber = entry.CategoryNumber;
			TimeWritten = entry.TimeWritten;
			TimeGenerated = entry.TimeGenerated;
			UserName = entry.UserName;
			InstanceId = entry.InstanceId;
		}
        public EventLogEntryEx(EventRecord entry)
        {
			Message = entry.TaskDisplayName;
			Source = entry.ProviderName;
			//CategoryNumber = entry.CategoryNumber;
			TimeWritten = DateTime.MinValue;
			TimeGenerated = entry.TimeCreated ?? DateTime.MinValue;
			UserName = entry.UserId?.ToString() ?? "";
			InstanceId = entry.Id;
		}
		//[Obsolete()]
		//public int EventId => entry.EventID;

		public string Message { get; set; }
		public string Source { get; set; }
		public short CategoryNumber { get; set; }
		public DateTime TimeWritten { get; set; }
		public DateTime TimeGenerated { get; set; }
		public string UserName { get; set; }
		public long InstanceId { get; set; }

		public static EventLogEntryEx Create(EventRecord entry)
		{
			return entry.ProviderName switch
			{
				KernelPowerLogSource.SourceName => new KernelPowerLogEventEntry(entry),
				_ => new EventLogEntryEx(entry),
			};
		}
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
		public KernelPowerLogEventEntry(EventRecord entry) : base(entry) { }
		public KernelPowerLogSource.Categories Category => (KernelPowerLogSource.Categories)CategoryNumber;
		public KernelPowerLogSource.Instance InstanceType => (KernelPowerLogSource.Instance)InstanceId;

		public override string ToString() => $"{Source} {Category} {TimeGenerated} {InstanceType} {Message}";
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
	
