using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PortCodePlexToGitHub
{
    internal sealed class CodePlexIssueConverter
    {
        private readonly HtmlWeb _htmlWeb = new HtmlWeb();

        internal CodePlexIssue Convert(int id)
        {
            var rawUrl = string.Format("http://roslyn.codeplex.com/workitem/{0}", id);
            var htmlDocument = _htmlWeb.Load(rawUrl);
            return Parse(htmlDocument);
        }

        internal CodePlexIssue Parse(HtmlDocument document)
        {
            int id = ParseId(document);
            return null;
        }

        internal static int ParseId(HtmlDocument document)
        {
            var div = document
                .DocumentNode
                .Descendants("div")
                .Where(x => HasAttribute(x, "class", "votebox"))
                .First();
            var idText = div.GetAttributeValue("d:workItemId", "");
            return int.Parse(idText);
        }

        internal static bool HasAttribute(HtmlNode node, string name, string value)
        {
            return node.GetAttributeValue(name, "") == value;
        }
    }
}
