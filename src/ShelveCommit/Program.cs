using LibGit2Sharp;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using OneWayMirror.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShelveCommit
{
    internal static class Program
   {
        internal static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Usage();
                return 1;
            }

            var repository = new Repository(args[0]);
            var commit = repository.Lookup(args[1]) as Commit;
            if (commit == null)
            {
                Usage();
                return 1;
            }

            Go(repository, commit);
            return 0;
        }

        private static void Usage()
        {
            Console.WriteLine("shelvecommit [git-repo-path] [commit-sha]");
        }

        private static void Go(Repository repository, Commit commit)
        {
            Workspace workspace = null;
            WorkingFolder folder = null;
            string tempPath = null;
            try
            {
                workspace = CreateWorkspace();
                tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempPath);

                Console.WriteLine("Creating scratch TFS enlistment");
                folder = AddMapping(workspace, tempPath);

                Console.WriteLine("Building Git Tree");
                var workspaceTree = GitUtils.CreateTreeFromWorkspace(workspace, tempPath, repository.ObjectDatabase);

                Console.WriteLine("Calculating diff");
                var treeChanges = repository.Diff.Compare<TreeChanges>(workspaceTree, commit.Tree, compareOptions: new LibGit2Sharp.CompareOptions() { Similarity = SimilarityOptions.Renames });

                Console.WriteLine("Applying commit to workspace");
                var commitPortUtil = new CommitPortUtil(repository, workspace, tempPath);
                commitPortUtil.ApplyGitChangeToWorkspace(treeChanges);

                var shelvesetName = string.Format("shelve-commit-{0}", commit.Sha);
                Console.WriteLine("Shelving to {0}", shelvesetName);
                ShelveChanges(workspace, shelvesetName, commit);
            }
            finally
            {
                if (workspace != null)
                {
                    Console.WriteLine("Cleaning up TFS enlistment");
                    if (folder != null)
                    {
                        workspace.DeleteMapping(folder);
                    }

                    workspace.Delete();
                }

                if (tempPath != null && Directory.Exists(tempPath))
                {
                    Console.WriteLine("Cleaning up temp files");

                    try
                    {
                        Directory.Delete(tempPath, recursive: true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: {0}", ex.Message);
                    }
                }
            }
        }

        private static Workspace CreateWorkspace()
        {
            var tfsCollection = new Uri("http://vstfdevdiv:8080/DevDiv2");
            var tfsServer = new TfsTeamProjectCollection(tfsCollection);
            tfsServer.EnsureAuthenticated();
            var vcServer = tfsServer.GetService<VersionControlServer>();
            return vcServer.CreateWorkspace("shelve-git-commit " + Guid.NewGuid().ToString());
        }

        private static void ShelveChanges(Workspace workspace, string shelvesetName, Commit commit)
        {
            var shelveset = new Shelveset(workspace.VersionControlServer, shelvesetName, workspace.OwnerName);
            shelveset.Comment = string.Format("TFS shelve of Git Commit {0}{1}{2}", commit.Sha, Environment.NewLine, commit.Message);

            workspace.Shelve(shelveset, workspace.GetPendingChanges(), ShelvingOptions.None);
        }

        private static WorkingFolder AddMapping(Workspace workspace, string tempPath)
        {
            var folder = new WorkingFolder("$/Roslyn/Main/Open", tempPath);
            workspace.CreateMapping(folder);
            workspace.Get();
            return folder;
        }
    }
}
