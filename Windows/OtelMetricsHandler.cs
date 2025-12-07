// See https://aka.ms/new-console-template for more information
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

public interface IMetricsHandler
{
	void AddToMetric(string metric, double value, IEnumerable<KeyValuePair<string, object?>> tags);
	public void AddToMetric(string metric, double value, string dim1, object? val1) =>
		AddToMetric(metric, value, [KeyValuePair.Create(dim1, val1)]);
}

public class OtelMetricsHandler : IMetricsHandler
{
	private readonly Meter meter;

	public OtelMetricsHandler(Meter meter)
	{
		this.meter = meter;
	}

	public void AddToMetric(string metric, double value, IEnumerable<KeyValuePair<string, object?>> tags)
	{
		GetCounter(metric)?.Add(value, tags.ToArray()); //.Select(o => KeyValuePair.Create(o.Key, (object?)o.Value)).ToArray());
		// tags?.Select(o => KeyValuePair.Create(o.Key, (o.Value is string s ? s : o.Value) ?? "")).ToArray());
	}

	private ConcurrentDictionary<string, Counter<double>> counters = new();
	private Counter<double>? GetCounter(string name)
	{
		return counters.GetOrAdd(name, (n) => meter.CreateCounter<double>(n));
	}
}
