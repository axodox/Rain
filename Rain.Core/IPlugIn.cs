using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rain.Core
{
  public interface IRainPlugIn
  {
    void OnAttach();

    void OnDetach();
  }
}
