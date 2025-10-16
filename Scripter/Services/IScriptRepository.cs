using Scripter.Services;

namespace Scripter
{
	public interface IScriptRepository
	{
		Task<List<ExecutedRow>> GetExecutedRowsAsync(string baseFolder);
		Task<IReadOnlyList<HistoryRow>> GetHistoryAsync();
		string GetScriptContentById(int id);
		string GetScriptContentByNameOrFile(string fileName);
		DateTime GetScriptFileTimeUtc(string scriptKey);
		Task<DbTestResult> TestConnectionAsync();
	}

	public sealed class ExecutedRow
	{
		public string ScriptName { get; set; } = "";
		public DateTime? Applied { get; set; }
		public string Path { get; set; } = "";
	}

	public sealed class HistoryRow
	{
		public string Id { get; set; } = "";
		public string Script { get; set; } = "";
		public DateTime AppliedUtc { get; set; }
		public string? By { get; set; }
		public string? Machine { get; set; }
		public string? Path { get; set; }
	}
}