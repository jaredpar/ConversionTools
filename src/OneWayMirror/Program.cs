using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OneWayMirror.Core;
using Microsoft.TeamFoundation.VersionControl.Client;
using LibGit2Sharp;
using System.IO;
using IniParser.Parser;

namespace OneWayMirror
{
    internal static class Program
    {
        internal static void Main(string[] args)
        {
            var parser = new IniDataParser();
            var data = parser.Parse(File.ReadAllText("onewaymirror.ini"));
            var tfsCollection = new Uri(data["tfs"]["collectionUri"]);
            var tfsWorkspacePath = data["tfs"]["workspacePath"];
            var tfsTargetPath = data["tfs"]["targetPath"];
            var gitRepositoryPath = data["git"]["repositoryPath"];
            var gitRepositoryUri = new Uri(data["git"]["repositoryUri"]);
            var gitRemoteName = data["git"]["remote"];
            var gitBranchName = data["git"]["branch"];
            var alertEmailAddress = data["general"]["alertEmailAddress"];

            OneWayMirrorUtil.Run(
                new ReportingConsoleHost(verbose: true, reportEmailAddress: alertEmailAddress),
                tfsCollection,
                tfsWorkspacePath,
                tfsTargetPath,
                gitRepositoryPath,
                gitRepositoryUri,
                gitRemoteName,
                gitBranchName,
                confirmBeforeCheckin: false,
                lockWorkspacePath: true);
        }

        /// <summary>
        /// Print out the Tree object to the command line.  This is useful for debugging purposes.
        /// </summary>
        private static void PrintTree(Tree tree, int depth = 0)
        {
            var prefix = new string(' ', depth * 2);
            foreach (var entry in tree)
            {
                Console.Write(prefix);
                Console.WriteLine(entry.Path);

                if (entry.TargetType == TreeEntryTargetType.Tree)
                {
                    PrintTree((Tree)entry.Target, depth + 1);
                }
            }
        }
    }
}
