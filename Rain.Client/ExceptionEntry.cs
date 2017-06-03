using System.Runtime.Serialization;

namespace Rain.Client
{
  [DataContract]
  public class ExceptionEntry : Entry
  {
    [DataMember]
    public string Source { get; set; }

    [DataMember]
    public string Message { get; set; }

    [DataMember]
    public string Description { get; set; }

    public override bool Equals(object obj)
    {
      if (obj is ExceptionEntry exceptionEntry)
      {
        return exceptionEntry.Description == Description;
      }
      else
      {
        return false;
      }
    }

    public static bool operator ==(ExceptionEntry a, ExceptionEntry b)
    {
      return (object)a == (object)b || ((object)a != null && a.Equals(b));
    }

    public static bool operator !=(ExceptionEntry a, ExceptionEntry b)
    {
      return (object)a != (object)b && ((object)a == null || !a.Equals(b));
    }

    public override int GetHashCode()
    {
      return Description.GetHashCode();
    }
  }
}
