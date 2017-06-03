using System.Runtime.Serialization;

namespace Rain.Client
{
  [DataContract]
  public class LogEntry : Entry
  {
    [DataMember]
    public LogEntrySeverity Severity { get; set; }

    [DataMember]
    public string Source { get; set; }

    [DataMember]
    public string Text { get; set; }
  }
}
