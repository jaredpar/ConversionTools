using LibGit2Sharp;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreateFullEnlistment
{
    internal static class Program
    {
        private static readonly Uri s_tfsCollection = new Uri("http://vstfdevdiv:8080/DevDiv2");

        internal static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Usage();
                return 1;
            }

            var workspacePath = args[0];
            var githubUserName = args[1];
            var workspace = GetWorkspace(workspacePath);
            if (workspace == null ||
                !AdjustWorkspace(workspace, workspacePath) ||
                !CreateRepository(workspacePath, githubUserName))
            {
                return 1;
            }

            Console.WriteLine("Completed");
            return 0;
        }

        private static void Usage()
        {
            Console.WriteLine("CreateFullEnlistment [tfs-workspace-path] [github-user-name]");
        }

        private static Workspace GetWorkspace(string workspacePath)
        {
            try
            {
                var tfsServer = new TfsTeamProjectCollection(s_tfsCollection);
                tfsServer.EnsureAuthenticated();

                var vcServer = tfsServer.GetService<VersionControlServer>();
                return vcServer.GetWorkspace(workspacePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Cannot get TFS workspace: {0}", ex.Message);
                return null;
            }
        }

        private static bool AdjustWorkspace(Workspace workspace, string workspacePath)
        {
            if (workspace.GetPendingChanges().Length > 0)
            {
                Console.WriteLine("Workspace cannot have pending changes");
                return false;
            }

            if (!workspace.IsServerPathMapped(@"$/Roslyn/Main"))
            {
                Console.WriteLine("This workspace does not map Roslyn Main");
                return false;
            }

            var openPath = @"$/Roslyn/Main/Open";
            if (workspace.IsServerPathMapped(openPath))
            {
                Console.WriteLine("Cloaking and removing Open");
                workspace.Cloak(openPath);
            }

            // Ensure the TFS info is removed.  Unconditionally perform this operation 
            // as the last tool run could have thrown at this point. 
            Console.WriteLine("Sync TFS to latest");
            workspace.Get();

            // Delete all file system remnants
            var path = Path.Combine(workspacePath, "Open");
            try
            {
                if (Directory.Exists(path))
                {
                    Console.WriteLine("Deleting existing Open directory");
                    ForceDeleteDirectory(path);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to delete Open: {0}", ex.Message);
                return false;
            }

            return true;
        }

        private static void ForceDeleteDirectory(string path)
        {
            var directory = new DirectoryInfo(path) { Attributes = FileAttributes.Normal };

            foreach (var info in directory.GetFileSystemInfos("*", SearchOption.AllDirectories))
            {
                info.Attributes = FileAttributes.Normal;
            }

            directory.Delete(recursive: true);
        }

        private static bool CreateRepository(string workspacePath, string githubUserName)
        {
            var repositoryPath = Path.Combine(workspacePath, "Open");
            if (!CloneRepository(repositoryPath, githubUserName))
            {
                return false;
            }

            var repository = new Repository(repositoryPath);

            // Now we need to fix up the remotes.  Clone uses the https:// format because that is what
            // libgit2sharp supports.  Better to use the git@ format as that is what the other tooling
            // like GitHub for Windows uses.  
            //
            // TODO: This is done mainly to help ensure that 2 factor auth is handled properly.  It's
            // possible that https:// handles this and hence this step is unneeded.  Haven't had time
            // to test it.  
            Console.WriteLine("Setting up remotes origin and upstream");
            repository.Network.Remotes.Remove("origin");
            repository.Network.Remotes.Add("origin", string.Format("git@github.com:{0}/roslyn.git", githubUserName));
            repository.Network.Remotes.Add("upstream", "https://github.com/dotnet/roslyn.git");

            return true;
        }

        private static bool CloneRepository(string repositoryPath, string githubUserName)
        {
            var forkCloneUrl = string.Format("https://github.com/{0}/roslyn.git", githubUserName);
            try
            {
                Console.WriteLine("Cloning {0}", forkCloneUrl);
                Repository.Clone(forkCloneUrl, repositoryPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error cloning: {0}", ex.Message);
                return false;
            }

            return true;
        }
    }
}
