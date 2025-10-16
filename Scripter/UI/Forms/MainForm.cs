using DbUp.Engine.Output;

namespace Scripter.UI.Forms
{
	public partial class MainForm : Form
	{
		// UI controls
		private TextBox txtConnectionString = null!;
		private TextBox txtScriptsFolder = null!;
		private Button btnBrowse = null!;
		private Button btnLoad = null!;
		private Button btnRun = null!;
		private ListView lvScripts = null!;
		private ListView lvHistory = null!;
		private Button btnHistoryRefresh = null!;
		private Label lblStatus = null!;
		private FolderBrowserDialog folderDialog = new();

		// Overlays
		private Panel overlayPanel = null!;
		private Label overlayLabel = null!;
		private ProgressBar overlayProgress = null!;
		private Panel overlayHistoryPanel = null!;
		private Label overlayHistoryLabel = null!;
		private ProgressBar overlayHistoryProgress = null!;
		private Panel scriptsTopSpacer = null!;

		// Tooltips + hover state
		private readonly ToolTip ttScripts = new() { InitialDelay = 200, ReshowDelay = 100, AutoPopDelay = 15000 };
		private readonly ToolTip ttHistory = new() { InitialDelay = 200, ReshowDelay = 100, AutoPopDelay = 15000 };
		private ListViewItem? _lastTipItemScripts;
		private ListViewItem? _lastTipItemHistory;
		private bool _scriptsHandCursor;
		private bool _historyHandCursor;

		// Services
		private readonly IScriptService _scriptService;
		private readonly IScriptRepository _repository;
		private readonly IPreviewService _previewService;
		private readonly IIconProvider _icons;

		// Constants
		private const int GapYSmall = 0;
		private const int GapY = 1;
		private const int GapX = 6;

		public MainForm()
		{
			Text = "";
			StartPosition = FormStartPosition.CenterScreen;
			Width = 900;
			Height = 1000;
			AutoScaleMode = AutoScaleMode.Dpi;
			ShowIcon = false;
			Icon = null;

			// Instantiate services (simple composition)
			_icons = new IconProvider();
			_repository = new ScriptRepository(GetConnectionString, GetBaseFolder);
			_scriptService = new ScriptService(_repository);
			_previewService = new PreviewService(_repository);

			var header = CreateHeaderPanel();
			var inputs = CreateInputsPanel();

			var tabs = new TabControl { Dock = DockStyle.Fill };
			tabs.TabPages.Add(CreateScriptsTab());
			tabs.TabPages.Add(CreateHistoryTab());

			lblStatus = new Label
			{
				Dock = DockStyle.Bottom,
				Height = 96,
				Padding = new Padding(10, 6, 10, 6),
				TextAlign = ContentAlignment.MiddleLeft,
				Text = "Ready."
			};

			Controls.Add(tabs);
			Controls.Add(inputs);
			Controls.Add(header);
			Controls.Add(lblStatus);
		}

		private string GetConnectionString() => txtConnectionString.Text;
		private string GetBaseFolder()
		{
			var raw = txtScriptsFolder?.Text;
			if (string.IsNullOrWhiteSpace(raw))
				return ""; // signal "not set"
			return Path.GetFullPath(raw); // safe now
		}

		// ---------- UI Builders ----------

		private Panel CreateHeaderPanel()
		{
			var header = new Panel
			{
				Dock = DockStyle.Top,
				Height = 42,
				Padding = new Padding(12, 6, 0, 0),
				Margin = Padding.Empty
			};

			header.Paint += (s, e) =>
			{
				var logo = _icons.GetLogo();
				if (logo == null) return;
				int availableH = header.Height - header.Padding.Top - header.Padding.Bottom;
				int desiredH = Math.Min(availableH, 44);
				float scale = (float)desiredH / logo.Height;
				int drawW = (int)Math.Round(logo.Width * scale);
				int drawH = (int)Math.Round(logo.Height * scale);
				int x = header.Padding.Left;
				int y = header.Padding.Top + (availableH - drawH) / 2;
				var g = e.Graphics;
				g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
				g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
				g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
				g.DrawImage(logo, new Rectangle(x, y, drawW, drawH));
			};
			return header;
		}

		private TableLayoutPanel CreateInputsPanel()
		{
			var panel = new TableLayoutPanel
			{
				Dock = DockStyle.Top,
				AutoSize = true,
				Padding = new Padding(4, 2, 4, 0),
				ColumnCount = 3
			};
			panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

			var lblConn = new Label
			{
				Text = "Connection string:",
				Anchor = AnchorStyles.Left,
				Margin = new Padding(0, GapY, GapX, GapY)
			};
			txtConnectionString = new TextBox
			{
				Anchor = AnchorStyles.Left | AnchorStyles.Right,
				Margin = new Padding(0, GapYSmall, GapX, GapYSmall),
				Text = "Data Source=localhost; Initial Catalog=Aleacc; User Id=sa; Password=Password1*; TrustServerCertificate=True"
			};

			var lblFolder = new Label
			{
				Text = "Scripts folder:",
				Anchor = AnchorStyles.Left,
				Margin = new Padding(0, GapY, GapX, GapY)
			};
			txtScriptsFolder = new TextBox
			{
				Anchor = AnchorStyles.Left | AnchorStyles.Right,
				Margin = new Padding(0, GapYSmall, GapX, GapYSmall)
			};

			btnBrowse = new Button
			{
				Text = "Browse",
				AutoSize = true,
				Image = _icons.Get("browse", 18),
				TextImageRelation = TextImageRelation.ImageBeforeText
			};
			btnBrowse.Click += (s, e) =>
			{
				folderDialog.SelectedPath = Directory.Exists(txtScriptsFolder.Text)
					? txtScriptsFolder.Text
					: AppContext.BaseDirectory;
				if (folderDialog.ShowDialog() == DialogResult.OK)
					txtScriptsFolder.Text = folderDialog.SelectedPath;
			};

			var actions = new FlowLayoutPanel
			{
				FlowDirection = FlowDirection.LeftToRight,
				AutoSize = true,
				WrapContents = false,
				Margin = new Padding(0, 0, GapX, 0)
			};

			btnLoad = new Button
			{
				Text = "Load Scripts",
				AutoSize = true,
				Image = _icons.Get("refresh", 18),
				TextImageRelation = TextImageRelation.ImageBeforeText
			};
			btnLoad.Click += async (s, e) => await LoadScriptsAsync(true);

			btnRun = new Button
			{
				Text = "Run Pending",
				AutoSize = true,
				Enabled = false,
				Image = _icons.Get("play", 18),
				TextImageRelation = TextImageRelation.ImageBeforeText
			};
			btnRun.Click += async (s, e) => await RunPendingAsync();

			actions.Controls.AddRange(new Control[] { btnLoad, btnRun });

			panel.Controls.Add(lblConn, 0, 0);
			panel.Controls.Add(txtConnectionString, 1, 0);
			panel.Controls.Add(new Panel(), 2, 0);
			panel.Controls.Add(lblFolder, 0, 1);
			panel.Controls.Add(txtScriptsFolder, 1, 1);
			panel.Controls.Add(btnBrowse, 2, 1);
			panel.Controls.Add(new Panel(), 0, 2);
			panel.Controls.Add(actions, 1, 2);
			panel.Controls.Add(new Panel(), 2, 2);

			return panel;
		}

		private TabPage CreateScriptsTab()
		{
			var page = new TabPage("Scripts") { Padding = new Padding(3) };

			lvScripts = new ListView
			{
				Dock = DockStyle.Fill,
				View = View.Details,
				FullRowSelect = true,
				GridLines = true,
				Font = new Font("Segoe UI", 10f),
				OwnerDraw = true
			};
			lvScripts.Columns.Add("", 40);
			lvScripts.Columns.Add("Script Name", 440);
			lvScripts.Columns.Add("Applied", 240);
			lvScripts.Columns.Add("Status", 160);

			var imgList = new ImageList { ImageSize = new Size(20, 20), ColorDepth = ColorDepth.Depth32Bit };
			imgList.Images.Add("executed", _icons.Get("executed", 20));
			imgList.Images.Add("pending", _icons.Get("pending", 20));
			imgList.Images.Add("error", _icons.Get("error", 20));
			lvScripts.SmallImageList = imgList;

			lvScripts.DrawColumnHeader += (s, e) => e.DrawDefault = true;
			lvScripts.DrawSubItem += DrawScriptsSubItem;

			lvScripts.MouseMove += ScriptsMouseMove;
			lvScripts.MouseLeave += (s, e) => ResetScriptsHover();
			lvScripts.MouseUp += ScriptsMouseUp;

			overlayPanel = BuildOverlay(out overlayLabel, out overlayProgress);
			lvScripts.Controls.Add(overlayPanel);

			var host = new Panel { Dock = DockStyle.Fill };
			scriptsTopSpacer = new Panel { Dock = DockStyle.Top, Height = 48 };
			host.Controls.Add(lvScripts);
			host.Controls.Add(scriptsTopSpacer);
			page.Controls.Add(host);
			return page;
		}

		private TabPage CreateHistoryTab()
		{
			var page = new TabPage("History") { Padding = new Padding(3) };
			var layout = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				ColumnCount = 2,
				RowCount = 2
			};
			layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

			btnHistoryRefresh = new Button
			{
				Text = "Refresh",
				AutoSize = true,
				Image = _icons.Get("refresh", 18),
				TextImageRelation = TextImageRelation.ImageBeforeText,
				Margin = new Padding(3, 3, 3, 6)
			};
			btnHistoryRefresh.Click += async (s, e) => await LoadHistoryAsync();

			var spacer = new Panel { Dock = DockStyle.Fill, Height = btnHistoryRefresh.Height };

			lvHistory = new ListView
			{
				Dock = DockStyle.Fill,
				View = View.Details,
				FullRowSelect = true,
				GridLines = true,
				Font = new Font("Segoe UI", 10f),
				OwnerDraw = true
			};
			lvHistory.Columns.Add("Id", 100);
			lvHistory.Columns.Add("Script Name", 440);
			lvHistory.Columns.Add("Applied", 240);
			lvHistory.Columns.Add("Executed By", 140);
			lvHistory.Columns.Add("Machine", 180);
			lvHistory.Columns.Add("Path", 640);

			lvHistory.DrawColumnHeader += (s, e) => e.DrawDefault = true;
			lvHistory.DrawSubItem += DrawHistorySubItem;

			lvHistory.MouseMove += HistoryMouseMove;
			lvHistory.MouseLeave += (s, e) => ResetHistoryHover();
			lvHistory.MouseUp += HistoryMouseUp;

			overlayHistoryPanel = BuildOverlay(out overlayHistoryLabel, out overlayHistoryProgress);
			lvHistory.Controls.Add(overlayHistoryPanel);

			layout.Controls.Add(spacer, 0, 0);
			layout.Controls.Add(btnHistoryRefresh, 1, 0);
			layout.Controls.Add(lvHistory, 0, 1);
			layout.SetColumnSpan(lvHistory, 2);

			page.Controls.Add(layout);
			page.Enter += async (s, e) =>
			{
				if (lvHistory.Items.Count == 0)
					await LoadHistoryAsync();
			};

			return page;
		}

		private Panel BuildOverlay(out Label label, out ProgressBar bar)
		{
			var panel = new Panel
			{
				Dock = DockStyle.Fill,
				Visible = false,
				BackColor = Color.FromArgb(160, SystemColors.ControlLightLight)
			};

			label = new Label { Text = "Working...", AutoSize = true, Font = new Font("Segoe UI", 10) };
			bar = new ProgressBar { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 35, Width = 260 };

			var content = new FlowLayoutPanel
			{
				FlowDirection = FlowDirection.TopDown,
				AutoSize = true,
				Padding = new Padding(16)
			};
			content.Controls.Add(label);
			content.Controls.Add(bar);

			var center = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 };
			center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
			center.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
			center.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
			center.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			center.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
			center.Controls.Add(content, 1, 1);

			panel.Controls.Add(center);
			return panel;
		}

		// ---------- Event handlers (Scripts) ----------

		private void DrawScriptsSubItem(object? sender, DrawListViewSubItemEventArgs e)
		{
			if (e.ColumnIndex == 0 && e?.Item?.ImageList != null)
			{
				var img = e.Item.ImageList.Images[e.Item.ImageKey];
				if (img != null)
				{
					int x = e.Bounds.X + (e.Bounds.Width - img.Width) / 2;
					int y = e.Bounds.Y + (e.Bounds.Height - img.Height) / 2;
					e.Graphics.DrawImage(img, x, y, img.Width, img.Height);
				}
			}
			else if (e?.ColumnIndex == 1)
			{
				TextRenderer.DrawText(
					e.Graphics,
					e.SubItem?.Text,
					e.SubItem?.Font!,
					e.Bounds,
					Color.RoyalBlue,
					TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
			}
			else
			{
				e?.DrawText(TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
			}
		}

		private void ScriptsMouseMove(object? sender, MouseEventArgs e)
		{
			var hit = lvScripts.HitTest(e.Location);
			bool onName = hit.Item != null && hit.SubItem != null && hit.Item.SubItems.IndexOf(hit.SubItem) == 1;
			if (onName != _scriptsHandCursor)
			{
				_scriptsHandCursor = onName;
				lvScripts.Cursor = onName ? Cursors.Hand : Cursors.Default;
			}

			if (onName && hit.Item != null && !ReferenceEquals(_lastTipItemScripts, hit.Item))
			{
				_lastTipItemScripts = hit.Item;
				string fileName = hit.Item.SubItems[1].Text;
				string preview = _previewService.GetPreviewByFileName(fileName);
				if (!string.IsNullOrEmpty(preview))
					ttScripts.Show(preview, lvScripts, e.Location + new Size(16, 20), 15000);
			}
		}

		private void ScriptsMouseUp(object? sender, MouseEventArgs e)
		{
			if (e.Button != MouseButtons.Left) return;
			var hit = lvScripts.HitTest(e.Location);
			if (hit.Item == null || hit.SubItem == null) return;
			if (hit.Item.SubItems.IndexOf(hit.SubItem) != 1) return;

			string fileName = hit.Item.SubItems[1].Text;
			string content = _repository.GetScriptContentByNameOrFile(fileName);
			using var dlg = new ScriptViewerForm(fileName, content);
			dlg.ShowDialog(this);
		}

		private void ResetScriptsHover()
		{
			_scriptsHandCursor = false;
			lvScripts.Cursor = Cursors.Default;
			_lastTipItemScripts = null;
			ttScripts.Hide(lvScripts);
		}

		// ---------- Event handlers (History) ----------

		private void DrawHistorySubItem(object? sender, DrawListViewSubItemEventArgs e)
		{
			if (e.ColumnIndex == 1)
			{
				TextRenderer.DrawText(
					e.Graphics,
					e.SubItem?.Text,
					e.SubItem?.Font!,
					e.Bounds,
					Color.RoyalBlue,
					TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
			}
			else
			{
				e.DrawText(TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
			}
		}

		private void HistoryMouseMove(object? sender, MouseEventArgs e)
		{
			var hit = lvHistory.HitTest(e.Location);
			bool onName = hit.Item != null && hit.SubItem != null && hit.Item.SubItems.IndexOf(hit.SubItem) == 1;
			if (onName != _historyHandCursor)
			{
				_historyHandCursor = onName;
				lvHistory.Cursor = onName ? Cursors.Hand : Cursors.Default;
			}

			if (onName && hit.Item != null && !ReferenceEquals(_lastTipItemHistory, hit.Item))
			{
				_lastTipItemHistory = hit.Item;
				int id = int.TryParse(hit.Item.SubItems[0].Text, out var v) ? v : 0;
				string preview = _previewService.GetPreviewById(id);
				if (!string.IsNullOrEmpty(preview))
					ttHistory.Show(preview, lvHistory, e.Location + new Size(16, 20), 15000);
			}
		}

		private void HistoryMouseUp(object? sender, MouseEventArgs e)
		{
			if (e.Button != MouseButtons.Left) return;
			var hit = lvHistory.HitTest(e.Location);
			if (hit.Item == null || hit.SubItem == null) return;
			if (hit.Item.SubItems.IndexOf(hit.SubItem) != 1) return;

			int id = int.TryParse(hit.Item.SubItems[0].Text, out var v) ? v : 0;
			string sql = _repository.GetScriptContentById(id);
			using var dlg = new ScriptViewerForm(hit.Item.SubItems[1].Text, sql);
			dlg.ShowDialog(this);
		}

		private void ResetHistoryHover()
		{
			_historyHandCursor = false;
			lvHistory.Cursor = Cursors.Default;
			_lastTipItemHistory = null;
			ttHistory.Hide(lvHistory);
		}

		// ---------- Actions ----------

		private async Task LoadScriptsAsync(bool showMessage)
		{
			lvScripts.Items.Clear();
			btnRun.Enabled = false;

			if (!Directory.Exists(GetBaseFolder()))
			{
				MessageBox.Show("Folder not found.");
				return;
			}

			ShowScriptsLoader("Loading scripts...");
			btnLoad.Enabled = false;
			btnRun.Enabled = false;

			try
			{
				var result = await _scriptService.LoadScriptsAsync(GetConnectionString(), GetBaseFolder());

				foreach (var row in result.Executed
					.OrderBy(r => r.Applied ?? DateTime.MaxValue)
					.ThenBy(r => r.ScriptName, StringComparer.OrdinalIgnoreCase))
				{
					var item = new ListViewItem { ImageKey = "executed", Text = "" };
					item.SubItems.Add(row.ScriptName);
					item.SubItems.Add(row.Applied?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "");
					item.SubItems.Add("Executed");
					lvScripts.Items.Add(item);
				}

				foreach (var file in result.PendingFiles)
				{
					var item = new ListViewItem { ImageKey = "pending", Text = "" };
					item.SubItems.Add(file);
					item.SubItems.Add("");
					item.SubItems.Add("Pending");
					lvScripts.Items.Add(item);
				}

				SetStatus(result.Executed.Count, result.PendingFiles.Count);
				btnRun.Enabled = result.PendingFiles.Count > 0;

				if (showMessage)
					MessageBox.Show($"Executed: {result.Executed.Count}\nPending: {result.PendingFiles.Count}", "Scripts loaded");
			}
			catch (Exception ex)
			{
				ShowError("Error", ex);
			}
			finally
			{
				HideScriptsLoader();
				btnLoad.Enabled = true;
				btnRun.Enabled = btnRun.Enabled;
			}
		}

		private async Task RunPendingAsync()
		{
			if (!Directory.Exists(GetBaseFolder()))
			{
				MessageBox.Show("Folder not found.");
				return;
			}

			ShowScriptsLoader("Running scripts...");
			btnLoad.Enabled = false;
			btnRun.Enabled = false;

			try
			{
				var execResult = await _scriptService.ExecutePendingAsync(GetConnectionString(), GetBaseFolder(), new WinFormsLog(this));
				if (!execResult.Success)
				{
					await LoadScriptsAsync(false);
					if (!string.IsNullOrEmpty(execResult.ErrorFile))
						MarkErrorRow(execResult.ErrorFile!, execResult.Error?.Message);

					var header = !string.IsNullOrEmpty(execResult.ErrorFile)
						? $"Failed while executing script:\r\n{execResult.ErrorFile}\r\n\r\n"
						: "Execution failed.\r\n\r\n";
					ShowError("Execution error", execResult.Error!, header);
					return;
				}

				MessageBox.Show("All pending scripts executed successfully.", "Success");
				await LoadScriptsAsync(false);
			}
			catch (Exception ex)
			{
				ShowError("Error", ex);
			}
			finally
			{
				HideScriptsLoader();
				btnLoad.Enabled = true;
				btnRun.Enabled = true;
			}
		}

		private async Task LoadHistoryAsync()
		{
			lvHistory.Items.Clear();
			ShowHistoryLoader("Loading history...");

			try
			{
				var rows = await _repository.GetHistoryAsync();
				foreach (var row in rows)
				{
					var it = new ListViewItem { Text = row.Id, UseItemStyleForSubItems = false };
					it.SubItems.Add(row.Script);
					it.SubItems.Add(row.AppliedUtc == DateTime.MinValue ? "" : row.AppliedUtc.ToString("yyyy-MM-dd HH:mm:ss"));
					it.SubItems.Add(row.By ?? "");
					it.SubItems.Add(row.Machine ?? "");
					it.SubItems.Add(row.Path ?? "");
					lvHistory.Items.Add(it);
				}
			}
			catch (Exception ex)
			{
				ShowError("Error loading history", ex);
			}
			finally
			{
				HideHistoryLoader();
			}
		}

		// ---------- Status / Error ----------

		private void ShowError(string title, Exception ex, string? prefix = null)
		{
			var msg = string.IsNullOrEmpty(prefix) ? "" : prefix + Environment.NewLine + Environment.NewLine;
			msg += ex.ToString();
			ErrorDialog.Show(title, msg, this);
		}

		private void MarkErrorRow(string fileNameOnly, string? errorText = null)
		{
			foreach (ListViewItem it in lvScripts.Items)
			{
				if (string.Equals(it.SubItems[1].Text, fileNameOnly, StringComparison.OrdinalIgnoreCase))
				{
					it.ImageKey = "error";
					it.ForeColor = Color.Red;
					if (it.SubItems.Count >= 4)
						it.SubItems[3].Text = "Error";
					if (!string.IsNullOrWhiteSpace(errorText))
						it.ToolTipText = errorText;
					it.Selected = true;
					it.EnsureVisible();
					break;
				}
			}
		}

		private void SetStatus(int executed, int pending) =>
			lblStatus.Text = $"Loaded.{Environment.NewLine}Executed: {executed}{Environment.NewLine}Pending: {pending}";

		// ---------- Overlay helpers ----------

		private void ShowScriptsLoader(string text)
		{
			overlayLabel.Text = text;
			overlayPanel.Visible = true;
			overlayPanel.BringToFront();
			overlayProgress.Style = ProgressBarStyle.Marquee;
		}
		private void HideScriptsLoader()
		{
			overlayProgress.Style = ProgressBarStyle.Blocks;
			overlayPanel.Visible = false;
		}

		private void ShowHistoryLoader(string text)
		{
			overlayHistoryLabel.Text = text;
			overlayHistoryPanel.Visible = true;
			overlayHistoryPanel.BringToFront();
			overlayHistoryProgress.Style = ProgressBarStyle.Marquee;
		}
		private void HideHistoryLoader()
		{
			overlayHistoryProgress.Style = ProgressBarStyle.Blocks;
			overlayHistoryPanel.Visible = false;
		}

		// ---------- Logging ----------

		private sealed class WinFormsLog : IUpgradeLog
		{
			private readonly MainForm _form;
			public WinFormsLog(MainForm form) => _form = form;

			public void WriteError(string format, params object[] args) => Append("[ERROR] " + string.Format(format, args));
			public void WriteInformation(string format, params object[] args) => Append("[INFO] " + string.Format(format, args));
			public void WriteWarning(string format, params object[] args) => Append("[WARN] " + string.Format(format, args));

			private void Append(string text)
			{
				if (_form.InvokeRequired)
					_form.Invoke(new Action(() => _form.lblStatus.Text = text));
				else
					_form.lblStatus.Text = text;
			}
		}
	}
}