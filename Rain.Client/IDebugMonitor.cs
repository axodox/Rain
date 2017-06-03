using System.ServiceModel;

namespace Rain.Client
{
  [ServiceContract]
  public interface IDebugMonitor
  {
    [OperationContract(IsOneWay = true)]
    void AddLogEntry(LogEntry logEntry);

    [OperationContract(IsOneWay = true)]
    void AddExceptionEntry(ExceptionEntry exceptionEntry);
  }
}
