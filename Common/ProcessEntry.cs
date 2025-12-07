using System.Diagnostics;

public record ProcessEntry(string ProcessName, string MainWindowTitle, DateTime StartTime, int Id)
{
	public string DisplayName => $"{ProcessName} ({MainWindowTitle})";
	public static ProcessEntry From(Process p)
		=> new ProcessEntry(p.ProcessName, p.MainWindowTitle, p.StartTime, p.Id);
}
