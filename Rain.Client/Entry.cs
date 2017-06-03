using System;
using System.Runtime.Serialization;

namespace Rain.Client
{
  [DataContract]
  public class Entry
  {
    [DataMember]
    public int ProcessId { get; set; }

    [DataMember]
    public string ProcessName { get; set; }

    [DataMember]
    public DateTime TimeStamp { get; set; }
  }
}
