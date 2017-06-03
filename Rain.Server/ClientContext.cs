using Rain.Client;

namespace Rain.Server
{
  public class ClientContext
  {
    public IDebugService Client { get; private set; }

    public ClientInfo Info { get; private set; }

    public ClientContext(IDebugService client, ClientInfo info)
    {
      Client = client;
      Info = info;
    }
  }
}
