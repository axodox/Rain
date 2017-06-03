using Rain.Server;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Rain
{
  class Program
  {
    static readonly Dictionary<Client.LogEntrySeverity, ConsoleColor> mColorMapping = new Dictionary<Client.LogEntrySeverity, ConsoleColor>()
    {
      { Client.LogEntrySeverity.Debug, ConsoleColor.Cyan },
      { Client.LogEntrySeverity.Info, ConsoleColor.White },
      { Client.LogEntrySeverity.Error, ConsoleColor.Red },
      { Client.LogEntrySeverity.Fatal, ConsoleColor.DarkRed }
    };

    static StreamWriter _writer;
    static BlockingCollection<string> _toLog = new BlockingCollection<string>();
    static readonly object _syncRoot = new object();
    static EventType _eventFilter = EventType.All;

    [Flags]
    private enum EventType
    {
      None = 0,
      Log = 1,
      Exception = 2,
      All = Log | Exception
    };

    static void Main(string[] args)
    {
      if (args.Length == 0)
      {
        Console.WriteLine(
          "Tool for monitoring .Net processes for exceptions and log.\r\n" +
          "Usage examples:\r\n" +
          "Rain.exe MyFavoriteProcess\r\n" +
          "Rain.exe MyFavoriteProcess,MyOtherFavoriteProcess\r\n" +
          "Rain.exe MyFavoriteProcess someLogFile.log\r\n" +
          "\r\n" +
          "Further options:\r\n" +
          "-ft:exception|log\r\n" +
          "Press any key to exit...");
        Console.Read();
        return;
      }
      var processNames = args[0].Split(',');
      if (args.Length > 1 && !args[1].StartsWith("-"))
      {
        _writer = new StreamWriter(args[1]);
        new Thread(LogWorker).Start();
      }

      var arguments = args
        .Where(p => p.StartsWith("-"))
        .Select(p => p.TrimStart('-').Split(':'))
        .ToDictionary(p => p[0], p => p[1]);

      foreach (var argument in arguments)
      {
        switch (argument.Key)
        {
          case "ft":
            _eventFilter = argument.Value
              .Split('|')
              .Select(p => Enum.Parse(typeof(EventType), p, true))
              .OfType<EventType>()
              .Aggregate((a, b) => a | b);
            break;
        }
      }

      var debugMonitor = new DebugMonitor();
      debugMonitor.ExceptionReceived += OnExceptionReceived;
      debugMonitor.LogReceived += OnLogEntryReceived;

      var debugProvider = new DebugProvider();
      foreach (var wProcessName in processNames)
      {
        debugProvider.Monitor(wProcessName);
      }

      Thread.Sleep(Timeout.Infinite);
    }

    static void OnLogEntryReceived(object sender, Client.LogEntry e)
    {
      if (_eventFilter.HasFlag(EventType.Log))
      {
        lock (_syncRoot)
        {
          ConsoleColor wColor;
          if (!mColorMapping.TryGetValue(e.Severity, out wColor))
          {
            wColor = ConsoleColor.White;
          }

          Console.ForegroundColor = wColor;
          Console.Write("{0} {1}-{2} [{3}] {4}: ", e.TimeStamp, e.ProcessName, e.ProcessId, e.Source, e.Severity);

          Console.ForegroundColor = ConsoleColor.Gray;
          Console.WriteLine(e.Text);
        }

        Log("{0} {1}-{2} [{3}] {4}: {5}", e.TimeStamp, e.ProcessName, e.ProcessId, e.Source, e.Severity, e.Text);
      }
    }

    static void OnExceptionReceived(object sender, Client.ExceptionEntry e)
    {
      if (_eventFilter.HasFlag(EventType.Exception))
      {
        lock (_syncRoot)
        {
          Console.ForegroundColor = ConsoleColor.Red;
          Console.WriteLine("{0} {1}-{2} [{3}] EXCEPTION: ", e.TimeStamp, e.ProcessName, e.ProcessId, e.Source);

          Console.ForegroundColor = ConsoleColor.Gray;
          Console.WriteLine(e.Message);
          Console.WriteLine(e.Description);
        }

        Log("{0} {1}-{2} [{3}] EXCEPTION: {4}\r\n{5}", e.TimeStamp, e.ProcessName, e.ProcessId, e.Source, e.Message, e.Description);
      }
    }

    static void Log(string format, params object[] arguments)
    {
      if (_writer != null)
      {
        _toLog.TryAdd(string.Format(format, arguments));
      }
    }

    static void LogWorker()
    {
      Thread.CurrentThread.IsBackground = true;
      
      while (true)
      {
        if (_toLog.TryTake(out var item, TimeSpan.FromSeconds(1)))
        {
          _writer.WriteLine(item);
        }

        _writer.Flush();
      }
    }
  }
}
