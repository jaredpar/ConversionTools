using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWayMirror.Core
{
    public interface ILogger
    {
        void Information(string format, params object[] args);
        void Verbose(string format, params object[] args);
        void Warning(string format, params object[] args);
    }
}
