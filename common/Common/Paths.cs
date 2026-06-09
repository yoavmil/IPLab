using System;
using System.IO;
using System.Reflection;

namespace Common
{
	public static class Paths
	{
		public static string UserFolder { get => _userFolder; set => _userFolder = value; } // allow overring for testing
		private static string _userFolder = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			CompanyName,
			AppName
		);

		public static string CompanyName => "DRU";
		public static string AppName => "IPLab";

		public static string LogFolder { get { return Path.Combine(UserFolder, "Log"); } }

		public static string ConfigFolder { get { return Path.Combine(UserFolder, "Config"); } }

		public static string TempFolder { get => Path.Combine(Path.GetTempPath(), CompanyName, AppName); }
		
		public static string UniqeTempFolder { get => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); } // for testing
	
		public static string CWD { get => Environment.CurrentDirectory; }
	}
}
