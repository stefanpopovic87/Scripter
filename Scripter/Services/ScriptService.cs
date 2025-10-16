using DbUp;
using DbUp.Engine.Output;
using Scripter.Services;

namespace Scripter
{
	public sealed class ScriptService : IScriptService
	{
		private readonly IScriptRepository _repository;

		public ScriptService(IScriptRepository repository) => _repository = repository;

		public async Task<(List<ExecutedRow> Executed, List<string> PendingFiles)> LoadScriptsAsync(string connectionString, string baseFolder)
		{
			var executedRows = await _repository.GetExecutedRowsAsync(baseFolder);
			var upgrader =
				DeployChanges.To
					.SqlDatabase(connectionString)
					.WithScripts(new FullPathFileSystemScriptProvider(
						baseFolder,
						pattern: "*.sql",
						includeSubdirs: true,
						sortBy: ScriptSortBy.CreatedUtc))
					.JournalTo(new CustomJournal("scripts", "DbMigrationHistory", baseFolder, connectionString))
					.LogTo(new NullLogger())
					.Build();

			_ = upgrader.GetExecutedScripts();

			var executedKeys = executedRows
				.Select(r => ScriptKey.Make(r.Path, r.ScriptName))
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			var pendingScripts = upgrader.GetScriptsToExecute().ToList();

			var pendingFiles = pendingScripts
				.OrderBy(s => _repository.GetScriptFileTimeUtc(s.Name))
				.ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
				.Where(s => !executedKeys.Contains(s.Name))
				.Select(s => ScriptKey.Split(s.Name).file)
				.ToList();

			return (executedRows, pendingFiles);
		}

		public async Task<(bool Success, string? ErrorFile, Exception? Error)> ExecutePendingAsync(
			string connectionString,
			string baseFolder,
			IUpgradeLog log,
			IReadOnlyCollection<string> selectedFiles)
		{
			// Build provider of all pending scripts.
			var probe =
				DeployChanges.To
					.SqlDatabase(connectionString)
					.WithScripts(new FullPathFileSystemScriptProvider(
						baseFolder,
						"*.sql",
						includeSubdirs: true,
						sortBy: ScriptSortBy.CreatedUtc))
					.JournalTo(new CustomJournal("scripts", "DbMigrationHistory", baseFolder, connectionString))
					.LogTo(log)
					.Build();

			var pendingAll = probe.GetScriptsToExecute().ToList();

			// Order consistently (same as load).
			var pendingSorted = pendingAll
				.OrderBy(s => _repository.GetScriptFileTimeUtc(s.Name))
				.ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (pendingSorted.Count == 0)
				return (true, null, null);

			// Filter to selected file names only.
			var selectedSet = new HashSet<string>(selectedFiles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
			var toRun = pendingSorted
				.Where(s => selectedSet.Contains(ScriptKey.Split(s.Name).file))
				.ToList();

			// If user selected files but none match pending (possible race), treat as success.
			if (toRun.Count == 0)
				return (true, null, null);

			foreach (var script in toRun)
			{
				var runner =
					DeployChanges.To
						.SqlDatabase(connectionString)
						.WithScripts(new[] { script })
						.JournalTo(new CustomJournal("scripts", "DbMigrationHistory", baseFolder, connectionString))
						.LogTo(log)
						.Build();

				var res = runner.PerformUpgrade();
				if (!res.Successful)
				{
					var key = res.ErrorScript?.Name;
					var (_, file) = ScriptKey.Split(key ?? "");
					return (false, file, res.Error);
				}
			}
			return (true, null, null);
		}

		private sealed class NullLogger : IUpgradeLog
		{
			public void WriteError(string format, params object[] args) { }
			public void WriteInformation(string format, params object[] args) { }
			public void WriteWarning(string format, params object[] args) { }
		}
	}
}