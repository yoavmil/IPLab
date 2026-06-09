using Common;
using Logger;
using System.IO;
using System.Windows;

namespace IPLab.UI
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			Log.Folder = Paths.LogFolder;
			base.OnStartup(e);
		}
	}

}
