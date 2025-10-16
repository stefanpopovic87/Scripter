namespace Scripter
{
	public interface IPreviewService
	{
		string GetPreviewById(int id, int maxLen = 1000);
		string GetPreviewByFileName(string fileName, int maxLen = 1000);
	}
}