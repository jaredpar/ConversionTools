using LibGit2Sharp;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OneWayMirror.Core
{
    internal sealed class OneWayMirror
    {
        private struct CommitRange
        {
            internal readonly Commit OldCommit;
            internal readonly Commit NewCommit;

            internal CommitRange(Commit oldCommit, Commit newCommit)
            {
                OldCommit = oldCommit;
                NewCommit = newCommit;
            }

            public override string ToString()
            {
                return string.Format("{0} -> {1}", OldCommit.Sha.Substring(0, 6), NewCommit.Sha.Substring(0, 6));
            }
        }

        private readonly IHost _host;

        /// <summary>
        /// This is the path within the workspace where the git changes are mirrored to.  It 
        /// may be the exact location of the workspace or a sub directory within it.  
        /// </summary>
        private readonly string _workspacePath;
        private readonly Workspace _workspace;
        private readonly Repository _repository;
        private readonly Uri _repositoryUrl;
        private readonly Credentials _repositoryCredentials;
        private readonly bool _confirmBeforeCheckin;

        internal OneWayMirror(
            IHost host,
            Workspace workspace,
            string workspacePath,
            Repository repository, 
            Uri repositoryUrl,
            Credentials repositoryCredentials,
            bool confirmBeforeCheckin)
        {
            _host = host;
            _workspace = workspace;
            _workspacePath = workspacePath;
            _repository = repository;
            _repositoryUrl = repositoryUrl;
            _repositoryCredentials = repositoryCredentials;
            _confirmBeforeCheckin = confirmBeforeCheckin;
        }

        /// <summary>
        /// Run the server loop assuming the last successfully sync'd commit is the given value.
        /// </summary>
        internal void Run(Commit baseCommit)
        {
            // TODO: Add cancellation, async, etc ... to the loop 

            while (true)
            {
                if (!FetchGitLatest())
                {
                    return;
                }

                var commit = _repository.Refs["refs/remotes/upstream/master"].ResolveAs<Commit>();
                if (commit.Sha == baseCommit.Sha)
                {
                    Thread.Sleep(TimeSpan.FromMinutes(1));
                    continue;
                }

                if (!UpdateWorkspaceToLatest())
                {
                    return;
                }

                var commitRange = new CommitRange(oldCommit: baseCommit, newCommit: commit);
                if (!ApplyCommitToWorkspace(commitRange))
                {
                    return;
                }

                baseCommit = commit;
            }
        }

        private bool FetchGitLatest()
        {
            _host.Verbose("Updating Git from upstream/master.");

            var upstream = _repository.Network.Remotes["upstream"];
            if (upstream == null)
            {
                _host.Error("Git repository must have a remote named upstream");
                return false;
            }

            var fetchOptions = new FetchOptions();
            fetchOptions.CredentialsProvider = (url, userNameForUrl, type) => _repositoryCredentials;

            try
            {
                _repository.Network.Fetch(upstream, fetchOptions);
                return true;
            } 
            catch (Exception ex)
            {
                _host.Error("Error fetching upstream: {0}", ex.Message);

                // The git protocol not correctly supported by libgit2sharp at the moment
                if (upstream.Url.Contains("git@github"))
                {
                    _host.Error("upstream remote must have an https URL, currently git@github form");
                }

                return false;
            }
        }

        private bool UpdateWorkspaceToLatest()
        {
            _host.Verbose("Updating TFS from server");
            if (_workspace.GetPendingChanges().Length > 0)
            {
                _host.Error("Pending changes detected in TFS enlistment");
                return false;
            }

            try
            {
                _workspace.Get();
                return true;
            }
            catch (Exception ex)
            {
                _host.Error("Error syncing TFS: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Apply the changes between the two commits to the TFS workspace.  This will make the 
        /// changes as granular as possible. 
        /// </summary>
        private bool ApplyCommitToWorkspace(CommitRange commitRange)
        {
            var toApply = new Stack<CommitRange>();
            var current = commitRange.NewCommit;
            var oldCommit = commitRange.OldCommit;

            while (current.Sha != oldCommit.Sha && current.Parents.Count() == 1)
            {
                var parent = current.Parents.ElementAt(0);
                toApply.Push(new CommitRange(oldCommit: parent, newCommit: current));
                current = parent;
            }

            if (current.Sha != oldCommit.Sha)
            {
                toApply.Push(new CommitRange(oldCommit: oldCommit, newCommit: current));
            }

            while (toApply.Count > 0)
            {
                if (!ApplyCommitToWorkspaceCore(toApply.Pop()))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Apply the specific commit to the Workspace.  This method assumes the Workspace is in a good
        /// state at the time the application of the commit occurs.
        /// <return>True if the operation was completed successfully (or there was simply no work to do)</return>
        /// </summary>
        private bool ApplyCommitToWorkspaceCore(CommitRange commitRange)
        {
            Debug.Assert(_workspace.GetPendingChanges().Length == 0);

            var newCommit = commitRange.NewCommit;
            _host.Status("Applying {0}", newCommit);

            // Note: This is a suboptimal way of building the tree.  The file system can be changed by 
            // local builds and such.  Much better to build directly from the Workspace object.
            var workspaceTree = GitUtils.CreateTreeFromWorkspace(_workspace, _workspacePath, _repository.ObjectDatabase);
            var treeChanges = _repository.Diff.Compare<TreeChanges>(workspaceTree, newCommit.Tree, compareOptions: new LibGit2Sharp.CompareOptions() { Similarity = SimilarityOptions.Renames });

            if (!treeChanges.Any())
            {
                _host.Status("No changes to apply");
                return true;
            }

            if (!ApplyGitChangeToWorkspace(treeChanges))
            {
                return false;
            }

            var checkinMessage = CreateCheckinMessage(commitRange);
            if (_confirmBeforeCheckin && !ConfirmCheckin(newCommit, checkinMessage))
            {
                return false;
            }

            var checkinParameters = new WorkspaceCheckInParameters(_workspace.GetPendingChangesEnumerable(), checkinMessage);
            checkinParameters.NoAutoResolve = true;

            try
            {
                _workspace.CheckIn(checkinParameters);
            }
            catch (Exception ex)
            {
                _host.Error("Unable to complete checkin: {0}", ex.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Confirm to the host that we want to to check in the changes as currently staged.
        /// </summary>
        private bool ConfirmCheckin(Commit commit, string checkinMessage)
        {
            var shelvesetName = string.Format("gitToTfs-{0}", commit.Sha);
            var shelveset = new Shelveset(_workspace.VersionControlServer, shelvesetName, _workspace.OwnerName);
            shelveset.Comment = checkinMessage;

            _workspace.Shelve(shelveset, _workspace.GetPendingChanges(), ShelvingOptions.None);
            return _host.ConfirmCheckin(shelvesetName);
        }

        private string CreateCheckinMessage(CommitRange commitRange)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Port Git -> TFS");
            builder.AppendLine();
            builder.AppendFormatLine("From: {0}", commitRange.OldCommit.Sha);
            builder.AppendFormatLine("To: {0}", commitRange.NewCommit.Sha);
            builder.AppendLine();
            builder.AppendLine("This commit was generated automatically by a tool.  Contact dotnet-bot@microsoft.com for help.");
            builder.AppendLine();
            builder.AppendFormatLine("[git-commit-sha: {0}]", commitRange.NewCommit.Sha);

            var uriBuilder = new UriBuilder(_repositoryUrl);
            uriBuilder.Path = string.Format("commit/{0}", commitRange.NewCommit.Sha);
            builder.AppendFormatLine("[git-commit-url: {0}", uriBuilder.ToString());

            return builder.ToString();
        }

        /// <summary>
        /// Apply the specified changes to the TFS workspace.  This won't commit the changes.
        /// </summary>
        private bool ApplyGitChangeToWorkspace(TreeChanges treeChanges)
        {
            Debug.Assert(treeChanges.Any());
            Debug.Assert(_workspace.GetPendingChanges().Length == 0);

            // Construct a CS based on the Diff.
            foreach (var changeEntry in treeChanges)
            {
                var tfsFilePath = GetTfsWorkspacePath(changeEntry.Path);
                _host.Verbose("Pending change {0} {1} -> {2}", changeEntry.Status, changeEntry.Path, tfsFilePath);
                switch (changeEntry.Status)
                {
                    case ChangeKind.Added:
                        WriteObjectToFile(tfsFilePath, changeEntry.Oid, changeEntry.Path);
                        _workspace.PendAdd(tfsFilePath);
                        break;
                    case ChangeKind.Deleted:
                        _workspace.PendDelete(tfsFilePath);
                        break;
                    case ChangeKind.Renamed:
                        _workspace.PendRename(GetTfsWorkspacePath(changeEntry.OldPath), tfsFilePath);

                        if (changeEntry.OldOid != changeEntry.Oid)
                        {
                            _workspace.PendEdit(tfsFilePath);
                            CopyObjectToFile(tfsFilePath, changeEntry.Oid, changeEntry.Path);
                        }

                        break;
                    case ChangeKind.Modified:
                        Debug.Assert(changeEntry.Oid != changeEntry.OldOid || changeEntry.OldMode != changeEntry.Mode);
                        if (changeEntry.OldOid != changeEntry.Oid)
                        {
                            _workspace.PendEdit(tfsFilePath);
                            CopyObjectToFile(tfsFilePath, changeEntry.Oid, changeEntry.Path);
                        }
                        break;
                    default:
                        _host.Error("Unknown change status {0} {1}", changeEntry.Status, changeEntry.Path);
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Get the TFS file path for the given git relative path 
        /// </summary>
        private string GetTfsWorkspacePath(string gitPath)
        {
            Debug.Assert(gitPath[0] != '/');
            return Path.Combine(_workspacePath, gitPath);
        }

        private void WriteObjectToFile(string tfsFilePath, ObjectId objectId, string gitPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tfsFilePath));

            using (var fs = new FileStream(tfsFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                _repository.Lookup<Blob>(objectId).GetContentStream(new FilteringOptions(gitPath)).CopyTo(fs);
            }
        }

        private void CopyObjectToFile(string tfsFilePath, ObjectId oidOfFile, string gitPath)
        {
            using (var fs = new FileStream(tfsFilePath, FileMode.Truncate, FileAccess.Write, FileShare.None))
            {
                _repository.Lookup<Blob>(oidOfFile).GetContentStream(new FilteringOptions(gitPath)).CopyTo(fs);
            }
        }
    }
}
