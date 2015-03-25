using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWayMirror.Core
{
    public sealed class NullHost : IHost
    {
        public static readonly NullHost Instance = new NullHost();

        public bool ConfirmCheckin(string shelvesetName)
        {
            return true;
        }

        public void Verbose(string format, params object[] args)
        {
        }

        public void Status(string format, params object[] args)
        {
        }

        public void Error(string format, params object[] args)
        {
        }
    }
}
