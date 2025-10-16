namespace Scripter
{
	public sealed class PreviewService : IPreviewService
	{
		private readonly IScriptRepository _repository;
		public PreviewService(IScriptRepository repository) => _repository = repository;

		public string GetPreviewById(int id, int maxLen = 1000) =>
			Normalize(_repository.GetScriptContentById(id), maxLen);

		public string GetPreviewByFileName(string fileName, int maxLen = 1000) =>
			Normalize(_repository.GetScriptContentByNameOrFile(fileName), maxLen);

		private static string Normalize(string? sql, int maxLen)
		{
			if (string.IsNullOrEmpty(sql)) return string.Empty;
			string s = sql!;
			if (s.Length > maxLen) s = s.Substring(0, maxLen) + " …";
			return s.Trim();
		}
	}
}