using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ProcMon
{
    internal class Screenshot
	{
		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool GetWindowRect(HandleRef hWnd, out RECT lpRect);

		[StructLayout(LayoutKind.Sequential)]
		public struct RECT
		{
			public int Left;        // x position of upper-left corner
			public int Top;         // y position of upper-left corner
			public int Right;       // x position of lower-right corner
			public int Bottom;      // y position of lower-right corner
		}

		public static void Save(string file, System.Diagnostics.Process? process)
        {
			var bounds = new Rectangle(0, 0, 1024, 768);

			if (process != null)
			{
				if (process.MainWindowHandle > 0)
				{
					var hr = new HandleRef(process, process.MainWindowHandle);
					if (GetWindowRect(hr, out var rect))
					{
						bounds = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
					}
				}
			}
			using var bitmap = new Bitmap(bounds.Width, bounds.Height);
			using var g = Graphics.FromImage(bitmap);
			g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
			
			var format = new FileInfo(file).Extension.ToLower() switch
			{
				".jpg" => ImageFormat.Jpeg,
				".png" => ImageFormat.Png,
				"." => ImageFormat.Bmp,
				_ => ImageFormat.Png
			};
			bitmap.Save(file, format);
		}
	}
}
