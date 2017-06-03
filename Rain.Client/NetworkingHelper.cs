using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.ServiceModel;

namespace Rain.Client
{
  public static class NetworkingHelper
  {
    private const int _minAllowedTcpPort = 49152;
    private const int _maxAllowedTcpPort = 65535;

    public static Uri GetFreeTcpServiceAddress()
    {
      var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
      return new Uri(string.Format("net.tcp://{0}:{1}", ipGlobalProperties.HostName, GetFreeTcpPort()));
    }

    private static int GetFreeTcpPort(int rangeMinimum = _minAllowedTcpPort, int rangeMaximum = _maxAllowedTcpPort)
    {
      var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();

      var usedPorts = new List<int>();
      usedPorts.AddRange(ipGlobalProperties.GetActiveTcpConnections().Select(p => p.LocalEndPoint.Port));
      usedPorts.AddRange(ipGlobalProperties.GetActiveTcpListeners().Select(p => p.Port));

      var port = rangeMinimum;
      while ((usedPorts.Contains(port) || !TestTcpPort(port)) && port <= rangeMaximum)
      {
        port++;
      }

      if (port == rangeMaximum)
      {
        throw new Exception("Could not found free TCP port!");
      }

      return port;
    }

    private static bool TestTcpPort(int port)
    {
      try
      {
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        listener.Stop();
        return true;
      }
      catch
      {
        return false;
      }
    }

    public static NetTcpBinding CreateTcpBinding()
    {
      var netTcpBinding = new NetTcpBinding(SecurityMode.None)
      {
        MaxReceivedMessageSize = int.MaxValue,
        ReceiveTimeout = TimeSpan.MaxValue,
      };
      netTcpBinding.ReliableSession.Enabled = true;
      netTcpBinding.ReliableSession.InactivityTimeout = TimeSpan.FromSeconds(5);
      netTcpBinding.ReliableSession.Ordered = true;
      return netTcpBinding;
    }
  }
}
