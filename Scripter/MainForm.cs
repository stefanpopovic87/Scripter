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
		private Label lblStatus;
		private FolderBrowserDialog folderDialog = new();
		private ImageList statusImages = null!;

		// loader overlay
		private Panel overlayPanel = null!;
		private Label overlayLabel = null!;
		private ProgressBar overlayProgress = null!;

		// Layout constants (tight spacing)
		private const int GapYSmall = 0;
		private const int GapY = 1;
		private const int GapX = 6;

		// Cached logo
		private Image? _logoImage;

		// icon cache
		private readonly Dictionary<string, Image> _iconCache = new(StringComparer.OrdinalIgnoreCase);

		public MainForm()
		{
			Text = "";
			StartPosition = FormStartPosition.CenterScreen;
			Width = 900;
			Height = 600;
			AutoScaleMode = AutoScaleMode.Dpi;

			ShowIcon = false;
			Icon = null;

			// Build UI
			var header = CreateHeaderPanel();
			var inputs = CreateInputsPanel();
			lvScripts = CreateListView();
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

			Controls.Add(lvScripts); // Fill
			Controls.Add(inputs);    // Top (below header)
			Controls.Add(header);    // Top (with logo)
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

			// Row 1: Connection string
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

			// Row 2: Scripts folder + Browse
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

			// Row 3: Actions
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

			// Compose rows
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

		private ListView CreateListView()
		{
			var list = new ListView
			{
				Dock = DockStyle.Fill,
				View = View.Details,
				FullRowSelect = true,
				GridLines = true,
				Font = new Font("Segoe UI", 10f, FontStyle.Regular),
				OwnerDraw = true
			};

			list.Columns.Add("", 28, HorizontalAlignment.Center);
			list.Columns.Add("Script Name", 520, HorizontalAlignment.Left);
			list.Columns.Add("Applied", 180, HorizontalAlignment.Left);
			list.Columns.Add("Status", 120, HorizontalAlignment.Left);

			list.SmallImageList = new ImageList
			{
				ImageSize = new Size(20, 20),
				ColorDepth = ColorDepth.Depth32Bit
			};

			statusImages = list.SmallImageList;
			statusImages.Images.Add("executed", GetIcon("executed", 20));
			statusImages.Images.Add("pending", GetIcon("pending", 20));
			statusImages.Images.Add("error", GetIcon("error", 20));

			list.DrawColumnHeader += (s, e) => e.DrawDefault = true;
			list.DrawSubItem += (s, e) =>
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
				else
				{
					e.DrawText(TextFormatFlags.Left);
				}
			};

			// loader overlay over the list
			overlayPanel = new Panel
			{
				Dock = DockStyle.Fill,
				Visible = false,
				BackColor = Color.FromArgb(160, SystemColors.ControlLightLight)
			};

			var overlayContainer = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				ColumnCount = 1,
				RowCount = 2
			};
			overlayContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
			overlayContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

			var inner = new FlowLayoutPanel
			{
				FlowDirection = FlowDirection.TopDown,
				AutoSize = true,
				AutoSizeMode = AutoSizeMode.GrowAndShrink,
				Anchor = AnchorStyles.None,
				Padding = new Padding(12)
			};

			overlayLabel = new Label
			{
				Text = "Working...",
				AutoSize = true,
				Font = new Font("Segoe UI", 10, FontStyle.Regular),
				TextAlign = ContentAlignment.MiddleCenter
			};

			overlayProgress = new ProgressBar
			{
				Style = ProgressBarStyle.Marquee,
				MarqueeAnimationSpeed = 35,
				Width = 240
			};

			inner.Controls.Add(overlayLabel);
			inner.Controls.Add(overlayProgress);

			overlayContainer.Controls.Add(new Panel()); // spacer
			overlayContainer.Controls.Add(inner);

			overlayPanel.Controls.Add(overlayContainer);
			list.Controls.Add(overlayPanel); // sits above items

			return list;
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
					MessageBox.Show($"Loaded.\nExecuted: {executedRows.Count}\nPending: {pendingFiles.Count}", "Scripts loaded");
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

		// loader helpers
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
	}
}