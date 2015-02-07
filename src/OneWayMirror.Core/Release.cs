using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWayMirror.Core
{
    internal static class Release
    {
        public static void Assert(bool condition)
        {
            Assert(condition, "Assert failed.");
        }

        public static void Assert(bool condition, string format, params object[] args)
        {
            if (!condition)
            {
                Fail(format, args);
            }
        }

        public static void Fail()
        {
            Fail("Release Failure.");
        }

        public static void Fail(string format, params object[] args)
        {
            Debug.Assert(false, string.Format(format, args));
            Environment.Exit(-1);
        }
    }
}
