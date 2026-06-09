using Common;
using Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime;

namespace Settings
{
	public class SettingsStore
	{
		private static readonly object _lock = new object();
		private static readonly Dictionary<Type, ISettings> _cache = new Dictionary<Type, ISettings>();

		public static T Load<T>() where T : class, ISettings, new()
		{
			var type = typeof(T);
			lock (_lock)
			{
				if (_cache.ContainsKey(type))
					return _cache[type] as T;

				var filePath = Path.Combine(Paths.ConfigFolder, $"{typeof(T).Name}.json");

				if (!File.Exists(filePath))
				{
					T settings = new T();
					_cache[type] = settings;
					return settings;
				}

				Log.Info<SettingsStore>($"Loading {filePath}");

				string json;
				try
				{
					json = File.ReadAllText(filePath);
					var settings = JsonConvert.DeserializeObject<T>(json);
					Log.Info<SettingsStore>($"Loaded {filePath}");
					_cache[type] = settings;
					
					Loaded?.Invoke(settings);
					return settings;
				}
				catch (Exception ex)
				{
					Log.Error<SettingsStore>($"Failed to load {filePath}: ${ex.Message}");
					throw;
				}
			}
		}
		public static T Load<T>(string jsonPath) where T : class, ISettings, new()
		{
			var type = typeof(T);
			lock (_lock)
			{
				var filePath = jsonPath;

				Log.Info<SettingsStore>($"Loading {filePath}");

				try
				{
					string json = File.ReadAllText(filePath);
					var settings = JsonConvert.DeserializeObject<T>(json);
					Log.Info<SettingsStore>($"Loaded {filePath}");
					_cache[type] = settings;
					Loaded?.Invoke(settings);
					return settings;
				}
				catch (Exception ex)
				{
					Log.Error<SettingsStore>($"Failed to load {filePath}: ${ex.Message}");
					throw;
				}
			}
		}

		public static T Reload<T>() where T : class, ISettings, new()
		{
			var type = typeof(T);
			lock (_lock)
			{
				if (_cache.ContainsKey(type))
					_cache.Remove(type);
			}
			return Load<T>();
		}

		public static void Save(ISettings settings)
		{
			var filePath = Path.Combine(Paths.ConfigFolder, $"{settings.GetType().Name}.json");

			try
			{
				if (!Directory.Exists(Paths.ConfigFolder))
				{
					Directory.CreateDirectory(Paths.ConfigFolder);
					Log.Info<SettingsStore>($"Created directory {Paths.ConfigFolder}");
				}


				string json = JsonConvert.SerializeObject(settings,
					new JsonSerializerSettings
					{ // write enums as strings and not as index
						Converters = new List<JsonConverter> { new StringEnumConverter() },
						Formatting = Formatting.Indented
					}
				);
				File.WriteAllText(filePath, json);
				//Log.Info<SettingsStore>($"Saved {filePath}");
			}
			catch (Exception ex)
			{
				Log.Error<SettingsStore>($"Failed to save {filePath}: {ex.Message}");
				throw;
			}
		}

		public static bool HasFile<T>()
		{
			var filePath = Path.Combine(Paths.ConfigFolder, $"{typeof(T).Name}.json");
			return File.Exists(filePath);
		}

		public static event Action<object> Loaded;
	}
}
