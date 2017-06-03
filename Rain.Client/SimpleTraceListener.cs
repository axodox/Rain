using System;
using System.Diagnostics;

namespace Rain.Client
{
  public class LoggingTraceListener : TraceListener
  {
    public static void Initialize()
    {
      Debug.Listeners.Add(new LoggingTraceListener("DebugListener", LogEntrySeverity.Debug));
      Debug.AutoFlush = true;
    }

    private readonly string _name;
    private readonly LogEntrySeverity _severity;

    private string _lineBuffer = string.Empty;

    public static event Action<LogEntry> LogEntryAdded;

    public LoggingTraceListener(string name, LogEntrySeverity severity)
    {
      _name = name;
      _severity = severity;
    }

    public override void Write(string message)
    {
      _lineBuffer += message;
    }

    public override void WriteLine(string message)
    {
      var logEntry = new LogEntry()
      {
        Source = _name,
        Severity = _severity,
        Text = _lineBuffer + message,
        TimeStamp = DateTime.Now
      };

      _lineBuffer = null;

      LogEntryAdded?.Invoke(logEntry);
    }
  }
}
