using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OneWayMirror.Core;

namespace OneWayMirror
{
    internal static class Program
    {
        internal static void Main(string[] args)
        {
            var tfsCollection = new Uri("http://vstfdevdiv:8080/DevDiv2");
            var sha = OneWayMirrorUtil.FindLastMirroredSha(tfsCollection, @"c:\dd\ros-tfs");
            Console.WriteLine("Last sha is {0}", sha);
        }
    }
}
