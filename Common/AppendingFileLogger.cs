using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

public static class AppendingFileLoggerExtensions
{
	public static ILoggingBuilder AddAppendingFileLogger(this ILoggingBuilder builder)
	{
		builder.AddConfiguration();
		builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, AppendingFileLoggerProvider>());
		LoggerProviderOptions.RegisterProviderOptions<AppendingFileLogger.Config, AppendingFileLoggerProvider>(builder.Services);
		return builder;
	}

	public static ILoggingBuilder AddAppendingFileLogger(this ILoggingBuilder builder, Action<AppendingFileLogger.Config> configure)
	{
		builder.AddAppendingFileLogger();
		builder.Services.Configure(configure);
		return builder;
	}
}

public sealed class AppendingFileLoggerProvider : ILoggerProvider
{
	private readonly IDisposable? _onChangeToken;
	private AppendingFileLogger.Config _currentConfig;
	
	private static readonly ConcurrentDictionary<string, ILogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
	private static readonly ConcurrentDictionary<string, FileStream> pathToFilestream = new(StringComparer.OrdinalIgnoreCase);

	public AppendingFileLoggerProvider(IOptionsMonitor<AppendingFileLogger.Config> config)
	{
		// called each time providers are requested from DI container - using statics to handle multiple instances
		_currentConfig = config.CurrentValue;
		_onChangeToken = config.OnChange(updatedConfig => _currentConfig = updatedConfig);
	}

	public ILogger CreateLogger(string categoryName)
	{
		string filePath;
		if (_currentConfig.CategoryToFilePath.Any())
		{
			filePath = _currentConfig.CategoryToFilePath.GetValueOrDefault(categoryName, "");
			if (filePath.Any() == false)
			{
				filePath = _currentConfig.CategoryToFilePath.FirstOrDefault(o => Regex.IsMatch(categoryName, o.Key)).Value;
				if (filePath?.Any() != true)
				{
					Console.WriteLine($"No appending logger for {categoryName}");
					return NullLogger.Instance;
				}
			}
		}
		else
		{
			if (string.IsNullOrEmpty(_currentConfig.Filepath))
				throw new ArgumentException($"No file path provided");
			filePath = _currentConfig.Filepath;
		}

		var fs = pathToFilestream.GetOrAdd(filePath, name => new FileStream(filePath, FileMode.Append, FileAccess.Write));
		return _loggers.GetOrAdd(filePath, name => new AppendingFileLogger(fs));
	}

	public void Dispose()
	{
		foreach (var item in pathToFilestream.Values)
			item.Dispose();
		_loggers.Clear();
		_onChangeToken?.Dispose();
	}
}

public class AppendingFileLogger : IDisposable, ILogger
{
	public class Config
	{
		public Dictionary<string, string> CategoryToFilePath { get; set; } = new();
		public string Filepath { get; set; } = string.Empty;
	}

	private FileStream? fs;
	private StreamWriter sw;
	private DateTime lastFlush = DateTime.MinValue;

	private bool isDisposed = false;

	public AppendingFileLogger(FileStream fs)
	{
		sw = new StreamWriter(fs);
	}

	public AppendingFileLogger(string filePath)
	{
		fs = new FileStream(filePath, FileMode.Append, FileAccess.Write);
		sw = new StreamWriter(fs);
	}

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
	public bool IsEnabled(LogLevel logLevel) => true;

	public void Dispose()
	{
		if (isDisposed) return;
		isDisposed = true;

		sw.Flush();
		sw.Dispose();

		fs?.Dispose();
	}

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		var message = formatter(state, exception);
		if (message.Any() != true || message == "[null]")
			return;

		var formatted = $">{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}";
		//Console.WriteLine(formatted);

		//await sw.WriteLineAsync(formatted);
		sw.WriteLine(formatted);

		if ((DateTime.Now - lastFlush).TotalSeconds > 5)
		{
			sw.Flush();
			lastFlush = DateTime.Now;
		}
	}
}
