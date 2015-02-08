using System;
using System.IO;
using System.Linq;

using LibGit2Sharp;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace OneWayMirror.Core
{
    public static class GitUtils
    {
        public static T ResolveAs<T>(this Reference r) where T : GitObject
        {
            return (T)r.ResolveToDirectReference().Target;
        }
        
        /// <summary>
        /// This creates a Tree object from the local file system.  
        /// 
        /// Note: This is not the most robust method of building a Tree.  The most reliably way would 
        /// be to query the set of items in the Workspace object.  That is not subject to change through
        /// local operations like building. 
        /// </summary>
        public static Tree CreateTreeFromDirectory(string path, ObjectDatabase objectDatabase)
        {
            Func<string, string> convertToGitPath = (string filePath) =>
            {
                Release.Assert(!filePath.EndsWith(@"\"), "File path should not have a trailing slash");

                var gitPath = filePath.Substring(path.Length + 1);
                gitPath = gitPath.Replace('\\', '/');
                return gitPath;
            };

            var treeDefinition = new TreeDefinition();
            treeDefinition = CreateTreeFromDirectoryCore(path, objectDatabase, treeDefinition, convertToGitPath);
            return objectDatabase.CreateTree(treeDefinition);
        }

        private static TreeDefinition CreateTreeFromDirectoryCore(string path, ObjectDatabase objectDatabase, TreeDefinition treeDefinition, Func<string, string> convertToGitPath)
        {
            foreach (string filePath in Directory.GetFiles(path))
            {
                var gitPath = convertToGitPath(filePath);
                using (var fs = File.OpenRead(filePath))
                {
                    var blob = objectDatabase.CreateBlob(fs, gitPath);
                    var mode = GetFileModeForPath(filePath);
                    treeDefinition = treeDefinition.Add(gitPath, blob, mode);
                }
            }

            foreach (string subPath in Directory.GetDirectories(path))
            {
                treeDefinition = CreateTreeFromDirectoryCore(subPath, objectDatabase, treeDefinition, convertToGitPath);
            }

            return treeDefinition;
        }

        /// <summary>
        /// Creates a TreeDefinition from the state of the Workspace object.  
        /// </summary>
        public static Tree CreateTreeFromWorkspace(Workspace workspace, string workspacePath, ObjectDatabase objectDatabase)
        {
            var treeDefinition = new TreeDefinition();
            var itemSpec = new ItemSpec(workspacePath, RecursionType.Full);
            var allItems = workspace.GetItems(new[] { itemSpec }, DeletedState.NonDeleted, ItemType.File, generateDownloadUrls: false, getItemsOptions: GetItemsOptions.LocalOnly);
            Release.Assert(allItems.Length == 1);

            foreach (var item in allItems[0].Items)
            {
                var filePath = item.LocalItem;
                var gitPath = filePath.Substring(workspacePath.Length + 1);
                gitPath = gitPath.Replace('\\', '/');

                using (var fs = File.OpenRead(filePath))
                {
                    var blob = objectDatabase.CreateBlob(fs, gitPath);
                    var mode = GetFileModeForPath(filePath);
                    treeDefinition = treeDefinition.Add(gitPath, blob, mode);
                }
            }

            return objectDatabase.CreateTree(treeDefinition);
        }

        // TODO: This is a bit of a hack.  I think we should use something like .tpattributes in TFS to control this.
        private static Mode GetFileModeForPath(string filePath)
        {
            return filePath.EndsWith(".sh", StringComparison.OrdinalIgnoreCase) ? Mode.ExecutableFile : Mode.NonExecutableFile;
        }

    }
}
