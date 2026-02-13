using Common;

namespace SystemTrayApp
{
	public partial class Form1 : Form
	{
		private NotifyIcon notifyIcon;
		private readonly IMessageGetLastestRepository messageRepository;
		private List<Entry> Entries = new List<Entry>();

		public Form1(IMessageGetLastestRepository messageRepository)
		{
			InitializeComponent();
			FormClosing += Form1_FormClosing;

			notifyIcon = new NotifyIcon();
			InitializeNotifyIcon(notifyIcon);

			//cached = (DateTime.UtcNow.AddDays(-2), []);
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
			return $"X";
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
				Show();
				//MessageBox.Show("Hey!");
				//var hwnd = WindowFromPoint(MousePosition);
				//if (hwnd != IntPtr.Zero)
				//{
				//	if (GetWindowRect(hwnd, out RECT rect))
				//	{
				//		var f = new Form
				//		{
				//			StartPosition = FormStartPosition.Manual,
				//			TransparencyKey = BackColor,
				//			ControlBox = false,
				//			Text = string.Empty,
				//			FormBorderStyle = FormBorderStyle.FixedSingle,
				//			Bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom)
				//		};
				//		f.Show();
				//	}
				//}
			}
		}
	}
}
