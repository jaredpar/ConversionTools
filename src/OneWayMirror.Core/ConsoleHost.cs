using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWayMirror.Core
{
    public class ConsoleHost : IHost
    {
        private readonly bool _verbose;
        
        public ConsoleHost(bool verbose = false)
        {
            _verbose = verbose;
        }

        public virtual bool ConfirmCheckin(string shelvesetName)
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

        public virtual void Verbose(string format, params object[] args)
        {
            if (_verbose)
            {
                Console.WriteLine(format, args);
            }
        }

        public virtual void Status(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }

        public virtual void Error(string format, params object[] args)
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
