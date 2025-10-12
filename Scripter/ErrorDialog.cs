namespace Scripter
{
	public sealed class ErrorDialog : Form
	{
		private readonly RichTextBox rtb;
		private readonly Button btnCopy;
		private readonly Button btnClose;

		public ErrorDialog(string title, string details)
		{
			Text = string.IsNullOrWhiteSpace(title) ? "Error" : title;
			StartPosition = FormStartPosition.CenterParent;
			MinimizeBox = false;
			MaximizeBox = true;
			ShowInTaskbar = false;
			FormBorderStyle = FormBorderStyle.Sizable;
			Width = 900;
			Height = 600;

			rtb = new RichTextBox
			{
				Dock = DockStyle.Fill,
				ReadOnly = true,
				BorderStyle = BorderStyle.None,
				Font = new Font("Consolas", 10f, FontStyle.Regular),
				BackColor = Color.White,
				ForeColor = Color.FromArgb(139, 0, 0),
				WordWrap = false,
				DetectUrls = true,
				ScrollBars = RichTextBoxScrollBars.Both,
				Text = details ?? string.Empty
			};

			btnCopy = new Button
			{
				Text = "Copy",
				AutoSize = true
			};
			btnCopy.Click += (s, e) =>
			{
				try { Clipboard.SetText(rtb.Text); }
				catch { /* ignore */ }
			};

			btnClose = new Button
			{
				Text = "Close",
				AutoSize = true
			};
			btnClose.Click += (s, e) => Close();

			var buttons = new FlowLayoutPanel
			{
				Dock = DockStyle.Bottom,
				FlowDirection = FlowDirection.RightToLeft,
				Padding = new Padding(10),
				AutoSize = true,
				AutoSizeMode = AutoSizeMode.GrowAndShrink
			};
			buttons.Controls.Add(btnClose);
			buttons.Controls.Add(btnCopy);

			Controls.Add(rtb);
			Controls.Add(buttons);
		}

		public static void Show(string title, string details, IWin32Window? owner = null)
		{
			using var dlg = new ErrorDialog(title, details);
			dlg.ShowDialog(owner);
		}
	}
}
