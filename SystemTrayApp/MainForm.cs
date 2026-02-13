using Common;
using System.Runtime.InteropServices;

namespace SystemTrayApp
{
	public partial class MainForm : Form
	{
		[StructLayout(LayoutKind.Sequential)]
		private struct RECT
		{
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;
		}

		[DllImport("user32.dll")]
		private static extern IntPtr WindowFromPoint(Point Point);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);


		private NotifyIcon notifyIcon;
		private readonly IMessageGetLastestRepository messageRepository;
		private List<Entry> Entries = new List<Entry>();

		public MainForm(IMessageGetLastestRepository messageRepository)
		{
			InitializeComponent();
			FormClosing += Form1_FormClosing;

			notifyIcon = new NotifyIcon();
			InitializeNotifyIcon(notifyIcon);

			this.messageRepository = messageRepository;
		}

		public async Task GetLatestData()
		{
			if (!Entries.Any())
				Entries = await messageRepository.Get(DateTime.UtcNow.AddDays(-2));
			else
				Entries = await messageRepository.Get();
		}

		private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
		{
			e.Cancel = true;
			Hide();
		}

		private void InitializeNotifyIcon(NotifyIcon notifyIcon)
		{
			notifyIcon.Icon = SystemIcons.Information;
			notifyIcon.Text = "Initializing";
			notifyIcon.Visible = true;
			notifyIcon.MouseMove += NotifyIcon_MouseMove;
			notifyIcon.MouseClick += NotifyIcon_MouseClick;

			var contextMenuStrip = new ContextMenuStrip();
			contextMenuStrip.Items.AddRange(
			[
				new ToolStripMenuItem("No quitting this one!"),
			]);
			notifyIcon.ContextMenuStrip = contextMenuStrip;
		}

		private string GetShortInfo()
		{
			if (Entries.Any())
			{
				var sessions = Session.GetSessions(Entries);
				var latest = sessions.OrderByDescending(o => o.Start).FirstOrDefault();
				if (latest != null)
					return $"{latest.Start:HH:mm} - {latest.Start.Add(latest.Duration):HH:mm} ({$"{latest.Duration.ToString("%h")}h{latest.Duration.ToString("%m")}m"})";
			}
			return $"...";
		}

		private async void NotifyIcon_MouseMove(object? sender, MouseEventArgs e)
		{
			if (Entries.Any() == false || (DateTime.UtcNow - Entries.Last().Time).TotalSeconds > 30)
			{
				await GetLatestData();
				notifyIcon.Text = GetShortInfo();
			}
			notifyIcon.Text = GetShortInfo();
		}

		private void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				var hwnd = WindowFromPoint(MousePosition);
				if (hwnd != IntPtr.Zero)
				{
					if (GetWindowRect(hwnd, out RECT rect))
					{
						Bounds = Rectangle.FromLTRB(rect.Left, rect.Top - 200, rect.Right, rect.Top);
					}
				}
				Show();
			}
		}
	}
}
