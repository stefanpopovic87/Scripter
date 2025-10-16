namespace Scripter
{
	public interface IScriptService
	{
		Task<(List<ExecutedRow> Executed, List<string> PendingFiles)> LoadScriptsAsync(string connectionString, string baseFolder);

		// Updated: execute only selected pending scripts (file names from PendingFiles list)
		Task<(bool Success, string? ErrorFile, Exception? Error)> ExecutePendingAsync(
			string connectionString,
			string baseFolder,
			DbUp.Engine.Output.IUpgradeLog log,
			IReadOnlyCollection<string> selectedFiles);
	}
}