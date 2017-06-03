using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Rain.Server
{
  public class DebugProvider : IDisposable
  {
    [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWow64Process([In] IntPtr hProcess, [Out] out bool lpSystemInfo);

    private HashSet<string> _processesToMonitor = new HashSet<string>();

    private HashSet<int> _monitoredProcesses = new HashSet<int>();

    private bool _isEnabled;

    private Thread _monitoringThread;

    public DebugProvider()
    {
      _isEnabled = true;

      _monitoringThread = new Thread(Worker);
      _monitoringThread.Start();
    }

    private void Worker()
    {
      var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

      while (_isEnabled)
      {
        var processes = Process.GetProcesses()
          .Where(p => !_monitoredProcesses.Contains(p.Id))
          .Where(p => _processesToMonitor.Contains(p.ProcessName) || _processesToMonitor.Contains(p.Id.ToString()))
          .ToList();

        foreach (var process in processes)
        {
          if (IsWow64Process(process.Handle, out var isX86Process))
          {
            var path = Path.Combine(directory, isX86Process ? "Rain.Injector.x86.exe" : "Rain.Injector.x64.exe");
            var arguments = process.Id.ToString();

            var psExecProcess = new Process()
            {
              StartInfo = new ProcessStartInfo()
              {
                UseShellExecute = false,
                Arguments = string.Format("-accepteula -s -i {0} \"{1}\" {2}", process.SessionId, path, arguments),
                CreateNoWindow = false,
                FileName = Path.Combine(directory, "PsExec64.exe")
              }
            };

            psExecProcess.Start();
            psExecProcess.WaitForExit();
          }

          _monitoredProcesses.Add(process.Id);
        }

        Thread.Sleep(1000);
      }
    }

    public void Monitor(string processName)
    {
      _processesToMonitor.Add(processName);
    }

    public void Monitor(int processId)
    {
      _processesToMonitor.Add(processId.ToString());
    }

    public void Dispose()
    {
      if (_isEnabled)
      {
        _isEnabled = false;
        _monitoringThread.Join();
      }
    }
  }
}
