using DbUp;
using DbUp.Engine.Output;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Reflection;

namespace Scripter
{
	public class MainForm : Form
	{
		// UI
		private TextBox txtConnectionString = null!;
		private TextBox txtScriptsFolder = null!;
		private Button btnBrowse = null!;
		private Button btnLoad = null!;
		private Button btnRun = null!;
		private ListView lvScripts = null!;
		private ListView lvHistory = null!;
		private Button btnHistoryRefresh = null!;
		private Label lblStatus;
		private FolderBrowserDialog folderDialog = new();
		private ImageList statusImages = null!;

		// loader overlay (Scripts tab)
		private Panel overlayPanel = null!;
		private Label overlayLabel = null!;
		private ProgressBar overlayProgress = null!;

		// loader overlay (History tab)
		private Panel overlayHistoryPanel = null!;
		private Panel scriptsTopSpacer = null!;
		private Label overlayHistoryLabel = null!;
		private ProgressBar overlayHistoryProgress = null!;

		// hover preview
		private readonly ToolTip ttScripts = new() { InitialDelay = 200, ReshowDelay = 100, AutoPopDelay = 15000, UseAnimation = true, UseFading = true, ShowAlways = false };
		private readonly ToolTip ttHistory = new() { InitialDelay = 200, ReshowDelay = 100, AutoPopDelay = 15000, UseAnimation = true, UseFading = true, ShowAlways = false };
		private ListViewItem? _lastTipItemScripts;
		private ListViewItem? _lastTipItemHistory;

		// Layout constants (tight spacing)
		private const int GapYSmall = 0;
		private const int GapY = 1;
		private const int GapX = 6;

		// Cached logo
		private Image? _logoImage;

		// icon cache
		private readonly Dictionary<string, Image> _iconCache = new(StringComparer.OrdinalIgnoreCase);

		// cursor state
		private bool _scriptsHandCursor = false;
		private bool _historyHandCursor = false;

		public MainForm()
		{
			Text = "";
			StartPosition = FormStartPosition.CenterScreen;
			Width = 920;
			Height = 1000;
			AutoScaleMode = AutoScaleMode.Dpi;

			ShowIcon = false;
			Icon = null;

			var header = CreateHeaderPanel();
			var inputs = CreateInputsPanel();

			// Tabs
			var tabs = new TabControl { Dock = DockStyle.Fill };
			tabs.TabPages.Add(CreateScriptsTab());
			tabs.TabPages.Add(CreateHistoryTab());

			lblStatus = new Label
			{
				Dock = DockStyle.Bottom,
				AutoSize = false,
				Height = 96,
				Padding = new Padding(10, 6, 10, 6),
				TextAlign = ContentAlignment.MiddleLeft,
				AutoEllipsis = false,
				Text = "Ready."
			};

			Controls.Add(tabs);    // Fill (tabs host both tables)
			Controls.Add(inputs);  // Top
			Controls.Add(header);  // Top
			Controls.Add(lblStatus); // Bottom
		}

		// ---------- UI builders ----------

		private Panel CreateHeaderPanel()
		{
			var header = new Panel
			{
				Dock = DockStyle.Top,
				Height = 42,
				Padding = new Padding(12, 6, 0, 0),
				Margin = Padding.Empty,
				BackColor = BackColor
			};

			_logoImage ??= LoadLogoPng();

			header.Paint += (s, e) =>
			{
				if (_logoImage == null) return;

				int availableH = header.Height - header.Padding.Top - header.Padding.Bottom;
				int desiredH = Math.Min(availableH, 44);
				float scale = (float)desiredH / _logoImage.Height;

				int drawW = (int)Math.Round(_logoImage.Width * scale);
				int drawH = (int)Math.Round(_logoImage.Height * scale);
				int x = header.Padding.Left;
				int y = header.Padding.Top + (availableH - drawH) / 2;

				var g = e.Graphics;
				g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
				g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
				g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

				using var bg = new SolidBrush(header.BackColor);
				g.FillRectangle(bg, header.ClientRectangle);
				g.DrawImage(_logoImage, new Rectangle(x, y, drawW, drawH));
			};

			return header;
		}

		private TableLayoutPanel CreateInputsPanel()
		{
			var panel = new TableLayoutPanel
			{
				Dock = DockStyle.Top,
				AutoSize = true,
				AutoSizeMode = AutoSizeMode.GrowAndShrink,
				Padding = new Padding(4, 2, 4, 0),
				Margin = new Padding(0),
				ColumnCount = 3
			};
			panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

			var lblConn = new Label
			{
				Text = "Connection string:",
				Anchor = AnchorStyles.Left,
				AutoSize = true,
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
				AutoSize = true,
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
				AutoSizeMode = AutoSizeMode.GrowAndShrink,
				Margin = Padding.Empty,
				Image = GetIcon("browse", 18),
				TextImageRelation = TextImageRelation.ImageBeforeText,
				ImageAlign = ContentAlignment.MiddleLeft,
				TextAlign = ContentAlignment.MiddleRight
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
				AutoSizeMode = AutoSizeMode.GrowAndShrink,
				WrapContents = false,
				Margin = new Padding(0, 0, GapX, 0),
				Anchor = AnchorStyles.Left
			};

			btnLoad = new Button
			{
				Text = "Load Scripts",
				AutoSize = true,
				AutoSizeMode = AutoSizeMode.GrowAndShrink,
				Margin = new Padding(0, 0, 8, 0),
				Image = GetIcon("refresh", 18),
				TextImageRelation = TextImageRelation.ImageBeforeText,
				ImageAlign = ContentAlignment.MiddleLeft,
				TextAlign = ContentAlignment.MiddleRight
			};
			btnLoad.Click += async (s, e) => await LoadScriptsAsync(showMessage: true);

			btnRun = new Button
			{
				Text = "Run Pending",
				AutoSize = true,
				AutoSizeMode = AutoSizeMode.GrowAndShrink,
				Enabled = false,
				Image = GetIcon("play", 18),
				TextImageRelation = TextImageRelation.ImageBeforeText,
				ImageAlign = ContentAlignment.MiddleLeft,
				TextAlign = ContentAlignment.MiddleRight
			};
			btnRun.Click += async (s, e) => await RunPendingAsync();

			actions.Controls.AddRange(new Control[] { btnLoad, btnRun });

			panel.Controls.Add(lblConn, 0, 0);
			panel.Controls.Add(txtConnectionString, 1, 0);
			panel.Controls.Add(new Panel { Width = 1, Margin = Padding.Empty }, 2, 0);

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
				Font = new Font("Segoe UI", 10f, FontStyle.Regular),
				OwnerDraw = true
			};

			lvScripts.Columns.Add("", 40, HorizontalAlignment.Center);
			lvScripts.Columns.Add("Script Name", 440, HorizontalAlignment.Left);
			lvScripts.Columns.Add("Applied", 240, HorizontalAlignment.Left);
			lvScripts.Columns.Add("Status", 160, HorizontalAlignment.Left);

			lvScripts.SmallImageList = new ImageList { ImageSize = new Size(20, 20), ColorDepth = ColorDepth.Depth32Bit };
			statusImages = lvScripts.SmallImageList;
			statusImages.Images.Add("executed", GetIcon("executed", 20));
			statusImages.Images.Add("pending", GetIcon("pending", 20));
			statusImages.Images.Add("error", GetIcon("error", 20));

			// headers default
			lvScripts.DrawColumnHeader += (s, e) => e.DrawDefault = true;

			// custom draw for: icon column centered + script name plav (bez underline)
			lvScripts.DrawSubItem += (s, e) =>
			{
				if (e.ColumnIndex == 0 && e.Item?.ImageList != null)
				{
					var img = e.Item.ImageList.Images[e.Item.ImageKey];
					if (img != null)
					{
						int x = e.Bounds.X + (e.Bounds.Width - img.Width) / 2;
						int y = e.Bounds.Y + (e.Bounds.Height - img.Height) / 2;
						e.Graphics.DrawImage(img, x, y, img.Width, img.Height);
					}
				}
				else if (e.ColumnIndex == 1)
				{
					TextRenderer.DrawText(
						e.Graphics,
						e.SubItem.Text,
						e.SubItem.Font!, // regular
						e.Bounds,
						Color.RoyalBlue,
						TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
				}
				else
				{
					e.DrawText(TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
				}
			};

			// hover hand + preview tooltip
			lvScripts.MouseMove += (s, e) =>
			{
				var hit = lvScripts.HitTest(e.Location);
				bool onName = hit.Item != null && hit.SubItem != null && hit.Item.SubItems.IndexOf(hit.SubItem) == 1;
				if (onName != _scriptsHandCursor)
				{
					_scriptsHandCursor = onName;
					lvScripts.Cursor = onName ? Cursors.Hand : Cursors.Default;
				}

				// tooltip samo kada smo na novom itemu
				if (onName && hit.Item != null && !ReferenceEquals(_lastTipItemScripts, hit.Item))
				{
					_lastTipItemScripts = hit.Item;
					string fileName = hit.Item.SubItems[1].Text;
					string preview = GetPreviewTextByName(fileName);
					if (!string.IsNullOrEmpty(preview))
					{
						ttScripts.Show(preview, lvScripts, e.Location + new Size(16, 20), 15000);
					}
				}
			};
			lvScripts.MouseLeave += (s, e) =>
			{
				_scriptsHandCursor = false;
				lvScripts.Cursor = Cursors.Default;
				_lastTipItemScripts = null;
				ttScripts.Hide(lvScripts);
			};

			// open modal on click Script Name
			lvScripts.MouseUp += async (s, e) =>
			{
				if (e.Button != MouseButtons.Left) return;
				var hit = lvScripts.HitTest(e.Location);
				if (hit.Item == null || hit.SubItem == null) return;
				if (hit.Item.SubItems.IndexOf(hit.SubItem) != 1) return; // only "Script Name"

				string fileName = hit.Item.SubItems[1].Text;
				string title = fileName;
				string content = await Task.Run(() => GetScriptContentByNameOrFile(fileName));

				using var dlg = new ScriptViewerForm(title, content);
				dlg.ShowDialog(this);
			};

			// overlay for Scripts
			overlayPanel = BuildOverlay(out overlayLabel, out overlayProgress);
			lvScripts.Controls.Add(overlayPanel);

			var host = new Panel { Dock = DockStyle.Fill };
			scriptsTopSpacer = new Panel { Dock = DockStyle.Top, Height = 48 };

			host.Controls.Add(lvScripts);
			host.Controls.Add(scriptsTopSpacer); // Top

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
			layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

			btnHistoryRefresh = new Button
			{
				Text = "Refresh",
				AutoSize = true,
				Image = GetIcon("refresh", 18),
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
				Font = new Font("Segoe UI", 10f, FontStyle.Regular),
				OwnerDraw = true
			};
			lvHistory.Columns.Add("Id", 100, HorizontalAlignment.Left);
			lvHistory.Columns.Add("Script Name", 440, HorizontalAlignment.Left);
			lvHistory.Columns.Add("Applied", 240, HorizontalAlignment.Left);
			lvHistory.Columns.Add("Executed By", 140, HorizontalAlignment.Left);
			lvHistory.Columns.Add("Machine", 180, HorizontalAlignment.Left);
			lvHistory.Columns.Add("Path", 640, HorizontalAlignment.Left);

			overlayHistoryPanel = BuildOverlay(out overlayHistoryLabel, out overlayHistoryProgress);
			lvHistory.Controls.Add(overlayHistoryPanel);

			// custom draw: plav tekst za Script Name
			lvHistory.DrawColumnHeader += (s, e) => e.DrawDefault = true;
			lvHistory.DrawSubItem += (s, e) =>
			{
				if (e.ColumnIndex == 1)
				{
					TextRenderer.DrawText(
						e.Graphics,
						e.SubItem.Text,
						e.SubItem.Font!,
						e.Bounds,
						Color.RoyalBlue,
						TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
				}
				else
				{
					e.DrawText(TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
				}
			};

			// hover hand + tooltip preview
			lvHistory.MouseMove += (s, e) =>
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
					string preview = GetPreviewTextById(id);
					if (!string.IsNullOrEmpty(preview))
					{
						ttHistory.Show(preview, lvHistory, e.Location + new Size(16, 20), 15000);
					}
				}
			};
			lvHistory.MouseLeave += (s, e) =>
			{
				_historyHandCursor = false;
				lvHistory.Cursor = Cursors.Default;
				_lastTipItemHistory = null;
				ttHistory.Hide(lvHistory);
			};

			// open modal on click Script Name
			lvHistory.MouseUp += (s, e) =>
			{
				if (e.Button != MouseButtons.Left) return;
				var hit = lvHistory.HitTest(e.Location);
				if (hit.Item == null || hit.SubItem == null) return;
				if (hit.Item.SubItems.IndexOf(hit.SubItem) != 1) return; // only "Script Name"

				int id = int.TryParse(hit.Item.SubItems[0].Text, out var v) ? v : 0;
				string title = hit.Item.SubItems[1].Text;
				string sql = GetScriptContentById(id);

				using var dlg = new ScriptViewerForm(title, sql);
				dlg.ShowDialog(this);
			};

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

			var center = new FlowLayoutPanel
			{
				FlowDirection = FlowDirection.TopDown,
				AutoSize = true,
				AutoSizeMode = AutoSizeMode.GrowAndShrink,
				Anchor = AnchorStyles.None,
				Padding = new Padding(16)
			};

			label = new Label
			{
				Text = "Working...",
				AutoSize = true,
				Font = new Font("Segoe UI", 10, FontStyle.Regular),
				TextAlign = ContentAlignment.MiddleCenter
			};

			bar = new ProgressBar
			{
				Style = ProgressBarStyle.Marquee,
				MarqueeAnimationSpeed = 35,
				Width = 260
			};

			center.Controls.Add(label);
			center.Controls.Add(bar);

			var filler = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 };
			filler.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
			filler.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			filler.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
			filler.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
			filler.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			filler.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
			filler.Controls.Add(center, 1, 1);

			panel.Controls.Add(filler);
			return panel;
		}

		// ---------- Actions (async) ----------

		private async Task LoadScriptsAsync(bool showMessage)
		{
			lvScripts.Items.Clear();
			btnRun.Enabled = false;

			if (!Directory.Exists(txtScriptsFolder.Text))
			{
				MessageBox.Show("Folder not found.");
				return;
			}

			ShowLoader("Loading scripts...");
			btnLoad.Enabled = false;
			btnRun.Enabled = false;

			try
			{
				string connStr = txtConnectionString.Text;
				string baseFolder = Path.GetFullPath(txtScriptsFolder.Text);
				string basePrefix = baseFolder.EndsWith(Path.DirectorySeparatorChar.ToString()) ? baseFolder : baseFolder + Path.DirectorySeparatorChar;

				var (executedRows, pendingFiles) = await Task.Run(() =>
				{
					var upgrader =
						DeployChanges.To
							.SqlDatabase(connStr)
							.WithScripts(new FullPathFileSystemScriptProvider(
								baseFolder,
								pattern: "*.sql",
								includeSubdirs: true,
								sortBy: ScriptSortBy.CreatedUtc))
							.JournalTo(new CustomJournal("scripts", "DbMigrationHistory", baseFolder, connStr))
							.LogTo(BuildLogger())
							.Build();

					_ = upgrader.GetExecutedScripts();

					var executedRowsLocal = FetchExecutedRows(baseFolder, basePrefix);

					var executedKeys = executedRowsLocal
						.Select(r => ScriptKey.Make(r.Path, r.ScriptName))
						.ToHashSet(StringComparer.OrdinalIgnoreCase);

					var pendingScripts = upgrader.GetScriptsToExecute().ToList();

					var pendingFilesLocal = pendingScripts
						.OrderBy(s => GetScriptFileTimeUtc(s.Name))
						.ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
						.Where(s => !executedKeys.Contains(s.Name))
						.Select(s => ScriptKey.Split(s.Name).file)
						.ToList();

					return (executedRowsLocal, pendingFilesLocal);
				});

				foreach (var row in executedRows
					.OrderBy(r => r.Applied ?? DateTime.MaxValue)
					.ThenBy(r => r.ScriptName, StringComparer.OrdinalIgnoreCase))
				{
					var item = new ListViewItem { ImageKey = "executed", Text = "" };
					item.SubItems.Add(row.ScriptName);
					item.SubItems.Add(row.Applied?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "");
					item.SubItems.Add("Executed");
					lvScripts.Items.Add(item);
				}

				foreach (var file in pendingFiles)
				{
					var item = new ListViewItem { ImageKey = "pending", Text = "" };
					item.SubItems.Add(file);
					item.SubItems.Add("");
					item.SubItems.Add("Pending");
					lvScripts.Items.Add(item);
				}

				SetStatus(executedRows.Count, pendingFiles.Count);
				btnRun.Enabled = pendingFiles.Count > 0;

				if (showMessage)
					MessageBox.Show($"Executed: {executedRows.Count}\nPending: {pendingFiles.Count}", "Scripts loaded");
			}
			catch (Exception ex)
			{
				ShowError("Error", ex);
			}
			finally
			{
				HideLoader();
				btnLoad.Enabled = true;
			}
		}

		private async Task RunPendingAsync()
		{
			if (!Directory.Exists(txtScriptsFolder.Text))
			{
				MessageBox.Show("Folder not found.");
				return;
			}

			ShowLoader("Running scripts...");
			btnLoad.Enabled = false;
			btnRun.Enabled = false;

			try
			{
				string connStr = txtConnectionString.Text;
				string basePath = txtScriptsFolder.Text;

				var (success, errorFile, error) = await Task.Run(() =>
				{
					var probe =
						DeployChanges.To
							.SqlDatabase(connStr)
							.WithScripts(new FullPathFileSystemScriptProvider(
								basePath,
								"*.sql",
								includeSubdirs: true,
								sortBy: ScriptSortBy.CreatedUtc))
							.JournalTo(new CustomJournal("scripts", "DbMigrationHistory", basePath, connStr))
							.LogTo(BuildLogger())
							.Build();

					var pendingAll = probe.GetScriptsToExecute().ToList();

					var pendingSorted = pendingAll
						.OrderBy(s => GetScriptFileTimeUtc(s.Name))
						.ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
						.ToList();

					if (pendingSorted.Count == 0)
						return (true, (string?)null, (Exception?)null);

					foreach (var script in pendingSorted)
					{
						var singleRunner =
							DeployChanges.To
								.SqlDatabase(connStr)
								.WithScripts(new[] { script })
								.JournalTo(new CustomJournal("scripts", "DbMigrationHistory", basePath, connStr))
								.LogTo(BuildLogger())
								.Build();

						var res = singleRunner.PerformUpgrade();
						if (!res.Successful)
						{
							var key = res.ErrorScript?.Name;
							var (_, file) = ScriptKey.Split(key ?? "");
							return (false, file, res.Error);
						}
					}

					return (true, (string?)null, (Exception?)null);
				});

				if (!success)
				{
					await LoadScriptsAsync(showMessage: false);
					if (!string.IsNullOrEmpty(errorFile))
						MarkErrorRow(errorFile!, error?.Message);

					var header = !string.IsNullOrEmpty(errorFile)
						? $"Failed while executing script:\r\n{errorFile}\r\n\r\n"
						: "Execution failed.\r\n\r\n";

					ShowError("Execution error", error!, header);
					return;
				}

				MessageBox.Show("All pending scripts executed successfully.", "Success");
				await LoadScriptsAsync(showMessage: false);
			}
			catch (Exception ex)
			{
				ShowError("Error", ex);
			}
			finally
			{
				HideLoader();
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
				string connStr = txtConnectionString.Text;

				var rows = await Task.Run(() =>
				{
					var list = new List<(string Id, string Script, DateTime AppliedUtc, string? By, string? Machine, string? Path)>();
					using var conn = new SqlConnection(connStr);
					conn.Open();
					using var cmd = conn.CreateCommand();
					cmd.CommandText = @"
						SELECT [Id],[ScriptName],[Applied],[ExecutedBy],[MachineName],[Path]
						FROM [scripts].[DbMigrationHistory]";
					using var r = cmd.ExecuteReader();
					while (r.Read())
					{
						var id = r.IsDBNull(0) ? "" : (r.GetValue(0)?.ToString() ?? "");
						var script = r.IsDBNull(1) ? "" : r.GetString(1);
						var applied = r.IsDBNull(2) ? DateTime.MinValue : r.GetDateTime(2).ToUniversalTime();
						var by = r.IsDBNull(3) ? null : r.GetString(3);
						var machine = r.IsDBNull(4) ? null : r.GetString(4);
						var path = r.IsDBNull(5) ? null : r.GetString(5);

						list.Add((id, script, applied, by, machine, path));
					}
					return list
						.OrderByDescending(x => x.AppliedUtc)
						.ThenBy(x => x.Script, StringComparer.OrdinalIgnoreCase)
						.ToList();
				});

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

		// ---------- Infrastructure ----------

		private IUpgradeLog BuildLogger() => new WinFormsLog(this);

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

		private sealed class ExecutedRow
		{
			public string ScriptName { get; set; } = "";
			public DateTime? Applied { get; set; }
			public string Path { get; set; } = "";
		}

		private List<ExecutedRow> FetchExecutedRows(string baseFolder, string basePrefix)
		{
			var list = new List<ExecutedRow>();
			using var conn = new SqlConnection(txtConnectionString.Text);
			conn.Open();

			using var cmd = conn.CreateCommand();
			cmd.CommandText = "SELECT [ScriptName],[Applied],[Path] FROM [scripts].[DbMigrationHistory]";
			using var r = cmd.ExecuteReader();
			while (r.Read())
			{
				var script = r.IsDBNull(0) ? "" : r.GetString(0);
				var applied = r.IsDBNull(1) ? (DateTime?)null : r.GetDateTime(1);
				var path = r.IsDBNull(2) ? "" : r.GetString(2);

				var folder = Path.GetFullPath(path ?? "");
				if (folder.Equals(baseFolder, StringComparison.OrdinalIgnoreCase) ||
					folder.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase))
				{
					list.Add(new ExecutedRow { ScriptName = script, Applied = applied, Path = folder });
				}
			}
			return list;
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

		private static DateTime GetScriptFileTimeUtc(string scriptKey)
		{
			var (folder, file) = ScriptKey.Split(scriptKey);
			var fullPath = Path.Combine(folder ?? "", file ?? "");
			DateTime t = File.GetCreationTimeUtc(fullPath);
			if (t <= DateTime.MinValue.AddDays(1))
				t = File.GetLastWriteTimeUtc(fullPath);
			return t;
		}

		private void SetStatus(int executed, int pending) =>
			lblStatus.Text = $"Loaded.{Environment.NewLine}Executed: {executed}{Environment.NewLine}Pending: {pending}";

		// ---------- Utilities ----------

		private static Image? LoadLogoPng()
		{
			try
			{
				var asm = Assembly.GetExecutingAssembly();
				string ns = typeof(MainForm).Namespace ?? "Scripter";
				string resource = $"{ns}.Resources.scripter-logo.png";
				using var s = asm.GetManifestResourceStream(resource);
				if (s != null) return Image.FromStream(s);
			}
			catch { }

			try
			{
				var path = Path.Combine(AppContext.BaseDirectory, "Resources", "scripter-logo.png");
				if (File.Exists(path)) return Image.FromFile(path);
			}
			catch { }

			return null;
		}

		private Image GetIcon(string name, int size)
		{
			string key = $"{name}@{size}";
			if (_iconCache.TryGetValue(key, out var existing))
				return existing;

			Stream? stream = null;

			var asm = Assembly.GetExecutingAssembly();
			string ns = typeof(MainForm).Namespace ?? "Scripter";
			string resource = $"{ns}.Resources.icons.{name}.png";
			stream = asm.GetManifestResourceStream(resource);

			if (stream == null)
			{
				string path = Path.Combine(AppContext.BaseDirectory, "Resources", "icons", $"{name}.png");
				if (File.Exists(path))
					stream = File.OpenRead(path);
			}

			if (stream == null)
				throw new FileNotFoundException($"Icon not found: {name}.png");

			using (stream)
			using (var img = Image.FromStream(stream))
			{
				var scaled = new Bitmap(img, new Size(size, size));
				_iconCache[key] = scaled;
				return scaled;
			}
		}

		// loaders
		private void ShowLoader(string text)
		{
			overlayLabel.Text = text;
			overlayPanel.Visible = true;
			overlayPanel.BringToFront();
			overlayProgress.Style = ProgressBarStyle.Marquee;
		}
		private void HideLoader()
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

		// ---------- Content helpers ----------
		private string GetScriptContentById(int id)
		{
			if (id <= 0) return string.Empty;
			try
			{
				using var conn = new SqlConnection(txtConnectionString.Text);
				conn.Open();
				using var cmd = conn.CreateCommand();
				cmd.CommandText = "SELECT [Content] FROM [scripts].[DbMigrationHistory] WHERE [Id]=@id";
				cmd.Parameters.Add(new SqlParameter("@id", id));
				var o = cmd.ExecuteScalar();
				return o as string ?? string.Empty;
			}
			catch { return string.Empty; }
		}

		private string GetScriptContentByNameOrFile(string fileName)
		{
			try
			{
				using var conn = new SqlConnection(txtConnectionString.Text);
				conn.Open();
				using var cmd = conn.CreateCommand();
				cmd.CommandText = @"
					SELECT TOP(1) [Content]
					FROM [scripts].[DbMigrationHistory]
					WHERE [ScriptName] = @name AND (@path = '' OR [Path] LIKE @path + '%')
					ORDER BY [Applied] DESC";
				cmd.Parameters.Add(new SqlParameter("@name", fileName));
				string baseFolder = "";
				try { baseFolder = Path.GetFullPath(txtScriptsFolder.Text ?? ""); } catch { }
				cmd.Parameters.Add(new SqlParameter("@path", baseFolder ?? string.Empty));
				var o = cmd.ExecuteScalar();
				var content = o as string;
				if (!string.IsNullOrEmpty(content)) return content!;
			}
			catch { /* ignore and fallback */ }

			// 2) fallback: pronađi fajl na disku (rekurzivno)
			try
			{
				var root = txtScriptsFolder.Text;
				if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
				{
					var matches = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
					var full = matches.FirstOrDefault();
					if (!string.IsNullOrEmpty(full))
						return File.ReadAllText(full);
				}
			}
			catch { }

			return string.Empty;
		}

		private static string NormalizePreview(string? sql, int maxLen = 1000)
		{
			if (string.IsNullOrEmpty(sql)) return string.Empty;
			string s = sql!;
			if (s.Length > maxLen) s = s.Substring(0, maxLen) + " …";
			return s.Trim();
		}

		private string GetPreviewTextById(int id) => NormalizePreview(GetScriptContentById(id));
		private string GetPreviewTextByName(string fileName) => NormalizePreview(GetScriptContentByNameOrFile(fileName));
	}
}