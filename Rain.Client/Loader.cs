using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Discovery;
using System.Threading;

namespace Rain.Client
{
  public class Loader
  {
    static bool _isLoaded = false;

    public static bool IsLogging { get; private set; }

    public Loader()
    {
      if (_isLoaded) return;
      _isLoaded = true;

      //Debugger.Launch();
      AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

      if (AppDomain.CurrentDomain.GetAssemblies().Any(p => StringComparer.OrdinalIgnoreCase.Equals(p.GetName().Name, "log4net")))
      {
        InitializeLogging();
      }
      LoggingTraceListener.Initialize();

      var loadedEvent = new AutoResetEvent(false);

      var thread = new Thread(() =>
      {
        Thread.CurrentThread.Name = "Debug thread for Rain";
        Thread.CurrentThread.IsBackground = true;

        ServiceHostBase serviceHost = null;
        try
        {
          serviceHost = LoadService<IDebugService, DebugService>();
          serviceHost.Open();
        }
        catch (Exception e)
        {
          Console.WriteLine(e.GetDescription());
        }

        loadedEvent.Set();

        try
        {
          Thread.Sleep(Timeout.Infinite);
        }
        catch
        {
          serviceHost.Close();
        }
      });

      thread.Start();
      loadedEvent.WaitOne();
      loadedEvent.Dispose();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InitializeLogging()
    {
      DebugAppender.Initialize();
      IsLogging = true;
    }

    private static ServiceHostBase LoadService<TServiceContract, TServiceImplementation>()
    {
      var address = NetworkingHelper.GetFreeTcpServiceAddress();
      Console.WriteLine(typeof(TServiceContract).Name + ": " + address);
      var host = new ServiceHost(typeof(TServiceImplementation), new[] { address });

      host.AddServiceEndpoint(typeof(TServiceContract), NetworkingHelper.CreateTcpBinding(), address);

      var serviceDiscoveryBehavior = new ServiceDiscoveryBehavior();
      serviceDiscoveryBehavior.AnnouncementEndpoints.Add(new UdpAnnouncementEndpoint());

      host.Description.Behaviors.Add(serviceDiscoveryBehavior);

      var metadateBehavior = host.Description.Behaviors.FirstOrDefault(p => p is ServiceMetadataBehavior);
      if (metadateBehavior != null)
      {
        host.Description.Behaviors.Remove(metadateBehavior);
      }

      host.AddServiceEndpoint(new UdpDiscoveryEndpoint());
      return host;
    }

    Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
    {
      var name = Path.GetFileName(args.Name.Contains(',') ? args.Name.Substring(0, args.Name.IndexOf(',')) : args.Name);
      var assembly = AppDomain.CurrentDomain.GetAssemblies().Where(p => !p.IsDynamic && p.GetName().Name == name).FirstOrDefault();
      return assembly;
    }
  }
}
