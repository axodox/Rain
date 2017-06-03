using System;

namespace Rain.Client
{
  public static class Extensions
  {
    public static string GetDescription(this Exception exception)
    {
      var description = string.Format("{0}: {1}\r\n{2}",
          exception.GetType().Name,
          exception.Message,
          exception.StackTrace);

      if (exception.InnerException != null)
      {
        description += "\r\n" + exception.InnerException.GetDescription();
      }

      return description;
    }
  }
}
