using LibGit2Sharp;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OneWayMirror.Core
{
    public static class OneWayMirrorUtil
    {
        private static readonly Regex s_checkinShaRegex = new Regex(@"\[git-commit-sha: ([a-z0-9]+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static void Run(
            IHost host,
            Uri tfsCollection,
            string tfsWorkspacePath,
            string tfsWorkspaceTargetDirectory,
            string tfsUserInfoMappingFilePath,
            string gitRepositoryPath,
            Uri gitRepositoryUrl,
            string gitRemoteName,
            string gitBranchName,
            bool confirmBeforeCheckin = false,
            bool lockWorkspacePath = false)
        {
            var tfsWorkspace = GetTfsWorkspace(tfsCollection, tfsWorkspacePath);
            var workspacePath = string.IsNullOrEmpty(tfsWorkspaceTargetDirectory)
                ? tfsWorkspacePath
                : Path.Combine(tfsWorkspacePath, tfsWorkspaceTargetDirectory);

            var gitRepository = new Repository(gitRepositoryPath);

            var oneWayMirror = new OneWayMirror(
                host,
                tfsWorkspace,
                workspacePath,
                tfsUserInfoMappingFilePath,
                gitRepository,
                gitRepositoryUrl,
                gitRemoteName,
                gitBranchName,
                confirmBeforeCheckin,
                lockWorkspacePath);

            var sha = FindLastMirroredSha(tfsWorkspace.VersionControlServer, workspacePath);
            if (sha == null)
            {
                host.Error("Could not find the last mirrored SHA1");
                return;
            }

            try
            {
                oneWayMirror.Run(sha);
            }
            catch (Exception ex)
            {
                host.Error("Mirror exitted with an exception: {0}{1}{2}", ex.Message, Environment.NewLine, ex.StackTrace);
            }
        }

        public static string FindLastMirroredSha(Uri tfsCollection, string tfsWorkspacePath)
        {
            var tfsWorkspace = GetTfsWorkspace(tfsCollection, tfsWorkspacePath);
            return FindLastMirroredSha(tfsWorkspace.VersionControlServer, tfsWorkspacePath).Sha;
        }

        public static Workspace GetTfsWorkspace(Uri tfsCollection, string tfsWorkspacePath)
        {
            try
            {
                var tfsServer = new TfsTeamProjectCollection(tfsCollection);
                tfsServer.EnsureAuthenticated();

                var vcServer = tfsServer.GetService<VersionControlServer>();

                // Can't use GetWorkspace(location) because it fails on machines with multiple 
                // workspaces that have the same server path.  Instead we have to query all local
                // workspaces and find the one with the right folder
                foreach (var workspace in vcServer.QueryWorkspaces(null, null, Environment.MachineName))
                {
                    foreach (var folder in workspace.Folders)
                    {
                        if (folder.LocalItem.StartsWith(tfsWorkspacePath, StringComparison.OrdinalIgnoreCase))
                        {
                            return workspace;
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Find the SHA1 which was last sync'd to this workspace.
        /// </summary>
        private static ObjectId FindLastMirroredSha(VersionControlServer vcServer, string path, int maxResults = 100)
        {
            var itemSpec = new ItemSpec(path, RecursionType.Full);
            var parameters = new QueryHistoryParameters(itemSpec);

            try
            {
                foreach (var changeset in vcServer.QueryHistory(itemSpec, maxResults))
                {
                    var comment = changeset.Comment;
                    var match = s_checkinShaRegex.Match(comment);
                    if (match.Success)
                    {
                        return new ObjectId(match.Groups[1].Value);
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
