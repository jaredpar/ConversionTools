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
using System.Threading.Tasks;

namespace OneWayMirror.Core
{
    internal sealed class OneWayMirror
    {
        private readonly ILogger _logger;
        private readonly string _workspacePath;
        private readonly Workspace _workspace;
        private readonly Repository _repository;

        internal OneWayMirror(
            Workspace workspace,
            ILogger logger)
        {
            _logger = logger;
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
                _logger.Verbose("Pending Change: {OldPath}({OldOid}:{OldMode}) -> {NewPath}({NewOld}:{NewMode}) {Status}.", changeEntry.OldPath, changeEntry.OldOid, changeEntry.OldMode, changeEntry.Path, changeEntry.Oid, changeEntry.Mode, changeEntry.Status);

                var tfsFilePath = GetTfsWorkspacePath(changeEntry.Path);
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
                        _logger.Error("Unknown change {0}({1}:{2}) -> {3}({4}:{5}) {6}!", changeEntry.OldPath, changeEntry.OldOid, changeEntry.OldMode, changeEntry.Path, changeEntry.Oid, changeEntry.Mode, changeEntry.Status);
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
