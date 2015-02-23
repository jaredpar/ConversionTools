using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortCodePlexToGitHub
{
    internal static class Program
    {
        internal static void Main(string[] args)
        {
            var converter = new CodePlexIssueConverter();
            var codePlexIssue = converter.Convert(20);
            
        }
    }
}
