using System.Runtime.Serialization;

namespace Rain.Client
{
  [DataContract]
  public class ClientInfo
  {
    [DataMember]
    public int ProcessId { get; set; }

    [DataMember]
    public string ProcessName { get; set; }

    [DataMember]
    public string ProcessLocation { get; set; }

    [DataMember]
    public int DomainId { get; set; }

    [DataMember]
    public string DomainName { get; set; }

    [DataMember]
    public string MachineName { get; set; }

    [DataMember]
    public bool IsX64Process { get; set; }

    public override bool Equals(object obj)
    {
      return obj is ClientInfo &&
        (obj as ClientInfo).MachineName == MachineName &&
        (obj as ClientInfo).ProcessId == ProcessId &&
        (obj as ClientInfo).DomainId == DomainId;
    }

    public override int GetHashCode()
    {
      return MachineName.GetHashCode() ^ ProcessId ^ DomainId.GetHashCode();
    }
  }
}
