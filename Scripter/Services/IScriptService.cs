namespace Scripter
{
	public interface IScriptService
	{
		Task<(List<ExecutedRow> Executed, List<string> PendingFiles)> LoadScriptsAsync(string connectionString, string baseFolder);
		Task<(bool Success, string? ErrorFile, Exception? Error)> ExecutePendingAsync(string connectionString, string baseFolder, DbUp.Engine.Output.IUpgradeLog log);
	}
}