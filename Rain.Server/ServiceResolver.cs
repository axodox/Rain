using Rain.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Discovery;

namespace Rain.Server
{
  public class ServiceResolver : IDisposable
  {
    private static ServiceHost _discoveryServiceHost;

    private static ConcurrentDictionary<string, ServiceResolver> _discoverers;

    static ServiceResolver()
    {
      _discoverers = new ConcurrentDictionary<string, ServiceResolver>();

      var announcementService = new AnnouncementService();
      announcementService.OnlineAnnouncementReceived += OnOnlineAnnouncementReceived;
      announcementService.OfflineAnnouncementReceived += OnOfflineAnnouncementReceived;

      _discoveryServiceHost = new ServiceHost(announcementService);
      if (_discoveryServiceHost.Description.Behaviors.Any(p => p is ServiceMetadataBehavior))
      {
        _discoveryServiceHost.Description.Behaviors.Remove(_discoveryServiceHost.Description.Behaviors.First(p => p is ServiceMetadataBehavior));
      }
      _discoveryServiceHost.AddServiceEndpoint(new UdpAnnouncementEndpoint());
      _discoveryServiceHost.Open();
    }

    static void OnOfflineAnnouncementReceived(object sender, AnnouncementEventArgs e)
    {
      foreach (var contractType in e.EndpointDiscoveryMetadata.ContractTypeNames)
      {
        if (_discoverers.TryGetValue(contractType.Name, out var resolver))
        {
          resolver.OnServiceOfflineAnnouncementReceived(e.EndpointDiscoveryMetadata);
        }
      }
    }

    static void OnOnlineAnnouncementReceived(object sender, AnnouncementEventArgs e)
    {
      foreach (var wContractType in e.EndpointDiscoveryMetadata.ContractTypeNames)
      {
        if (_discoverers.TryGetValue(wContractType.Name, out var resolver))
        {
          resolver.OnServiceOnlineAnnouncementReceived(e.EndpointDiscoveryMetadata);
        }
      }
    }

    public static ServiceResolver Create<TServiceInterface>()
    {
      var type = typeof(TServiceInterface);
      if (!_discoverers.TryGetValue(type.Name, out var discoverer))
      {
        discoverer = new ServiceResolver(type);
      }
      return discoverer;
    }

    public Type ServiceInterface { get; private set; }

    public event Action<EndpointDiscoveryMetadata> EndpointFound, EndpointLost;

    private DiscoveryClient _discoveryClient;

    private ServiceResolver(Type serviceInterface)
    {
      ServiceInterface = serviceInterface;
      _discoverers[serviceInterface.Name] = this;

      _discoveryClient = new DiscoveryClient(new UdpDiscoveryEndpoint());
      _discoveryClient.FindCompleted += OnServiceFound;
    }

    public void Probe(TimeSpan timeout = default(TimeSpan))
    {
      _discoveryClient.FindAsync(new FindCriteria(ServiceInterface)
      {
        MaxResults = timeout == TimeSpan.Zero ? 1 : int.MaxValue,
        Duration = timeout == TimeSpan.Zero ? TimeSpan.MaxValue : timeout
      });
    }

    private void OnServiceFound(object sender, FindCompletedEventArgs e)
    {
      var endpoints = new List<EndpointDiscoveryMetadata>(e.Result.Endpoints);
      var hostName = Environment.MachineName.ToLower();
      var hostIndex = endpoints.FindIndex(p => p.Address.Uri.ToString().Contains(hostName));
      if (hostIndex != -1)
      {
        var wHostEndpoint = endpoints[hostIndex];
        endpoints.RemoveAt(hostIndex);
        endpoints.Insert(0, wHostEndpoint);
      }
      foreach (var endpointDiscoveryMetadata in endpoints)
      {
        OnServiceOnlineAnnouncementReceived(endpointDiscoveryMetadata);
      }
    }

    private void OnServiceOnlineAnnouncementReceived(EndpointDiscoveryMetadata metadata)
    {
      EndpointFound?.Invoke(metadata);
    }

    private void OnServiceOfflineAnnouncementReceived(EndpointDiscoveryMetadata metadata)
    {
      EndpointLost?.Invoke(metadata);
    }

    private bool _isDisposed;
    public void Dispose()
    {
      if (!_isDisposed)
      {
        _isDisposed = true;
        _discoverers.TryRemove(ServiceInterface.Name, out _);
        _discoveryClient.Close();
      }
    }

    ~ServiceResolver()
    {
      Dispose();
    }
  }
}
