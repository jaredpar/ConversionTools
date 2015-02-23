using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortCodePlexToGitHub
{
    internal sealed class CodePlexIssue
    {
        internal readonly int Id;
        internal string Description;
        internal List<string> Comments;
    }
}
