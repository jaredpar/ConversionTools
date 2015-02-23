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
        internal readonly string Description;
        internal readonly List<Comment> Comments;
    }

    internal sealed class Comment
    {
        internal readonly string User;
        internal readonly string Content;

        internal Comment(string user, string content)
        {
            User = user;
            Content = content;
        }
    }

}
