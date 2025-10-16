using DbUp.Engine.Output;
using System.Windows.Forms.VisualStyles;

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

		// Selection state (Scripts)
		private bool _scriptsSelectAll;
		private bool _hasPendingScripts;
		private Image _imgExecuted = null!;
		private Image _imgPending = null!;
		private Image _imgError = null!;

		// Sorting state
		private int _scriptsSortColumn = -1;
		private bool _scriptsSortAscending = true;
		private int _historySortColumn = -1;
		private bool _historySortAscending = true;

		private sealed class ScriptRowTag
		{
			public bool IsPending;
			public bool Selected;
			public string FileName = "";
			public string Status = "";
		}

		// Constants
		private const int GapYSmall = 0;
		private const int GapY = 1;
		private const int GapX = 6;

		private TabControl _tabs = null!;

		public MainForm()
		{
			Text = "";
			StartPosition = FormStartPosition.CenterScreen;
			Width = 900;
			Height = 1000;
			AutoScaleMode = AutoScaleMode.Dpi;
			ShowIcon = false;
			Icon = null;

			_icons = new IconProvider();
			_repository = new ScriptRepository(GetConnectionString, GetBaseFolder);
			_scriptService = new ScriptService(_repository);
			_previewService = new PreviewService(_repository);

			var header = CreateHeaderPanel();
			var inputs = CreateInputsPanel();

			_tabs = new TabControl { Dock = DockStyle.Fill };
			_tabs.TabPages.Add(CreateScriptsTab());
			_tabs.TabPages.Add(CreateHistoryTab());

			lblStatus = new Label
			{
				Dock = DockStyle.Bottom,
				Height = 96,
				Padding = new Padding(10, 6, 10, 6),
				Text = "Ready."
			};

			Controls.Add(_tabs);
			Controls.Add(inputs);
			Controls.Add(header);
			Controls.Add(lblStatus);

			KeyPreview = true;
			KeyDown += MainForm_KeyDown;
		}

		private string GetConnectionString() => txtConnectionString.Text;
		private string GetBaseFolder()
		{
			var raw = txtScriptsFolder?.Text;
			if (string.IsNullOrWhiteSpace(raw))
				return "";
			return Path.GetFullPath(raw);
		}

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

			lvScripts.Columns.Add("Select", 54);
			lvScripts.Columns.Add("", 40);
			lvScripts.Columns.Add("Script Name", 440);
			lvScripts.Columns.Add("Applied", 240);
			lvScripts.Columns.Add("Status", 160);

			_imgExecuted = _icons.Get("executed", 20);
			_imgPending = _icons.Get("pending", 20);
			_imgError = _icons.Get("error", 20);

			lvScripts.DrawColumnHeader += DrawScriptsColumnHeader;
			lvScripts.DrawSubItem += DrawScriptsSubItem;

			lvScripts.MouseMove += ScriptsMouseMove;
			lvScripts.MouseLeave += (s, e) => ResetScriptsHover();
			lvScripts.MouseUp += ScriptsMouseUp;
			lvScripts.ColumnClick += ScriptsHeaderClick;

			overlayPanel = BuildOverlay(out overlayLabel, out overlayProgress);
			lvScripts.Controls.Add(overlayPanel);

			var host = new Panel { Dock = DockStyle.Fill };
			scriptsTopSpacer = new Panel { Dock = DockStyle.Top, Height = 48 };
			host.Controls.Add(lvScripts);
			host.Controls.Add(scriptsTopSpacer);
			page.Controls.Add(host);
			return page;
		}

		private void DrawScriptsColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
		{
			if (e.ColumnIndex != 0)
			{
				e.DrawDefault = true;
				return;
			}

			bool themed = Application.RenderWithVisualStyles &&
						  VisualStyleRenderer.IsElementDefined(VisualStyleElement.Header.Item.Normal);
			if (themed)
			{
				var renderer = new VisualStyleRenderer(VisualStyleElement.Header.Item.Normal);
				renderer.DrawBackground(e.Graphics, e.Bounds);
			}
			else
			{
				e.Graphics.FillRectangle(SystemBrushes.Control, e.Bounds);
				ControlPaint.DrawBorder(e.Graphics, e.Bounds, SystemColors.ControlDark, ButtonBorderStyle.Solid);
			}

			bool enabled = _hasPendingScripts;
			var state = !enabled
				? (_scriptsSelectAll ? CheckBoxState.CheckedDisabled : CheckBoxState.UncheckedDisabled)
				: (_scriptsSelectAll ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal);

			// Create (missing) rectangle for checkbox glyph
			Size glyph = CheckBoxRenderer.GetGlyphSize(e.Graphics, state);
			var cb = new Rectangle(
				e.Bounds.X + (e.Bounds.Width - glyph.Width) / 2,
				e.Bounds.Y + (e.Bounds.Height - glyph.Height) / 2,
				glyph.Width,
				glyph.Height);

			CheckBoxRenderer.DrawCheckBox(e.Graphics, cb.Location, state);
		}

		// ColumnClick for scripts (sorting + select-all)
		private void ScriptsHeaderClick(object? sender, ColumnClickEventArgs e)
		{
			// Column 0: Select-All
			if (e.Column == 0)
			{
				if (!_hasPendingScripts) return;
				_scriptsSelectAll = !_scriptsSelectAll;
				foreach (ListViewItem it in lvScripts.Items)
					if (it.Tag is ScriptRowTag tag && tag.IsPending)
						tag.Selected = _scriptsSelectAll;
				lvScripts.Invalidate();
				return;
			}

			// Ignore icon column (1)
			if (e.Column == 1) return;

			// Sorting (columns 2,3,4)
			if (_scriptsSortColumn == e.Column)
				_scriptsSortAscending = !_scriptsSortAscending;
			else
			{
				_scriptsSortColumn = e.Column;
				_scriptsSortAscending = true;
			}
			SortScripts();
		}

		private void SortScripts()
		{
			if (_scriptsSortColumn < 0) return;

			var items = lvScripts.Items.Cast<ListViewItem>().ToList();
			IEnumerable<ListViewItem> ordered = items;

			switch (_scriptsSortColumn)
			{
				case 2: // Script Name
					ordered = _scriptsSortAscending
						? items.OrderBy(i => i.SubItems[2].Text, StringComparer.OrdinalIgnoreCase)
						: items.OrderByDescending(i => i.SubItems[2].Text, StringComparer.OrdinalIgnoreCase);
					break;

				case 3: // Applied (date; pending blank -> DateTime.MaxValue in ascending)
					DateTime GetApplied(ListViewItem it)
					{
						var t = it.SubItems[3].Text;
						if (string.IsNullOrWhiteSpace(t)) return DateTime.MaxValue;
						if (DateTime.TryParse(t, out var dt)) return dt;
						return DateTime.MaxValue;
					}
					ordered = _scriptsSortAscending
						? items.OrderBy(GetApplied).ThenBy(i => i.SubItems[2].Text, StringComparer.OrdinalIgnoreCase)
						: items.OrderByDescending(GetApplied).ThenByDescending(i => i.SubItems[2].Text, StringComparer.OrdinalIgnoreCase);
					break;

				case 4: // Status custom rank
					int Rank(ListViewItem it) => it.SubItems[4].Text.ToLowerInvariant() switch
					{
						"executed" => 0,
						"pending" => 1,
						"error" => 2,
						_ => 99
					};
					ordered = _scriptsSortAscending
						? items.OrderBy(Rank).ThenBy(i => i.SubItems[2].Text, StringComparer.OrdinalIgnoreCase)
						: items.OrderByDescending(Rank).ThenByDescending(i => i.SubItems[2].Text, StringComparer.OrdinalIgnoreCase);
					break;
			}

			lvScripts.BeginUpdate();
			lvScripts.Items.Clear();
			foreach (var it in ordered) lvScripts.Items.Add(it);
			lvScripts.EndUpdate();
		}

		private void DrawScriptsSubItem(object? sender, DrawListViewSubItemEventArgs e)
		{
			var tag = e?.Item?.Tag as ScriptRowTag;
			if (e?.ColumnIndex == 0)
			{
				e.Graphics.FillRectangle(SystemBrushes.Window, e.Bounds);
				if (tag?.IsPending == true)
				{
					Rectangle cb = new(
						e.Bounds.X + (e.Bounds.Width - 16) / 2,
						e.Bounds.Y + (e.Bounds.Height - 16) / 2,
						16,
						16);
					CheckBoxRenderer.DrawCheckBox(
						e.Graphics,
						cb.Location,
						tag.Selected ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal);
				}
				return;
			}
			if (e?.ColumnIndex == 1)
			{
				e.Graphics.FillRectangle(SystemBrushes.Window, e.Bounds);
				Image? img = null;
				if (tag != null)
				{
					if (string.Equals(tag.Status, "Error", StringComparison.OrdinalIgnoreCase))
						img = _imgError;
					else if (tag.IsPending)
						img = _imgPending;
					else
						img = _imgExecuted;
				}
				if (img != null)
				{
					int x = e.Bounds.X + (e.Bounds.Width - img.Width) / 2;
					int y = e.Bounds.Y + (e.Bounds.Height - img.Height) / 2;
					e.Graphics.DrawImage(img, x, y, img.Width, img.Height);
				}
				return;
			}
			if (e?.ColumnIndex == 2)
			{
				TextRenderer.DrawText(
					e.Graphics,
					e.SubItem?.Text,
					e.SubItem?.Font!,
					e.Bounds,
					Color.RoyalBlue,
					TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
				return;
			}
			e?.DrawText(TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
		}

		private void ScriptsMouseMove(object? sender, MouseEventArgs e)
		{
			var hit = lvScripts.HitTest(e.Location);
			bool onName = hit.Item != null && hit.SubItem != null && hit.Item.SubItems.IndexOf(hit.SubItem) == 2;
			if (onName != _scriptsHandCursor)
			{
				_scriptsHandCursor = onName;
				lvScripts.Cursor = onName ? Cursors.Hand : Cursors.Default;
			}

			if (onName && hit.Item != null && !ReferenceEquals(_lastTipItemScripts, hit.Item))
			{
				_lastTipItemScripts = hit.Item;
				string fileName = hit.Item.SubItems[2].Text;
				string preview = _previewService.GetPreviewByFileName(fileName);
				if (!string.IsNullOrEmpty(preview))
					ttScripts.Show(preview, lvScripts, e.Location + new Size(16, 20), 15000);
			}
		}

		private void ScriptsMouseUp(object? sender, MouseEventArgs e)
		{
			var hit = lvScripts.HitTest(e.Location);
			if (hit.Item == null || hit.SubItem == null) return;
			int idx = hit.Item.SubItems.IndexOf(hit.SubItem);
			if (idx == 0 && hit.Item.Tag is ScriptRowTag tag && tag.IsPending)
			{
				tag.Selected = !tag.Selected;
				if (!tag.Selected) _scriptsSelectAll = false;
				lvScripts.Invalidate(hit.SubItem.Bounds);
				return;
			}
			if (e.Button == MouseButtons.Left && idx == 2)
			{
				string fileName = hit.Item.SubItems[2].Text;
				string content = _repository.GetScriptContentByNameOrFile(fileName);
				using var dlg = new ScriptViewer(fileName, content);
				dlg.ShowDialog(this);
			}
		}

		private void ResetScriptsHover()
		{
			_scriptsHandCursor = false;
			lvScripts.Cursor = Cursors.Default;
			_lastTipItemScripts = null;
			ttScripts.Hide(lvScripts);
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
			lvHistory.ColumnClick += HistoryHeaderClick;

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
			using var dlg = new ScriptViewer(hit.Item.SubItems[1].Text, sql);
			dlg.ShowDialog(this);
		}

		private void ResetHistoryHover()
		{
			_historyHandCursor = false;
			lvHistory.Cursor = Cursors.Default;
			_lastTipItemHistory = null;
			ttHistory.Hide(lvHistory);
		}

		private void HistoryHeaderClick(object? sender, ColumnClickEventArgs e)
		{
			if (_historySortColumn == e.Column)
				_historySortAscending = !_historySortAscending;
			else
			{
				_historySortColumn = e.Column;
				_historySortAscending = true;
			}
			SortHistory();
		}

		private void SortHistory()
		{
			if (_historySortColumn < 0) return;
			var items = lvHistory.Items.Cast<ListViewItem>().ToList();
			IEnumerable<ListViewItem> ordered = items;

			switch (_historySortColumn)
			{
				case 0: // Id numeric
					int GetId(ListViewItem it) => int.TryParse(it.SubItems[0].Text, out var v) ? v : int.MaxValue;
					ordered = _historySortAscending ? items.OrderBy(GetId) : items.OrderByDescending(GetId);
					break;
				case 1: // Script Name
					ordered = _historySortAscending
						? items.OrderBy(i => i.SubItems[1].Text, StringComparer.OrdinalIgnoreCase)
						: items.OrderByDescending(i => i.SubItems[1].Text, StringComparer.OrdinalIgnoreCase);
					break;
				case 2: // Applied date
					DateTime GetApplied(ListViewItem it)
					{
						var t = it.SubItems[2].Text;
						if (string.IsNullOrWhiteSpace(t)) return DateTime.MaxValue;
						if (DateTime.TryParse(t, out var dt)) return dt;
						return DateTime.MaxValue;
					}
					ordered = _historySortAscending
						? items.OrderBy(GetApplied).ThenBy(i => i.SubItems[1].Text, StringComparer.OrdinalIgnoreCase)
						: items.OrderByDescending(GetApplied).ThenByDescending(i => i.SubItems[1].Text, StringComparer.OrdinalIgnoreCase);
					break;
				case 3: // Executed By
				case 4: // Machine
				case 5: // Path
					ordered = _historySortAscending
						? items.OrderBy(i => i.SubItems[_historySortColumn].Text, StringComparer.OrdinalIgnoreCase)
						: items.OrderByDescending(i => i.SubItems[_historySortColumn].Text, StringComparer.OrdinalIgnoreCase);
					break;
				default:
					return;
			}

			lvHistory.BeginUpdate();
			lvHistory.Items.Clear();
			foreach (var it in ordered) lvHistory.Items.Add(it);
			lvHistory.EndUpdate();
		}

		private async Task LoadScriptsAsync(bool showMessage)
		{
			lvScripts.Items.Clear();
			btnRun.Enabled = false;
			_scriptsSelectAll = false;
			_hasPendingScripts = false;
			_scriptsSortColumn = -1;

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
					var item = new ListViewItem { Text = "" };
					item.SubItems.Add("");
					item.SubItems.Add(row.ScriptName);
					item.SubItems.Add(row.Applied?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "");
					item.SubItems.Add("Executed");
					item.Tag = new ScriptRowTag { IsPending = false, Selected = false, FileName = row.ScriptName, Status = "Executed" };
					lvScripts.Items.Add(item);
				}

				foreach (var file in result.PendingFiles)
				{
					var item = new ListViewItem { Text = "" };
					item.SubItems.Add("");
					item.SubItems.Add(file);
					item.SubItems.Add("");
					item.SubItems.Add("Pending");
					item.Tag = new ScriptRowTag { IsPending = true, Selected = false, FileName = file, Status = "Pending" };
					lvScripts.Items.Add(item);
				}

				_hasPendingScripts = result.PendingFiles.Count > 0;
				btnRun.Enabled = _hasPendingScripts;
				SetStatus(result.Executed.Count, result.PendingFiles.Count);

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
				lvScripts.Invalidate();
			}
		}

		private async Task RunPendingAsync()
		{
			if (!Directory.Exists(GetBaseFolder()))
			{
				MessageBox.Show("Folder not found.");
				return;
			}
			if (!_hasPendingScripts)
			{
				MessageBox.Show("No pending scripts available.", "Info");
				return;
			}
			var selected = GetSelectedPendingScripts();
			if (selected.Count == 0)
			{
				MessageBox.Show("No pending scripts selected.", "Info");
				return;
			}

			string previewList = string.Join(Environment.NewLine, selected.Take(20));
			if (selected.Count > 20)
				previewList += Environment.NewLine + $"... (+{selected.Count - 20} more)";
			string confirmMsg =
				"Are you sure you want to rexecute un the selected scripts?" +
				Environment.NewLine + Environment.NewLine + previewList;

			var answer = MessageBox.Show(
				confirmMsg,
				"Confirm Execution",
				MessageBoxButtons.YesNo,
				MessageBoxIcon.Question,
				MessageBoxDefaultButton.Button2);

			if (answer != DialogResult.Yes) return;

			ShowScriptsLoader("Running scripts...");
			btnLoad.Enabled = false;
			btnRun.Enabled = false;

			try
			{
				var execResult = await _scriptService.ExecutePendingAsync(
					GetConnectionString(),
					GetBaseFolder(),
					new WinFormsLog(this),
					selected);

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

				MessageBox.Show("Selected pending scripts executed successfully.", "Success");
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
				btnRun.Enabled = _hasPendingScripts;
			}
		}

		private List<string> GetSelectedPendingScripts()
		{
			var list = new List<string>();
			foreach (ListViewItem it in lvScripts.Items)
				if (it.Tag is ScriptRowTag tag && tag.IsPending && tag.Selected)
					list.Add(tag.FileName);
			return list;
		}

		private async Task LoadHistoryAsync()
		{
			lvHistory.Items.Clear();
			_historySortColumn = -1;
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
				if (string.Equals(it.SubItems[2].Text, fileNameOnly, StringComparison.OrdinalIgnoreCase))
				{
					if (it.Tag is ScriptRowTag tag)
					{
						tag.Status = "Error";
						tag.Selected = false;
					}
					it.ForeColor = Color.Red;
					it.SubItems[4].Text = "Error";
					if (!string.IsNullOrWhiteSpace(errorText))
						it.ToolTipText = errorText;
					it.Selected = true;
					it.EnsureVisible();
					break;
				}
			}
			lvScripts.Invalidate();
		}

		private void SetStatus(int executed, int pending) =>
			lblStatus.Text = $"Loaded.{Environment.NewLine}Executed: {executed}{Environment.NewLine}Pending: {pending}";

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

		private Panel BuildOverlay(out Label label, out ProgressBar progress)
		{
			var panel = new Panel
			{
				Dock = DockStyle.Fill,
				BackColor = Color.FromArgb(128, Color.LightGray),
				Visible = false
			};

			label = new Label
			{
				AutoSize = false,
				Dock = DockStyle.Top,
				Height = 32,
				Font = new Font("Segoe UI", 12f, FontStyle.Bold),
				ForeColor = Color.Black,
				BackColor = Color.Transparent
			};

			progress = new ProgressBar
			{
				Dock = DockStyle.Top,
				Height = 24,
				Style = ProgressBarStyle.Blocks
			};

			panel.Controls.Add(progress);
			panel.Controls.Add(label);
			panel.Controls.SetChildIndex(label, 0);

			return panel;
		}

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

		private void ToggleSelectAllScripts()
		{
			if (!_hasPendingScripts || lvScripts.Items.Count == 0) return;
			_scriptsSelectAll = !_scriptsSelectAll;
			foreach (ListViewItem it in lvScripts.Items)
			{
				if (it.Tag is ScriptRowTag tag && tag.IsPending)
					tag.Selected = _scriptsSelectAll;
			}
			lvScripts.Invalidate(); // redraw header + checkboxes
		}

		private async void MainForm_KeyDown(object? sender, KeyEventArgs e)
		{
			// Esc: close
			if (e.KeyCode == Keys.Escape)
			{
				e.SuppressKeyPress = true;
				Close();
				return;
			}

			// Ctrl+A: toggle select/unselect all pending scripts (Scripts tab only)
			if (e.Control && e.KeyCode == Keys.A)
			{
				bool scriptsTabActive = _tabs.SelectedTab != null &&
					string.Equals(_tabs.SelectedTab.Text, "Scripts", StringComparison.OrdinalIgnoreCase);
				if (scriptsTabActive && _hasPendingScripts && lvScripts.Items.Count > 0)
				{
					e.SuppressKeyPress = true;
					ToggleSelectAllScripts();
					return;
				}
			}

			// Ctrl+B: browse for folder
			if (e.Control && e.KeyCode == Keys.B)
			{
				e.SuppressKeyPress = true;
				if (btnBrowse.Enabled)
					btnBrowse.PerformClick();
				return;
			}

			// Ctrl+L: load scripts
			if (e.Control && e.KeyCode == Keys.L)
			{
				e.SuppressKeyPress = true;
				if (btnLoad.Enabled)
					await LoadScriptsAsync(true);
				return;
			}

			// Ctrl+R: run pending
			if (e.Control && e.KeyCode == Keys.R)
			{
				e.SuppressKeyPress = true;
				if (btnRun.Enabled)
					await RunPendingAsync();
				return;
			}
		}
	}
}