using DbUp.Engine;
using DbUp.Engine.Transactions;

namespace Scripter.Services
{
	public enum ScriptSortBy { Name, CreatedUtc, ModifiedUtc }

	public static class ScriptKey
	{
		public const string Sep = "||";
		public static string Make(string folderFull, string fileName) => $"{folderFull}{Sep}{fileName}";
		public static (string folder, string file) Split(string key)
		{
			var idx = key.LastIndexOf(Sep, StringComparison.Ordinal);
			if (idx < 0) return ("", key);
			return (key.Substring(0, idx), key.Substring(idx + Sep.Length));
		}
	}

	public class FullPathFileSystemScriptProvider : IScriptProvider
	{
		private readonly string folder;
		private readonly string pattern;
		private readonly SearchOption search;
		private readonly ScriptSortBy sortBy;

		public FullPathFileSystemScriptProvider(
			string folder,
			string pattern = "*.sql",
			bool includeSubdirs = true,
			ScriptSortBy sortBy = ScriptSortBy.CreatedUtc)
		{
			this.folder = folder;
			this.pattern = pattern;
			this.search = includeSubdirs ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
			this.sortBy = sortBy;
		}

		public IEnumerable<SqlScript> GetScripts(IConnectionManager connectionManager)
		{
			var files = Directory.EnumerateFiles(folder, pattern, search)
								 .Select(f => new FileInfo(f));

			files = sortBy switch
			{
				ScriptSortBy.CreatedUtc => files.OrderBy(fi => SafeCreationUtc(fi)).ThenBy(fi => fi.FullName, StringComparer.OrdinalIgnoreCase),
				ScriptSortBy.ModifiedUtc => files.OrderBy(fi => fi.LastWriteTimeUtc).ThenBy(fi => fi.FullName, StringComparer.OrdinalIgnoreCase),
				_ => files.OrderBy(fi => fi.FullName, StringComparer.OrdinalIgnoreCase),
			};

			foreach (var fi in files)
			{
				var contents = File.ReadAllText(fi.FullName);
				var folderFull = fi.DirectoryName!;
				var fileName = fi.Name;
				var key = ScriptKey.Make(folderFull, fileName);
				yield return new SqlScript(key, contents);
			}
		}

		private static DateTime SafeCreationUtc(FileInfo fi) =>
			fi.CreationTimeUtc > DateTime.MinValue.AddDays(1) ? fi.CreationTimeUtc : fi.LastWriteTimeUtc;
	}
}