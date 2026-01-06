using System;

namespace Common
{
    public static class TimeSpanExtensions
    {
        public enum TimeSpanPart
        {
			//Nanoseconds,
			Microseconds,
			Milliseconds,
			Seconds,
			Minutes,
            Hours,
            Days
		}

        public static int GetPart(this TimeSpan value, TimeSpanPart part)
        {
            return part switch
            {
                //TimeSpanPart.Nanoseconds => value.Nanoseconds,
                TimeSpanPart.Microseconds => value.Microseconds,
                TimeSpanPart.Milliseconds => value.Milliseconds,
                TimeSpanPart.Seconds => value.Seconds,
                TimeSpanPart.Minutes => value.Minutes,
                TimeSpanPart.Hours => value.Hours,
                TimeSpanPart.Days => value.Days,
                _ => throw new NotImplementedException()
            };
		}

		public static TimeSpan SetPart(this TimeSpan value, TimeSpanPart part, int v)
		{
			return part switch
			{
				//TimeSpanPart.Nanoseconds => value + new TimeSpan( value.Nanoseconds,
				TimeSpanPart.Microseconds => value + TimeSpan.FromMicroseconds(v - value.Microseconds),
				TimeSpanPart.Milliseconds => value + TimeSpan.FromMilliseconds(v - value.Milliseconds),
				TimeSpanPart.Seconds => value + TimeSpan.FromSeconds(v - value.Seconds),
				TimeSpanPart.Minutes => value + TimeSpan.FromMinutes(v - value.Minutes),
				TimeSpanPart.Hours => value + TimeSpan.FromHours(v - value.Hours),
				TimeSpanPart.Days => value + TimeSpan.FromDays(v - value.Days),
				_ => throw new NotImplementedException()
			};
		}

		public static TimeSpan CreateFullModuloUntil(TimeSpanPart part)
		{
			var result = new TimeSpan();
			var enums = Enum.GetValues<TimeSpanPart>().Reverse();
			var withFull = enums.TakeWhile(o => o != part).Concat([part]).ToArray();
			foreach (var item in withFull)
			{
				result = result.SetPart(item,
					item switch
					{
						TimeSpanPart.Days => 1000 * 365,
						TimeSpanPart.Hours => 23,
						TimeSpanPart.Minutes or TimeSpanPart.Seconds => 59,
						_ => 999
					});
			}
			return result;
		}

		public static TimeSpan Modulo(this TimeSpan value, TimeSpan spec)
        {
			var modified = value;
			foreach (var item in Enum.GetValues<TimeSpanPart>())
			{
				var p = spec.GetPart(item);
				p = p == 0 ? 1 : p;
				modified = modified.SetPart(item, modified.GetPart(item) % p);
			}
			return modified;
        }
    }

	public static class FileSystemInfoExtensions
	{

		public static string? ResolveSpecialFolder(string folder, char? surrounding = '%')
		{
			if (surrounding.HasValue)
			{
				if (folder.StartsWith(surrounding.Value) && folder.EndsWith(surrounding.Value))
					folder = folder[1..^1];
				else
					return null;
			}

			return folder.ToLower() switch
			{
				"localappdata" => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"appdata" or "roaming" => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
				"temp" => Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
				_ => throw new NotImplementedException()
			};
		}

		public static T Resolve<T>(this T fsi) where T : FileSystemInfo
		{
			var separator = Path.DirectorySeparatorChar;
			if (separator == '\\')
			{
				if (fsi.FullName.Split('/').Length > fsi.FullName.Split('\\').Length)
					separator = '/';
			}

			var split = fsi.FullName.Split(separator);

			var lastWithSpecialFolder = split.Index().LastOrDefault(o => ResolveSpecialFolder(o.Item) != null);
			if (lastWithSpecialFolder.Item != default)
			{
				split = split[lastWithSpecialFolder.Index..];
			}

			var segments = split.Select(o => ResolveSpecialFolder(o) ?? o);

			var resolved = Path.GetFullPath(string.Join(Path.DirectorySeparatorChar, segments));

			FileSystemInfo result = fsi switch
			{
				FileInfo fi => new FileInfo(resolved),
				DirectoryInfo di => new DirectoryInfo(resolved),
				_ => throw new NotSupportedException()
			};

			return (T)result;
		}
	}
}
