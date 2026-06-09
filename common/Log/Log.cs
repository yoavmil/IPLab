using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace Logger
{
	public class Log
	{
		static Log()
		{
			consumerTask = Task.Run(ConsumeLogEntries);
		}
		private static Task consumerTask = null;
		public enum LogLevel
		{
			Debug,
			Info,
			Warning,
			Error
		}

		#region non static option
        public static Log GetInstance<T>(LogLevel levelOverride = LogLevel.Info) { return new Log() { _className = typeof(T).Name, LevelOverride = levelOverride }; }
		private string _className = "";
		public LogLevel? LevelOverride = null;
		public string Debug(string message)
		{
			FireLogEvent(LogLevel.Debug, _className, DateTime.Now, message);
			if (CheckLevel(LogLevel.Debug)) log(LogLevel.Debug, _className, message); return message;
		}
		public string Info(string message)
		{
			FireLogEvent(LogLevel.Info, _className, DateTime.Now, message);
			if (CheckLevel(LogLevel.Info)) log(LogLevel.Info, _className, message); return message;
		}
		public string Warn(string message)
		{
			FireLogEvent(LogLevel.Warning, _className, DateTime.Now, message);
			if (CheckLevel(LogLevel.Warning)) log(LogLevel.Warning, _className, message); return message;
		}
		public string Error(string message)
		{
			FireLogEvent(LogLevel.Error, _className, DateTime.Now, message);
			if (CheckLevel(LogLevel.Error)) log(LogLevel.Error, _className, message); return message;
		}
		// For testing only
		public static async Task Flush()
		{
			_bc.CompleteAdding();
			await consumerTask;
			_bc = new BlockingCollection<LogEntry>(new ConcurrentBag<LogEntry>());
			consumerTask = Task.Run(ConsumeLogEntries);
		}
		#endregion

		static public string Debug<T>(string message)
		{
			FireLogEvent(LogLevel.Debug, typeof(T).Name, DateTime.Now, message);
			if (CheckStaticLevel(LogLevel.Debug)) log(LogLevel.Debug, typeof(T).Name, message); return message;
		}
		static public string Info<T>(string message)
		{
			FireLogEvent(LogLevel.Info, typeof(T).Name, DateTime.Now, message);
			if (CheckStaticLevel(LogLevel.Info)) log(LogLevel.Info, typeof(T).Name, message); return message;
		}
		static public string Warn<T>(string message)
		{
			FireLogEvent(LogLevel.Warning, typeof(T).Name, DateTime.Now, message);
			if (CheckStaticLevel(LogLevel.Warning)) log(LogLevel.Warning, typeof(T).Name, message); return message;
		}
		static public string Error<T>(string message)
		{
			FireLogEvent(LogLevel.Error, typeof(T).Name, DateTime.Now, message);
			if (CheckStaticLevel(LogLevel.Error)) log(LogLevel.Error, typeof(T).Name, message); return message;
		}
		public static event EventHandler<ILogEvent> OnLogEvent;
		static void FireLogEvent(LogLevel level, string className, DateTime timestamp, string message)
		{
			OnLogEvent?.Invoke(null, new LogEvent(level, className, timestamp, message));
		}
		static private bool CheckStaticLevel(LogLevel level) { return level >= Level; }
		private static object _lockObj = new object();
		static private void log(LogLevel level, string callerName, string message)
		{
			string msg = AddCallerName ?
				$"[{level}];\t{callerName};\t{message}" :
				$"[{level}];\t{message}";
			var entry = new LogEntry { time = DateTime.Now, msg = msg };
			if (Async)
				_bc.Add(entry);
			else
				lock (_lockObj)
				{
					Process(entry);
				}
		}

		#region static parameters 
		static public LogLevel Level = LogLevel.Info;
		static public string FileName { get => $"log_{DateTime.Now:yyyyMMdd}.txt"; }
		static public string Folder { get; set; }
		static public bool AddCallerName { get; set; } = true;
		static public bool WriteToConsole { get; set; } = false;
		static public string FilePath { get { return Path.Combine(Folder, FileName); } }
		/// <summary>
		/// Option to write to the disk in an asynchronous way (default)
		/// The option to write to the disk in a synchronous way is mainly for 
		/// debugging and testing, because if the process crashes before the log lines 
		/// finished writing, the last lines would be missing
		/// </summary>
		public static bool Async = true;
		#endregion

		private bool CheckLevel(LogLevel level) { return LevelOverride != null ? level >= LevelOverride : level >= Level; }

		class LogEntry { public DateTime time; public string msg; }
		static private BlockingCollection<LogEntry> _bc = new BlockingCollection<LogEntry>(new ConcurrentBag<LogEntry>());
		static void ConsumeLogEntries()
		{
			foreach (var entry in _bc.GetConsumingEnumerable()) Process(entry);
		}

		private static void Process(LogEntry data)
		{
			if (WriteToConsole) Console.WriteLine(data.msg);

			if (!Directory.Exists(Folder))
				Directory.CreateDirectory(Folder);

			File.AppendAllText(FilePath, $"{data.time:yyyy/MM/dd HH:mm:ss.fff};\t{data.msg}\n");
		}
	}
	public interface ILogEvent
	{
		Log.LogLevel Level { get; }
		string ClassName { get; }
		DateTime TimeStamp { get; }
		string Message { get; }
	}
	class LogEvent : ILogEvent
	{
		public Log.LogLevel Level { get; private set; }
		public string ClassName { get; private set; }
		public DateTime TimeStamp { get; private set; }
		public string Message { get; private set; }

		public LogEvent(Log.LogLevel level, string className, DateTime timeStamp, string message)
		{
			Level = level;
			ClassName = className;
			TimeStamp = timeStamp;
			Message = message;
		}
	}
}


