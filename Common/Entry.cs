namespace Common
{
	public record Entry(DateTime Time, Message Message, string? Sender);

	public static class EntryExtensions
	{
		public static List<(DateTime Start, TimeSpan Duration, List<Entry> Entries)> AsSessions(this IEnumerable<Entry> entries, TimeSpan threshold)
		{
			var ordered = entries.OrderBy(o => o.Time);

			var tmp = ordered
				.Paired(firstEntryIsDouble: true)
				.Select(pair => new { Diff = pair.Item2.Time - pair.Item1.Time, Item = pair.Item1 })
				.ToList();

			return tmp.SplitBy(o => o.Diff > threshold).Select(o => (o.First().Item.Time, o.Last().Item.Time - o.First().Item.Time, o.Select(p => p.Item).ToList())).ToList();
		}
	}
}
