using Microsoft.VisualStudio.TestTools.UnitTesting;
using Settings;
using System.IO;

namespace SettingsTester
{
	[TestClass]
	public class SettingsTester
	{
		class DemoSettings : ISettings
		{
			public int IntegerProperty { get; set; }
			public string StringProperty { get; set; }
			public double[] DoubleArrayProperty { get; set; }
		}

		private string TestFilePath = "";

		[TestInitialize]
		public void SetUp()
		{
			Common.Paths.UserFolder = Common.Paths.UniqeTempFolder;
			TestFilePath = Path.Combine(Common.Paths.ConfigFolder, "DemoSettings.json");
		}

		[TestMethod]
		public void SaveAndLoadSettings()
		{
			// Create a new settings instance and populate it
			var settings = new DemoSettings
			{
				IntegerProperty = 42,
				StringProperty = "Test String",
				DoubleArrayProperty = new double[] { 1.1, 2.2, 3.3 }
			};

			// Save the settings to a file
			SettingsStore.Save(settings);

			// Load the settings from the file
			var loadedSettings = SettingsStore.Load<DemoSettings>() as DemoSettings;

			// Verify that the loaded settings match the original settings
			Assert.AreEqual(settings.IntegerProperty, loadedSettings.IntegerProperty);
			Assert.AreEqual(settings.StringProperty, loadedSettings.StringProperty);
			CollectionAssert.AreEqual(settings.DoubleArrayProperty, loadedSettings.DoubleArrayProperty);
		}
	}
}
