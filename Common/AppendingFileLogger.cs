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
		LoggerProviderOptions.RegisterProviderOptions<AppendingFileLoggerProvider.Config, AppendingFileLoggerProvider>(builder.Services);
		return builder;
	}

	public static ILoggingBuilder AddAppendingFileLogger(this ILoggingBuilder builder, Action<AppendingFileLoggerProvider.Config> configure)
	{
		builder.AddAppendingFileLogger();
		builder.Services.Configure(configure);
		return builder;
	}
}

public sealed class AppendingFileLoggerProvider : ILoggerProvider
{
	public class Config
	{
		public Dictionary<string, string> CategoryToFilePath { get; set; } = new();
		public string Filepath { get; set; } = string.Empty;
		/// <summary>
		/// If false, file will be opened for each write - decreases performance when lots of logging
		/// </summary>
		public bool KeepFileOpen { get; set; } = true;
	}

	private readonly IDisposable? _onChangeToken;
	private Config _currentConfig;
	
	private static readonly ConcurrentDictionary<string, ILogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
	private static readonly ConcurrentDictionary<string, FileStream> pathToFilestream = new(StringComparer.OrdinalIgnoreCase);

	public AppendingFileLoggerProvider(IOptionsMonitor<Config> config)
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

		if (_currentConfig.KeepFileOpen)
		{
			var fs = pathToFilestream.GetOrAdd(filePath, name => new FileStream(filePath, FileMode.Append, FileAccess.Write));
			return _loggers.GetOrAdd(filePath, name => new AppendingFileLogger(fs));
		}
		else
		{
			return _loggers.GetOrAdd(filePath, name => new AppendingFileLogger(filePath, false));
		}
	}

	public void Dispose()
	{
		foreach (var item in pathToFilestream.Values)
			item.Dispose();
		foreach (var item in _loggers.Values.OfType<IDisposable>())
			item.Dispose();
		_loggers.Clear();
		_onChangeToken?.Dispose();
	}
}

public class AppendingFileLogger : IDisposable, ILogger
{
	private readonly FileStream? fs;
	private readonly StreamWriter? sw;
	private readonly string? filePath;
	private readonly List<string> unwrittenMessages = new();
	private DateTime lastFlush = DateTime.MinValue;

	private bool isDisposed = false;

	public AppendingFileLogger(FileStream fs)
	{
		sw = new StreamWriter(fs);
	}

	public AppendingFileLogger(string filePath, bool keepOpen)
	{
		if (keepOpen)
			(fs, sw) = OpenWrite(filePath);
		this.filePath = filePath;
	}
	private (FileStream?, StreamWriter?) OpenWrite(string filePath)
	{
		try
		{
			var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write);
			return (fs, new StreamWriter(fs));
		}
		catch (IOException ioEx) when ((ioEx.HResult & 0x0000FFFF) == 32)
		{
			return (null, null);
		}
	}

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
	public bool IsEnabled(LogLevel logLevel) => true;

	public void Dispose()
	{
		if (isDisposed) return;
		isDisposed = true;

		if (unwrittenMessages.Any())
		{
			TryWrite(null);
		}

		sw?.Flush();
		sw?.Dispose();

		fs?.Dispose();
	}

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		var message = formatter(state, exception);
		if (message.Any() != true || message == "[null]")
			return;

		var formatted = $">{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}";
		//Console.WriteLine(formatted);

		if (TryWrite(formatted))
			unwrittenMessages.Add(formatted);
	}

	private bool TryWrite(string? formatted)
	{
		var swLocal = sw;
		FileStream? fsLocal = null;
		if (swLocal == null)
		{
			if (filePath == null)
				throw new Exception("x");
			(fsLocal, swLocal) = OpenWrite(filePath);
		}
		if (swLocal == null)
			return false;

		var copy = new List<string>(unwrittenMessages);
		unwrittenMessages.Clear();

		foreach (var item in copy)
			swLocal.WriteLine(item);
		if (formatted?.Any() == true)
			swLocal.WriteLine(formatted);

		if ((DateTime.Now - lastFlush).TotalSeconds > 5 || sw == null)
		{
			swLocal.Flush();
			lastFlush = DateTime.Now;

			if (sw == null)
			{
				swLocal.Dispose();
				fsLocal?.Dispose();
			}
		}
		return true;
	}
}
