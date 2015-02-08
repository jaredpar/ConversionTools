using System;
using System.IO;
using System.Linq;

using LibGit2Sharp;

namespace OneWayMirror.Core
{
    internal static class GitUtils
    {
        internal const string GitMirrorFile = ".gitmirror";
        internal const string GitMirrorAllFile = ".gitmirrorall";

        /// <summary>
        /// Should we mirror part of this tree (i.e. does it have a .gitmirror file in the root?)
        /// </summary>
        public static bool ShouldMirrorPartial(this Tree t)
        {
            return t.Any(e => e.TargetType == TreeEntryTargetType.Blob &&
                         e.Name.Equals(GitMirrorFile, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Should we mirror all of this tree (i.e. does it have a .gitmirrorall file in the root?)
        /// </summary>
        public static bool ShouldMirrorFull(this Tree t)
        {
            return t.Any(e => e.TargetType == TreeEntryTargetType.Blob &&
                         e.Name.Equals(GitMirrorAllFile, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Should we not mirror this tree (i.e. it doesn't have a .gitmirror or .gitmirrorall file in the root).
        /// </summary>
        public static bool ShouldExclude(this Tree t)
        {
            return !ShouldMirrorPartial(t) && !ShouldMirrorFull(t);
        }

        public static T ResolveAs<T>(this Reference r) where T : GitObject
        {
            return (T)r.ResolveToDirectReference().Target;
        }
        
        /// <summary>
        /// Takes an existing tree and removes all the folders not included via the .gitmirror/.gitmirrorall
        /// files. Creates a new tree in the object database and returns it.
        /// </summary>
        /// <remarks>
        /// We assume the layout of the .gitmirror/.gitmirrorall files in the input tree is correct.  e.g. the
        /// root of the tree has a .gitmirror or .gitmirrorall file and the sub-trees are also laid out correctly.
        /// We stop processing a subtree as soon as we see a .gitmirrorall directory.
        /// </remarks>
        public static Tree RewriteTree(Tree sourceTree, ObjectDatabase objectDatabase)
        {
            if (sourceTree.ShouldMirrorFull())
            {
                return sourceTree;
            }

            if (sourceTree.ShouldMirrorPartial())
            {
                TreeDefinition td = TreeDefinition.From(sourceTree);

                foreach (TreeEntry entry in sourceTree)
                {
                    if (entry.TargetType != TreeEntryTargetType.Tree)
                    {
                        continue;
                    }

                    Tree subtree = (Tree)entry.Target;
                    td.Remove(entry.Name);

                    if (!subtree.ShouldExclude())
                    {
                        td.Add(entry.Name, RewriteTree(subtree, objectDatabase));
                    }
                }

                return objectDatabase.CreateTree(td);
            }

            Release.Fail("Tree {0} to rewrite did not contain .gitmirror or .gitmirrorall", sourceTree.Sha);
            return null;
        }

        /// <summary>
        /// Construct a Git Tree that represents the on disk version of the TFS workspace, eliding files
        /// that are not mirrored via .gitmirror and .gitmirrorall
        /// </summary>
        public static Tree BuildTreeFromTfsWorkspaceLegacy(string tfsWorkspacePath, ObjectDatabase objectDatabase)
        {
            bool hasGitMirror = File.Exists(Path.Combine(tfsWorkspacePath, GitMirrorFile));
            bool hasGitMirrorAll = File.Exists(Path.Combine(tfsWorkspacePath, GitMirrorAllFile));

            Release.Assert(hasGitMirror || hasGitMirrorAll, "hasGitMirror || hasGitMirrorAll");

            Func<string, string> tfsToGitPath = (string tfsPath) =>
            {
                Release.Assert(!tfsPath.EndsWith(@"\"), "TFS Path Should not have trailing slash.");

                return tfsPath.Substring(tfsWorkspacePath.Length + 1).Replace('\\', '/');
            };

            TreeDefinition td = new TreeDefinition();

            AddIncludedItemsToTreeDefinitionLegacy(td, objectDatabase, tfsToGitPath, tfsWorkspacePath, hasGitMirrorAll);

            return objectDatabase.CreateTree(td);
        }

        private static void AddIncludedItemsToTreeDefinitionLegacy(TreeDefinition td, ObjectDatabase objectDatabase, Func<string, string> tfsToGitPath, string directory, bool inGitMirrorAll)
        {
            bool hasGitMirror = File.Exists(Path.Combine(directory, GitMirrorFile));
            bool hasGitMirrorAll = File.Exists(Path.Combine(directory, GitMirrorAllFile));

            if (!inGitMirrorAll && !hasGitMirror && !hasGitMirrorAll)
            {
                return;
            }

            foreach (string filePath in Directory.GetFiles(directory))
            {
                string gitPath = tfsToGitPath(filePath);
                using (FileStream fs = File.OpenRead(filePath))
                {
                    td.Add(gitPath, objectDatabase.CreateBlob(fs, gitPath), GetFileModeForPath(filePath));
                }
            }

            foreach (string directoryPath in Directory.GetDirectories(directory))
            {
                AddIncludedItemsToTreeDefinitionLegacy(td, objectDatabase, tfsToGitPath, directoryPath, inGitMirrorAll || hasGitMirrorAll);
            }
        }

        // TODO: This is a bit of a hack.  I think we should use something like .tpattributes in TFS to control this.
        private static Mode GetFileModeForPath(string filePath)
        {
            return filePath.EndsWith(".sh", StringComparison.OrdinalIgnoreCase) ? Mode.ExecutableFile : Mode.NonExecutableFile;
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
    }
}
