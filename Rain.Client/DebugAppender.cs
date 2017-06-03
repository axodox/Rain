using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository.Hierarchy;
using System;

namespace Rain.Client
{
  public class DebugAppender : AppenderSkeleton
  {
    public static event Action<LogEntry> LogEntryAdded;

    private const LogEntrySeverity _consoleLoggingLevel = LogEntrySeverity.All;

    public static void Initialize()
    {
      foreach (Hierarchy hierarchy in LogManager.GetAllRepositories())
      {
        hierarchy.Root.AddAppender(new DebugAppender());
        hierarchy.Configured = true;
      }
    }

    protected override void Append(LoggingEvent loggingEvent)
    {
      LogEntrySeverity severity;
      var color = ConsoleColor.Gray;
      switch (loggingEvent.Level.Name)
      {
        case "ALL":
          severity = LogEntrySeverity.All;
          color = ConsoleColor.Green;
          break;
        case "DEBUG":
          severity = LogEntrySeverity.Debug;
          color = ConsoleColor.Cyan;
          break;
        case "INFO":
          severity = LogEntrySeverity.Info;
          color = ConsoleColor.DarkGray;
          break;
        case "WARN":
          severity = LogEntrySeverity.Warn;
          color = ConsoleColor.Yellow;
          break;
        case "ERROR":
          severity = LogEntrySeverity.Error;
          color = ConsoleColor.Red;
          break;
        case "FATAL":
          severity = LogEntrySeverity.Fatal;
          color = ConsoleColor.DarkRed;
          break;
        case "OFF":
          severity = LogEntrySeverity.Off;
          break;
        default:
          severity = LogEntrySeverity.Unknown;
          break;
      }

      if (severity >= _consoleLoggingLevel)
      {
        var currentColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(loggingEvent.LoggerName);
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine(": " + loggingEvent.RenderedMessage);
        Console.ForegroundColor = currentColor;
      }

      if (LogEntryAdded != null)
      {
        var logEntry = new LogEntry()
        {
          TimeStamp = loggingEvent.TimeStamp,
          Source = loggingEvent.LoggerName,
          Text = loggingEvent.RenderedMessage,
          Severity = severity
        };

        LogEntryAdded(logEntry);
      }
    }
  }
}
