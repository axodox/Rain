using System.Runtime.Serialization;

namespace Rain.Client
{
  [DataContract]
  public enum LogEntrySeverity : int
  {
    [EnumMember]
    Unknown,
    [EnumMember]
    All,
    [EnumMember]
    Debug,
    [EnumMember]
    Info,
    [EnumMember]
    Warn,
    [EnumMember]
    Error,
    [EnumMember]
    Fatal,
    [EnumMember]
    Off
  }
}
