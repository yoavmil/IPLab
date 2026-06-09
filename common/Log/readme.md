# Logger

## Overview

This projects contains a logger with these features:

- **Thread Safety**: Supports logging from multiple threads simultaneously (`Async`).
- **Log Format**: Each log entry includes a timestamp, severity level, caller class name, and log message.
- **Daily log**: The log file name includes the date.
- **CSV Compatible**: fields are seperated with ';', User resposible for writing 1 liners only.
- **Log Level**: Option to set the log verbosity.
- **Class Log Instance**: Option to hold a local instance and set its log level.
- **Console**: Option to write also to the console (`WriteToConsole`).
Example log line:

`2024/06/04 17:44:39.846;	[Warning];	CallerClass;	The log message`


## Usage

See `LogUsageExample.cs`:
```C#
Log.Folder = "path/to/folder";
Log.Level = LogLevel.Error; // only errors
Log.Debug<LogTester>("a"); // won't write
var log = Log.GetInstance<LogTester>();
log.LevelOverride = Log.Level.Debug;
log.Warning("a"); // will write
```
