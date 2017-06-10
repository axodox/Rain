using Rain.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.Text.RegularExpressions;
using System.Threading;

namespace Rain.Client
{
  [ServiceBehavior(
    InstanceContextMode = InstanceContextMode.PerSession,
    ConcurrencyMode = ConcurrencyMode.Multiple,
    AddressFilterMode = AddressFilterMode.Any)]
  [AspNetCompatibilityRequirements(
    RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
  public class DebugService : IDebugService
  {
    private static readonly DateTime _startUpTime = DateTime.Now;

    private IDebugMonitor _monitor;

    private BlockingCollection<Entry> _entriesToSend;

    private Process _process = Process.GetCurrentProcess();
    private static readonly int _processId = Process.GetCurrentProcess().Id;
    private static readonly string _processName = Process.GetCurrentProcess().ProcessName;
    private IDictionary<string, string> _settings;

    private static List<IRainPlugIn> PlugIns = new List<IRainPlugIn>();

    public DebugService()
    {
      _settings = ReadSettings();
      LoadPlugIns();

      _entriesToSend = new BlockingCollection<Entry>();
      var _sendThread = new Thread(SendWorker);
      _sendThread.IsBackground = true;
      _sendThread.Start();
    }

    private void LoadPlugIns()
    {
      var clientRoot = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      foreach (var setting in _settings.Where(p=>p.Key.StartsWith("Modules.")))
      {
        try
        {
          var libraryPath = setting.Value;
          if (!Path.IsPathRooted(libraryPath))
          {
            libraryPath = Path.Combine(clientRoot, libraryPath);
          }

          if (File.Exists(libraryPath))
          {
            var assemblyName = AssemblyName.GetAssemblyName(libraryPath);
            var assembly = Assembly.Load(assemblyName);
            var exportedPlugIns = assembly
              .GetExportedTypes()
              .Where(p => p.GetInterface(typeof(IRainPlugIn).FullName) != null)
              .ToArray();

            foreach(var exportedPlugIn in exportedPlugIns)
            {
              var plugIn = Activator.CreateInstance(exportedPlugIn) as IRainPlugIn;
              plugIn.OnAttach();
              PlugIns.Add(plugIn);
            }
          }
        }
        catch(Exception e)
        {
          Console.WriteLine($"Could not load module library {setting.Value}!");
        }
      }
    }

    private static IDictionary<string, string> ReadSettings()
    {
      var settings = new Dictionary<string, string>();

      var clientRoot = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      var configPath = Path.Combine(clientRoot, "Rain.ini");
      if (File.Exists(configPath))
      {
        var chapterRegex = new Regex(@"^\[(\w+)\]$");
        var keyValueRegex = new Regex(@"^(\w+)=(.*)$");

        var chapter = string.Empty;
        var lines = File.ReadAllLines(configPath);
        foreach (var line in lines)
        {
          var chapterMatch = chapterRegex.Match(line);
          if (chapterMatch.Success)
          {
            chapter = chapterMatch.Groups[1].Value;
          }

          var keyValueMatch = keyValueRegex.Match(line);
          if (keyValueMatch.Success)
          {
            settings[chapter + "." + keyValueMatch.Groups[1].Value] = keyValueMatch.Groups[2].Value;
          }
        }
      }

      return settings;
    }

    private void SendWorker()
    {
      while (true)
      {
        try
        {
          var item = _entriesToSend.Take();
          item.ProcessId = _processId;
          item.ProcessName = _processName;

          switch (item)
          {
            case LogEntry logEntry:
              _monitor.AddLogEntry(logEntry);
              break;
            case ExceptionEntry exceptionEntry:
              _monitor.AddExceptionEntry(exceptionEntry);
              break;
          }
        }
        catch
        {
          break;
        }
      }
    }

    private ExceptionEntry GetExceptionEntry(Exception exception)
    {
      if (exception == null) return null;

      return new ExceptionEntry()
      {
        Message = exception.Message,
        TimeStamp = (DateTime)exception.Data["Time"],
        Description = exception.GetDescription(),
        Source = exception.TargetSite != null ? string.Format("{0} in {1}", exception.TargetSite, exception.TargetSite.DeclaringType.FullName) : "Unknown"
      };
    }

    public ClientInfo Initialize()
    {
      _monitor = OperationContext.Current.GetCallbackChannel<IDebugMonitor>();
      var communicationObject = (_monitor as ICommunicationObject);
      communicationObject.Closing += OnShutdown;
      communicationObject.Faulted += OnShutdown;
      if (Loader.IsLogging)
      {
        EnableLogging();
      }
      LoggingTraceListener.LogEntryAdded += OnLogEntryAdded;
      AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
      AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

      using (var process = Process.GetCurrentProcess())
      {
        return new ClientInfo()
        {
          ProcessId = process.Id,
          ProcessName = process.ProcessName,
          ProcessLocation = process.StartInfo.FileName,
          DomainId = AppDomain.CurrentDomain.Id,
          DomainName = AppDomain.CurrentDomain.FriendlyName,
          MachineName = Environment.MachineName,
          IsX64Process = Environment.Is64BitProcess
        };
      }
    }

    private void EnableLogging()
    {
      DebugAppender.LogEntryAdded += OnLogEntryAdded;
    }

    public void Close()
    {
      //Do nothing
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
      var exception = e.ExceptionObject as Exception;
      exception.Data["Time"] = DateTime.Now;
      _entriesToSend.Add(GetExceptionEntry(exception));
    }

    private void OnFirstChanceException(object sender, FirstChanceExceptionEventArgs e)
    {
      var exception = e.Exception;
      exception.Data["Time"] = DateTime.Now;
      _entriesToSend.Add(GetExceptionEntry(exception));
    }

    private void OnLogEntryAdded(LogEntry entry)
    {
      _entriesToSend.Add(entry);
    }

    private void OnShutdown(object sender, EventArgs e)
    {
      if (Loader.IsLogging)
      {
        DisableLogging();
      }
      LoggingTraceListener.LogEntryAdded -= OnLogEntryAdded;
      AppDomain.CurrentDomain.FirstChanceException -= OnFirstChanceException;
      AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
      _monitor = null;
      _entriesToSend.CompleteAdding();

      foreach (var plugIn in PlugIns.ToArray())
      {
        plugIn.OnDetach();
        PlugIns.Remove(plugIn);
      }
    }

    private void DisableLogging()
    {
      DebugAppender.LogEntryAdded -= OnLogEntryAdded;
    }
  }
}
