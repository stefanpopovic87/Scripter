using Microsoft.Data.SqlClient;
using Scripter.Services;
using System.Diagnostics;

namespace Scripter
{
	public sealed class ScriptRepository : IScriptRepository
	{
		private readonly Func<string> _getConnectionString;
		private readonly Func<string> _getBaseFolder;

		public ScriptRepository(Func<string> getConnectionString, Func<string> getBaseFolder)
		{
			_getConnectionString = getConnectionString;
			_getBaseFolder = getBaseFolder;
		}

		public async Task<List<ExecutedRow>> GetExecutedRowsAsync(string baseFolder)
		{
			var list = new List<ExecutedRow>();
			string connStr = _getConnectionString();
			string basePrefix = baseFolder.EndsWith(Path.DirectorySeparatorChar.ToString()) ? baseFolder : baseFolder + Path.DirectorySeparatorChar;

			await using var conn = new SqlConnection(connStr);
			await conn.OpenAsync();
			await using var cmd = conn.CreateCommand();
			cmd.CommandText = "SELECT [ScriptName],[Applied],[Path] FROM [scripts].[DbMigrationHistory]";
			await using var r = await cmd.ExecuteReaderAsync();
			while (await r.ReadAsync())
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

		public async Task<IReadOnlyList<HistoryRow>> GetHistoryAsync()
		{
			var list = new List<HistoryRow>();
			string connStr = _getConnectionString();
			await using var conn = new SqlConnection(connStr);
			await conn.OpenAsync();
			await using var cmd = conn.CreateCommand();
			cmd.CommandText = @"
				SELECT [Id],[ScriptName],[Applied],[ExecutedBy],[MachineName],[Path]
				FROM [scripts].[DbMigrationHistory]";
			await using var r = await cmd.ExecuteReaderAsync();
			while (await r.ReadAsync())
			{
				var id = r.IsDBNull(0) ? "" : (r.GetValue(0)?.ToString() ?? "");
				var script = r.IsDBNull(1) ? "" : r.GetString(1);
				var applied = r.IsDBNull(2) ? DateTime.MinValue : r.GetDateTime(2).ToUniversalTime();
				var by = r.IsDBNull(3) ? null : r.GetString(3);
				var machine = r.IsDBNull(4) ? null : r.GetString(4);
				var path = r.IsDBNull(5) ? null : r.GetString(5);
				list.Add(new HistoryRow { Id = id, Script = script, AppliedUtc = applied, By = by, Machine = machine, Path = path });
			}
			return list
				.OrderByDescending(x => x.AppliedUtc)
				.ThenBy(x => x.Script, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		public string GetScriptContentById(int id)
		{
			if (id <= 0) return string.Empty;
			try
			{
				using var conn = new SqlConnection(_getConnectionString());
				conn.Open();
				using var cmd = conn.CreateCommand();
				cmd.CommandText = "SELECT [Content] FROM [scripts].[DbMigrationHistory] WHERE [Id]=@id";
				cmd.Parameters.Add(new SqlParameter("@id", id));
				var o = cmd.ExecuteScalar();
				return o as string ?? string.Empty;
			}
			catch { return string.Empty; }
		}

		public string GetScriptContentByNameOrFile(string fileName)
		{
			try
			{
				using var conn = new SqlConnection(_getConnectionString());
				conn.Open();
				using var cmd = conn.CreateCommand();
				cmd.CommandText = @"
					SELECT TOP(1) [Content]
					FROM [scripts].[DbMigrationHistory]
					WHERE [ScriptName] = @name AND (@path = '' OR [Path] LIKE @path + '%')
					ORDER BY [Applied] DESC";
				cmd.Parameters.Add(new SqlParameter("@name", fileName));
				string baseFolder = "";
				try { baseFolder = Path.GetFullPath(_getBaseFolder()); } catch { }
				cmd.Parameters.Add(new SqlParameter("@path", baseFolder ?? string.Empty));
				var o = cmd.ExecuteScalar();
				var content = o as string;
				if (!string.IsNullOrEmpty(content)) return content!;
			}
			catch { }

			try
			{
				var root = _getBaseFolder();
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

		public DateTime GetScriptFileTimeUtc(string scriptKey)
		{
			var (folder, file) = ScriptKey.Split(scriptKey);
			var fullPath = Path.Combine(folder ?? "", file ?? "");
			DateTime t = File.GetCreationTimeUtc(fullPath);
			if (t <= DateTime.MinValue.AddDays(1))
				t = File.GetLastWriteTimeUtc(fullPath);
			return t;
		}

		public async Task<DbTestResult> TestConnectionAsync()
		{
			string cs = _getConnectionString();
			if (string.IsNullOrWhiteSpace(cs))
				return new DbTestResult(false, null, null, null, 0, new ArgumentException("Empty connection string."));

			try
			{
				var sw = Stopwatch.StartNew();
				await using var conn = new SqlConnection(cs);
				await conn.OpenAsync();
				sw.Stop();
				return new DbTestResult(
					Success: true,
					Server: conn.DataSource,
					Database: conn.Database,
					Version: conn.ServerVersion,
					ElapsedMs: sw.ElapsedMilliseconds,
					Error: null);
			}
			catch (Exception ex)
			{
				return new DbTestResult(false, null, null, null, 0, ex);
			}
		}
	}
}