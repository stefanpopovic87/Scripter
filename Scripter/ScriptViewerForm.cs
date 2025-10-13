namespace Scripter
{
	public sealed class ScriptViewerForm : Form
	{
		private readonly TextBox txt;
		private readonly Button btnCopy;
		private readonly Button btnClose;
		private readonly ToolTip tip;

		public ScriptViewerForm(string title, string content)
		{
			Text = title;
			StartPosition = FormStartPosition.CenterParent;
			MinimumSize = new Size(700, 500);
			Size = new Size(900, 650);
			KeyPreview = true;
			AutoScaleMode = AutoScaleMode.Dpi;

			Padding = new Padding(8, 6, 8, 6);

			// --- Text area (read-only, monospaced) ---
			txt = new TextBox
			{
				Multiline = true,
				ReadOnly = true,
				ScrollBars = ScrollBars.Both,
				WordWrap = false,
				Dock = DockStyle.Fill,
				Font = new Font("Consolas", 10f),
				Text = content,
				TabStop = true
			};

			Shown += (s, e) =>
			{
				txt.SelectionStart = 0;
				txt.SelectionLength = 0;
				txt.ScrollToCaret();
				ActiveControl = btnClose;
			};

			// --- Bottom bar (Copy/Close) ---
			var bottom = new Panel
			{
				Dock = DockStyle.Bottom,
				Height = 56,
				Padding = new Padding(10, 8, 10, 10)
			};

			var flow = new FlowLayoutPanel
			{
				Dock = DockStyle.Right,
				FlowDirection = FlowDirection.LeftToRight,
				WrapContents = false,
				AutoSize = true
			};

			btnCopy = new Button
			{
				Text = "Copy",
				AutoSize = true,
				Anchor = AnchorStyles.Right | AnchorStyles.Top,
				Margin = new Padding(0, 0, 8, 0)
			};

			btnClose = new Button
			{
				Text = "Close",
				AutoSize = true,
				Anchor = AnchorStyles.Right | AnchorStyles.Top
			};

			flow.Controls.Add(btnCopy);
			flow.Controls.Add(btnClose);
			bottom.Controls.Add(flow);

			// --- ToolTip ---
			tip = new ToolTip
			{
				IsBalloon = false,
				UseAnimation = true,
				UseFading = true
			};

			// Copy logic
			btnCopy.Click += (s, e) =>
			{
				try
				{
					var toCopy = string.IsNullOrEmpty(txt.SelectedText) ? txt.Text : txt.SelectedText;
					Clipboard.SetText(toCopy ?? string.Empty);
					tip.Show("Copied!", btnCopy, new Point(btnCopy.Width / 2, -20), 1200);
				}
				catch { /* ignore */ }
			};

			// Close
			btnClose.Click += (s, e) => Close();

			// Context menu (Copy / Select All)
			var ctx = new ContextMenuStrip();
			ctx.Items.Add("Copy", null, (s, e) => btnCopy.PerformClick());
			ctx.Items.Add("Select All", null, (s, e) => { txt.SelectAll(); txt.Focus(); });
			txt.ContextMenuStrip = ctx;

			// Tastaturne prečice
			KeyDown += (s, e) =>
			{
				if (e.Control && e.KeyCode == Keys.A) { txt.SelectAll(); e.SuppressKeyPress = true; }
				else if (e.Control && e.KeyCode == Keys.C) { btnCopy.PerformClick(); e.SuppressKeyPress = true; }
				else if (e.KeyCode == Keys.Escape) { Close(); }
			};

			Controls.Add(txt);
			Controls.Add(bottom);
		}
	}
}
