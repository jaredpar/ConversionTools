using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWayMirror.Core
{
    public sealed class ConsoleHost : IHost
    {
        private readonly bool _verbose;
        
        public ConsoleHost(bool verbose = false)
        {
            _verbose = verbose;
        }

        bool IHost.ConfirmCheckin(string shelvesetName)
        {
            Console.WriteLine("Pending shelveset: {0}", shelvesetName);
            var comp = StringComparer.OrdinalIgnoreCase;

            while (true)
            {
                Console.WriteLine("Do you wish to checkin (yes / no)?");
                var answer = Console.ReadLine();
                if (comp.Equals(answer, "yes"))
                {
                    return true;
                }
                else if (comp.Equals(answer, "no"))
                {
                    return false;
                }
                else
                {
                    Console.WriteLine("Please enter yes or no");
                }
            }
        }

        void IHost.Verbose(string format, params object[] args)
        {
            if (_verbose)
            {
                Console.WriteLine(format, args);
            }
        }

        void IHost.Status(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }

        void IHost.Error(string format, params object[] args)
        {
            var color = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("Error!!! ");
                Console.WriteLine(format, args);
            }
            finally
            {
                Console.ForegroundColor = color;
            }
        }
    }
}
