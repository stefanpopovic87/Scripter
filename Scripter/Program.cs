using Scripter.UI.Forms;

namespace Scripter
{
	internal static class Program
	{
		[STAThread]
		static void Main()
		{
			ApplicationConfiguration.Initialize();
			Application.Run(new MainForm());
		}
	}
}
