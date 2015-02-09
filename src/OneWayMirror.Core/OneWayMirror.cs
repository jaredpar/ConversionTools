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
        private readonly string _remoteName;
        private readonly Uri _repositoryUrl;
        private readonly Credentials _repositoryCredentials;
        private readonly bool _confirmBeforeCheckin;
        private readonly bool _lockWorkspacePath;

        private Tree _cachedWorkspaceTree;

        internal OneWayMirror(
            IHost host,
            Workspace workspace,
            string workspacePath,
            Repository repository,
            Uri repositoryUrl,
            Credentials repositoryCredentials,
            string remoteName,
            bool confirmBeforeCheckin,
            bool lockWorkspacePath)
        {
            _host = host;
            _workspace = workspace;
            _workspacePath = workspacePath;
            _repository = repository;
            _repositoryUrl = repositoryUrl;
            _repositoryCredentials = repositoryCredentials;
            _remoteName = remoteName;
            _confirmBeforeCheckin = confirmBeforeCheckin;
            _lockWorkspacePath = lockWorkspacePath;
        }

        /// <summary>
        /// Run the server loop assuming the last successfully sync'd commit is the given value.
        /// </summary>
        internal void Run(Commit baseCommit)
        {
            LockWorkspacePath();
            try
            {
                RunCore(baseCommit);
            }
            finally
            {
                UnlockWorkspacePath();
            }
        }

        internal void RunCore(Commit baseCommit)
        {
            // TODO: Add cancellation, async, etc ... to the loop 
            while (true)
            {
                if (!FetchGitLatest())
                {
                    return;
                }

                var refName = string.Format("refs/remotes/{0}/master", _remoteName);
                var commit = _repository.Refs[refName].ResolveAs<Commit>();
                if (commit.Sha == baseCommit.Sha)
                {
                    _host.Verbose("No changes detected, waiting");
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

        private void LockWorkspacePath()
        {
            Debug.Assert(_lockWorkspacePath);
            _workspace.SetLock(_workspacePath, LockLevel.Checkin);
        }

        private void UnlockWorkspacePath()
        {
            Debug.Assert(_lockWorkspacePath);
            _workspace.SetLock(_workspacePath, LockLevel.None);
        }

        private bool FetchGitLatest()
        {
            _host.Verbose("Updating Git from {0}/master.", _remoteName);

            var remote = _repository.Network.Remotes[_remoteName];
            if (remote == null)
            {
                _host.Error("Git repository must have a remote named {0}", _remoteName);
                return false;
            }

            var fetchOptions = new FetchOptions();
            fetchOptions.CredentialsProvider = (url, userNameForUrl, type) => _repositoryCredentials;

            try
            {
                _repository.Network.Fetch(remote, fetchOptions);
                return true;
            }
            catch (Exception ex)
            {
                _host.Error("Error fetching {0}: {1}", _remoteName, ex.Message);

                // The git protocol not correctly supported by libgit2sharp at the moment
                if (remote.Url.Contains("git@github"))
                {
                    _host.Error("{0} remote must have an https URL, currently git@github form", _remoteName);
                }

                return false;
            }
        }

        /// <summary>
        /// Does the workspace have pending changes other than a Lock against Open?
        /// </summary>
        /// <returns></returns>
        private bool HasPendingChangesBesidesLock()
        {
            var pendingChanges = _workspace.GetPendingChanges();
            if (pendingChanges.Length == 0)
            {
                return false;
            }

            if (pendingChanges.Length == 1 && pendingChanges[0].ChangeType == ChangeType.Lock)
            {
                return false;
            }

            return true;
        }

        private bool UpdateWorkspaceToLatest()
        {
            _host.Verbose("Updating TFS from server");
            if (HasPendingChangesBesidesLock())
            { 
                _host.Error("Pending changes detected in TFS enlistment");
                return false;
            }

            try
            {
                var itemSpec = new ItemSpec(_workspacePath, RecursionType.Full);
                var getStatus = _workspace.Get(new GetRequest(itemSpec, VersionSpec.Latest), GetOptions.NoAutoResolve);

                // If the get performed any actual work then kill the cached tree that we have.  
                if (!getStatus.NoActionNeeded)
                {
                    _cachedWorkspaceTree = null;
                }

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
            Debug.Assert(!HasPendingChangesBesidesLock());

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
                _cachedWorkspaceTree = newCommit.Tree;
            }
            catch (Exception ex)
            {
                _host.Error("Unable to complete checkin: {0}", ex.Message);
                _cachedWorkspaceTree = null;
                return false;
            }

            _host.Status("Checkin complete for {0}", commitRange.NewCommit.Sha);

            // The check in will undo the lock so re-lock now
            if (_lockWorkspacePath)
            {
                LockWorkspacePath();
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

            var commitUri = _repositoryUrl + string.Format("/commit/{0}", commitRange.NewCommit.Sha);
            builder.AppendFormatLine("[git-commit-url: {0}", commitUri);

            builder.AppendLine();
            builder.AppendLine("Original Commit Message");
            builder.AppendLine(commitRange.NewCommit.Message);

            return builder.ToString();
        }

        /// <summary>
        /// Apply the specified changes to the TFS workspace.  This won't commit the changes.
        /// </summary>
        private bool ApplyGitChangeToWorkspace(TreeChanges treeChanges)
        {
            Debug.Assert(treeChanges.Any());
            Debug.Assert(!HasPendingChangesBesidesLock());

            foreach (var changeEntry in treeChanges)
            {
                var tfsFilePath = GetTfsWorkspacePath(changeEntry.Path);
                var tfsFileName = Path.GetFileName(tfsFilePath);
                switch (changeEntry.Status)
                {
                    case ChangeKind.Added:
                        _host.Verbose("Pending add {0}", tfsFileName);
                        WriteObjectToFile(tfsFilePath, changeEntry.Oid, changeEntry.Path);
                        _workspace.PendAdd(tfsFilePath);
                        break;
                    case ChangeKind.Deleted:
                        _host.Verbose("Pending delete {0}", tfsFileName);
                        _workspace.PendDelete(tfsFilePath);
                        break;
                    case ChangeKind.Renamed:
                        _workspace.PendRename(GetTfsWorkspacePath(changeEntry.OldPath), tfsFilePath);

                        if (changeEntry.OldOid != changeEntry.Oid)
                        {
                            _workspace.PendEdit(tfsFilePath);
                            CopyObjectToFile(tfsFilePath, changeEntry.Oid, changeEntry.Path);
                        }

                        // If there is a path case difference between TFS and Git then it will keep showing
                        // up as a rename until an explicit path change occurs in TFS.  Don't report such
                        // differences as pends because it just causes noise to the output.
                        if (!StringComparer.OrdinalIgnoreCase.Equals(changeEntry.OldPath, changeEntry.Path))
                        {
                            _host.Verbose("Pending rename {0}", tfsFileName);
                        }

                        break;
                    case ChangeKind.Modified:
                        Debug.Assert(changeEntry.Oid != changeEntry.OldOid || changeEntry.OldMode != changeEntry.Mode);
                        if (changeEntry.OldOid != changeEntry.Oid)
                        {
                            _host.Verbose("Pending edit {0}", tfsFileName);
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

        private Tree GetOrCreateWorkspaceTree()
        {
            // Creating a Tree object for a Workspace is relatively expensive.  It requires reading all the files
            // on disk.  Given that we are the primary updaters of the tree we can safely cache the tree value to
            // avoid the operation.
            if (_cachedWorkspaceTree == null)
            {
                _cachedWorkspaceTree = GitUtils.CreateTreeFromWorkspace(_workspace, _workspacePath, _repository.ObjectDatabase);
            }

            return _cachedWorkspaceTree;
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
