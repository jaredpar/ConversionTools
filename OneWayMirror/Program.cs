using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OneWayMirror.Core;
using Microsoft.TeamFoundation.VersionControl.Client;
using LibGit2Sharp;
using System.IO;

namespace OneWayMirror
{
    internal static class Program
    {
        internal static void Main(string[] args)
        {
            var tfsCollection = new Uri("http://vstfdevdiv:8080/DevDiv2");
            var tfsWorkspacePath = @"c:\dd\ros-tfs";
            var gitRepositoryPath = @"c:\users\jaredpar\Documents\GitHub\roslyn";
            var gitRepositoryUrl = new Uri("https://github.com/dotnet/roslyn");
            var token = File.ReadAllText(@"c:\users\jaredpar\token.txt").Trim();
            var credentials = new UsernamePasswordCredentials()
            {
                Username = "jaredpar",
                Password = token
            };

            OneWayMirrorUtil.Run(
                new ConsoleHost(verbose: true),
                tfsCollection,
                tfsWorkspacePath,
                "Open",
                gitRepositoryPath,
                gitRepositoryUrl,
                credentials,
                confirmBeforeCheckin: true);

            /*
            var sha = OneWayMirrorUtil.FindLastMirroredSha(tfsCollection, @"c:\dd\ros-tfs");
            Console.WriteLine("Last sha is {0}", sha);
            */

            /*
            var workspace = OneWayMirrorUtil.GetTfsWorkspace(tfsCollection, tfsWorkspacePath);
            var repository = new Repository(gitRepositoryPath);
            var target = Path.Combine(tfsWorkspacePath, @"Open\src\Test\Utilities");
            var tree1 = GitUtils.CreateTreeFromWorkspace(workspace, target, repository.ObjectDatabase);
            var tree2 = GitUtils.CreateTreeFromDirectory(target, repository.ObjectDatabase);
            PrintTree(tree1);
            PrintTree(tree2);
            */
        }

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
