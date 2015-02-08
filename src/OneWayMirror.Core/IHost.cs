using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWayMirror.Core
{
    public interface IHost
    {
        bool ConfirmCheckin(string shelvesetName);
    }
}
