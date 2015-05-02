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
    public sealed class CommitPortUtil
    {
        private readonly IHost _host;

        /// <summary>
        /// This is the path within the workspace where the git changes are mirrored to.  It 
        /// may be the exact location of the workspace or a sub directory within it.  
        /// </summary>
        private readonly string _workspacePath;
        private readonly Workspace _workspace;
        private readonly Repository _repository;

        public CommitPortUtil(
            Repository repository,
            Workspace workspace,
            string workspacePath,
            IHost host = null)
        {
            _host = host ?? NullHost.Instance;
            _workspace = workspace;
            _workspacePath = workspacePath;
            _repository = repository;
        }

        /// <summary>
        /// Apply the specified changes to the TFS workspace.  This won't commit the changes.
        /// </summary>
        public bool ApplyGitChangeToWorkspace(TreeChanges treeChanges)
        {
            Debug.Assert(treeChanges.Any());

            foreach (var changeEntry in treeChanges)
            {
                var tfsFilePath = GetTfsWorkspacePath(changeEntry.Path);
                var tfsFileName = Path.GetFileName(tfsFilePath);
                switch (changeEntry.Status)
                {
                    case ChangeKind.Added:
                        if (File.Exists(tfsFilePath))
                        {
                            _host.Error("Pending add found existing file {0}", tfsFileName);
                            ApplyModified(changeEntry, tfsFilePath, tfsFileName);
                        }
                        else
                        {
                            _host.Verbose("Pending add {0}", tfsFileName);
                            WriteObjectToFile(tfsFilePath, changeEntry.Oid, changeEntry.Path);
                            _workspace.PendAdd(tfsFilePath);
                        }
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
                        ApplyModified(changeEntry, tfsFilePath, tfsFileName);
                        break;
                    default:
                        _host.Error("Unknown change status {0} {1}", changeEntry.Status, changeEntry.Path);
                        return false;
                }
            }

            return true;
        }

        private void ApplyModified(TreeEntryChanges changeEntry, string tfsFilePath, string tfsFileName)
        {
            if (changeEntry.OldOid != changeEntry.Oid)
            {
                _host.Verbose("Pending edit {0}", tfsFileName);
                _workspace.PendEdit(tfsFilePath);
                CopyObjectToFile(tfsFilePath, changeEntry.Oid, changeEntry.Path);
            }
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
