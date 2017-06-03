using Rain.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ServiceModel;

namespace Rain.Server
{
  [CallbackBehavior(UseSynchronizationContext = false, ConcurrencyMode = ConcurrencyMode.Multiple)]
  public class DebugMonitor : IDebugMonitor
  {
    private HashSet<ClientInfo> _connections = new HashSet<ClientInfo>();

    private ConcurrentDictionary<object, ClientContext> _clients = new ConcurrentDictionary<object, ClientContext>();
    private static readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(1);

    private ServiceResolver _dynamicServiceResolver;

    public DebugMonitor()
    {
      _dynamicServiceResolver = ServiceResolver.Create<IDebugService>();
      _dynamicServiceResolver.EndpointFound += OnEndpointFound;
      _dynamicServiceResolver.Probe(_probeTimeout);
    }

    private void OnEndpointFound(System.ServiceModel.Discovery.EndpointDiscoveryMetadata obj)
    {
      try
      {
        var channelFactory = new DuplexChannelFactory<IDebugService>(this, NetworkingHelper.CreateTcpBinding());
        var debugService = channelFactory.CreateChannel(obj.Address);
        var info = debugService.Initialize();
        if (_connections.Add(info))
        {
          _clients[debugService] = new ClientContext(debugService, info);
          var communicationObject = (debugService as ICommunicationObject);
          communicationObject.Faulted += OnEndpointLost;
          communicationObject.Closed += OnEndpointLost;
        }
        else
        {
          debugService.Close();
          (debugService as ICommunicationObject).Close();
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(e.Message);

        //Try restore connection
        _dynamicServiceResolver.Probe(_probeTimeout);
      }
    }

    private void OnEndpointLost(object sender, EventArgs e)
    {
      if (_clients.TryRemove(sender, out var context))
      {
        _connections.Remove(context.Info);
      }

      //Try restore connection
      _dynamicServiceResolver.Probe(_probeTimeout);
    }

    public event EventHandler<LogEntry> LogReceived;

    public event EventHandler<ExceptionEntry> ExceptionReceived;

    public void AddLogEntry(LogEntry entry)
    {
      if (LogReceived != null && _clients.TryGetValue(OperationContext.Current.Channel, out var context))
      {
        LogReceived(context, entry);
      }
    }

    public void AddExceptionEntry(ExceptionEntry exceptionEntry)
    {
      if (LogReceived != null && _clients.TryGetValue(OperationContext.Current.Channel, out var context))
      {
        ExceptionReceived(context, exceptionEntry);
      }
    }
  }
}
