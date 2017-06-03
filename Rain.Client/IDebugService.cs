using System.ServiceModel;

namespace Rain.Client
{
  [ServiceContract(
    SessionMode = SessionMode.Required,
    CallbackContract = typeof(IDebugMonitor))]
  public interface IDebugService
  {
    [OperationContract(IsInitiating = true)]
    ClientInfo Initialize();

    [OperationContract(IsTerminating = true)]
    void Close();
  }
}
