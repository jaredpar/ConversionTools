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
            string description = ParseDescription(document);
            List<Comment> comments = ParseComments(document);
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

        internal static string ParseDescription(HtmlDocument document)
        {
            var div = document
                .DocumentNode
                .Descendants("div")
                .Where(x => HasAttribute(x, "id", "descriptionContent"))
                .Single();
            return ParseContent(div);
        }

        internal static List<Comment> ParseComments(HtmlDocument document)
        {
            var div = document
                .DocumentNode
                .Descendants("div")
                .Where(x => HasAttribute(x, "id", "CommentsList"))
                .Single();
            var list = new List<Comment>();
            foreach (var commentDiv in div.ChildNodes.Where(x => x.Name == "div"))
            {
                // TODO: Need to parse out the commentor
                list.Add(ParseComment(commentDiv));
            }

            return list;
        }

        internal static Comment ParseComment(HtmlNode node)
        {
            var div = node
                .Descendants("div")
                .Where(x => x.GetAttributeValue("class", "").StartsWith("markDownOutput"))
                .Single();
            var content = ParseContent(div);
            var user = node
                .Descendants("a")
                .Where(x => x.GetAttributeValue("id", "").StartsWith("PostedByLink"))
                .Single()
                .InnerText;
            return new Comment(user, content);
        }

        internal static string ParseContent(HtmlNode node)
        {
            var builder = new StringBuilder();
            foreach (var child in node.ChildNodes)
            {
                if (child.Name == "pre")
                {
                    builder.AppendLine("``` csharp");
                    builder.AppendLine(child.InnerText);
                    builder.AppendLine("```");
                }
                else
                {
                    builder.Append(child.InnerText);
                }
            }

            return builder.ToString();
        }

        internal static bool HasAttribute(HtmlNode node, string name, string value)
        {
            return node.GetAttributeValue(name, "") == value;
        }
    }
}
