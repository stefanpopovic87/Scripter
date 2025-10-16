namespace Scripter
{
	public interface IIconProvider
	{
		Image Get(string name, int size);
		Image? GetLogo();
	}
}