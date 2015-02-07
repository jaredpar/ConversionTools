using LibGit2Sharp;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OneWayMirror.Core
{
    partial class TwoWayMirror : IDisposable
    {
        const string GitMirrorFile = ".gitmirror";
        const string GitMirrorAllFile = ".gitmirrorall";

        private static readonly Regex s_PortedFromTfsRegex = new Regex(@"\[tfs-changeset: (?<changeset>\d+)\]");


        public static readonly TimeSpan FiveMinutes = new TimeSpan(0, 5, 0);

        private readonly Config _config;
        private readonly TfsTeamProjectCollection _tfsServer;
        private readonly VersionControlServer _vcServer;
        private readonly Repository _gitRepo;
        private readonly ObjectDatabase _objectDatabase;
        private readonly Octokit.GitHubClient _gitHub;
        private readonly PushOptions _gitHubPushOptions;
        private readonly string _tfsRoot;
        private readonly string _tfsWorkspacePath;
        private readonly ILogger _logger;

        public TwoWayMirror(Config config)
        {
            _config = config;
            _tfsRoot = config.TfsRoot;
            _tfsWorkspacePath = config.TfsWorkspacePath;

            _tfsServer = new TfsTeamProjectCollection(config.TfsCollection);
            _tfsServer.EnsureAuthenticated();

            _vcServer = _tfsServer.GetService<VersionControlServer>();

            _gitRepo = new Repository(config.GitRepoPath);
            _objectDatabase = _gitRepo.ObjectDatabase;

            _gitHub = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("dotnetTwoWayTFSMirror"));
            _gitHub.Credentials = new Octokit.Credentials(config.GitHubApiKey);
            // _gitHubPushOptions = new PushOptions() { CredentialsProvider = GitHubCredentialsProvider };
        }

        /*
        public void Run()
        {
            CheckAndWaitForExistingPrToComplete().Wait();
            DeleteRemoteAndLocalBranches();

            while (true)
            {
                RunIteration();
            }
        }

        void RunIteration()
        {
            _config.LoadUserMap(_vcServer);

            if (PortFromTfsToGit())
            {
                CreatePullRequestAndWaitTillClose().Wait();
            }

            PortFromGitToTfs();

            _logger.Information("Iteration Complete.  Sleeping");

            Task.Delay(FiveMinutes).Wait();
        }

        */

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                _tfsServer.Dispose();
                _gitRepo.Dispose();
            }
        }

        public static VersionSpec ChangesetVersion(int id)
        {
            return new ChangesetVersionSpec(id);
        }

        ~TwoWayMirror()
        {
            Dispose(false);
        }
    }

    partial class TwoWayMirror
    {
        private const string PullRequestTitle = "Merge changes from TFS";

        /*
        private async Task CheckAndWaitForExistingPrToComplete()
        {
            _logger.Information("Checking for pending Pull Request.");

            IReadOnlyList<PullRequest> openPrs = await _gitHub.PullRequest.GetForRepository(_config.GitHubUpstreamOwner, _config.GitHubProject);

            foreach (PullRequest pr in openPrs)
            {
                if (pr.Title == PullRequestTitle && pr.User.Name == _config.GitHubOriginOwner)
                {
                    await WaitForPullRequestToClose(pr);
                    return;
                }
            }
        }

        private async Task CreatePullRequestAndWaitTillClose()
        {
            _logger.Information("Pushing {PendingBranch} to origin.", _config.GitPendingBranch);

            LibGit2Sharp.Remote origin = _gitRepo.Network.Remotes["origin"];
            string refPath = RefPathForHeadOfBranch(_config.GitPendingBranch);

            _gitRepo.Network.Push(origin, refPath, refPath, _gitHubPushOptions);

            _logger.Information("Submitting Pull Request.");

            NewPullRequest prInfo = new NewPullRequest(PullRequestTitle, _config.GitHubOriginOwner + ":" + _config.GitPendingBranch, _config.GitHubUpstreamDestinationBranch);

            PullRequest pr = await _gitHub.PullRequest.Create(_config.GitHubUpstreamOwner, _config.GitHubProject, prInfo);

            await WaitForPullRequestToClose(pr);
        }

        private async Task WaitForPullRequestToClose(PullRequest pr)
        {
            _logger.Information("Waiting for Pull Request {PullRequestNumber} to be merged.", pr.Number);

            while (pr.State == ItemState.Open)
            {
                _logger.Verbose("Pull Request {PullRequestNumber} is still open.  Sleeping.", pr.Number);

                Task.Delay(FiveMinutes).Wait();

                pr = await _gitHub.PullRequest.Get(_config.GitHubUpstreamOwner, _config.GitHubProject, pr.Number);
            }

            _logger.Information("Pull Request merged.", _config.GitPendingBranch);

            DeleteRemoteAndLocalBranches();
        }

        void DeleteRemoteAndLocalBranches()
        {
            _logger.Verbose("Removing local and origin branch {PendingBranch}.", _config.GitPendingBranch);

            LibGit2Sharp.Branch localBranch = _gitRepo.Branches[_config.GitPendingBranch];

            if (localBranch != null)
            {
                _gitRepo.Branches.Remove(localBranch);
            }

            LibGit2Sharp.Remote origin = _gitRepo.Network.Remotes["origin"];
            string refPath = RefPathForHeadOfBranch(_config.GitPendingBranch);

            _gitRepo.Network.Push(origin, "", refPath, _gitHubPushOptions);
        }

        */
    }

    partial class TwoWayMirror : IDisposable
    {
        private static readonly Regex s_PortedFromGitRegex = new Regex(@"\[git-commit-sha: (?<sha>[a-z0-9]{40})(?<initialcommittag> is-initial-commit)?\]");

        /// <summary>
        /// Prepare to port zero or more changes from TFS into Git.  This function creates a new branch
        /// in a git repository which contains all the changes that need to be ported from TFS to Git.
        /// After is is complete, the new branch must manually be merged into master (e.g. by submitting
        /// a pull request).
        /// </summary>
        /// <remarks>
        /// Here is the algorithm we use to port changes from TFS to Git.
        /// 
        /// 1) Compute the set of changes in TFS that are not in Git.  Logically this is done by walking
        ///    back from the head changeset until we find a TFS commit that is in Git.  There are some
        ///    special cases that allow us to short circuit, which are discussed in GetChangesetsToPort.
        ///    It is important to note that as part of this process, we will find one TFS commit tagged
        ///    with a [git-commit-sha: ABCD...XYZ] tag.  The Sha is is the commit that was last merged
        ///    from Git back into TFS.  We call this the BASE_COMMIT.
        /// 2) For each change in TFS but not in Git:
        ///   a) Create a tree that matches the state of TFS by applying that change to LAST_COMMIT
        ///      (taking into account the .gitmirror and .gitmirrorall files).
        ///   b) Create a new commit object with a parent of LAST_COMMIT, with copying the TFS commit message 
        ///      but adding a [tfs-changeset: CL] tag.  This will let us find this commit in Git.
        ///   c) set LAST_COMMIT to the commit we just created.
        /// 3) Create a branch named "from-tfs" which points at commit LAST_COMMIT.
        /// </remarks>
        /// <returns>
        /// True if a branch was created and pushed to origin.  False if there were no commits to port.
        /// </returns>
        public bool PortFromTfsToGit()
        {
            _logger.Information("Starting TFS -> Git port.");

            UpdateLocalMasterToLatest();

            _logger.Verbose("Computing Changesets to port.");

            IEnumerable<Changeset> history = GetHistorySnapshot();

            PortingData portingData = GetChangesetsToPort(history);

            if (!portingData.NewChanges.Any() && !portingData.MissedChanges.Any())
            {
                _logger.Information("Git is up to date with TFS.");
                return false;
            }

            _logger.Verbose("Porting {NewChangeCount} new changes and {MissedChangeCount} missed changes to {Sha}",
                        portingData.NewChanges.Count(),
                        portingData.MissedChanges.Count(),
                        portingData.Sha.Substring(0, 6));

            // Debug.Assert(!_gitRepo.Branches.Any(b => b.Name == _config.GitPendingBranch), "There is already a branch named {PendingBranch}", _config.GitPendingBranch);

            Commit parentCommit = _gitRepo.Lookup<Commit>(portingData.Sha);

            Debug.Assert(parentCommit != null, "Could not find commit {Sha} in Git Repository!", portingData.Sha);

            parentCommit = PortChanges(portingData.MissedBase, portingData.MissedChanges, parentCommit);
            parentCommit = PortChanges(portingData.NewBase, portingData.NewChanges, parentCommit);

            if (parentCommit.Sha == portingData.Sha)
            {
                _logger.Warning("Replaying all TFS changes caused no new commits.  Perhaps they are all case fixing commits?  Skipping a PR.");
                return false;
            }

            // _gitRepo.CreateBranch(_config.GitPendingBranch, parentCommit);

            // _logger.Information("Created branch {PendingBranch} -> {TargetSha}.", _config.GitPendingBranch, parentCommit.Sha.Substring(0, 6));

            return true;
        }

        private Commit PortChanges(Changeset baseChangeset, IEnumerable<Changeset> changes, Commit parentCommit)
        {
            if (!changes.Any())
            {
                return parentCommit;
            }

            Tree baseTree = BuildTreeFromChangeset(baseChangeset);

            foreach (Changeset c in changes)
            {
                Tree thisTree = BuildTreeFromChangeset(c);

                // Calculate Diff
                TreeChanges treeChanges = _gitRepo.Diff.Compare<TreeChanges>(baseTree, thisTree, compareOptions: new LibGit2Sharp.CompareOptions() { Similarity = SimilarityOptions.Renames });

                // Apply Diff
                TreeDefinition td = TreeDefinition.From(parentCommit);

                foreach (TreeEntryChanges t in treeChanges)
                {
                    switch (t.Status)
                    {
                        case ChangeKind.Added:
                            td.Add(t.Path, _gitRepo.Lookup<Blob>(t.Oid), t.Mode);
                            break;
                        case ChangeKind.Deleted:
                            td.Remove(t.OldPath);
                            break;
                        case ChangeKind.Modified:
                        case ChangeKind.Renamed:
                            td.Remove(t.OldPath);
                            td.Add(t.Path, _gitRepo.Lookup<Blob>(t.Oid), t.Mode);
                            break;
                        default:
                            // Debug.Assert(false, "Unknown change {0}({1}:{2}) -> {3}({4}:{5}) {6}!", t.OldPath, t.OldOid, t.OldMode, t.Path, t.Oid, t.Mode, t.Status);
                            break;
                    }
                }

                Tree newTree = _objectDatabase.CreateTree(td);

                if (newTree != parentCommit.Tree)
                {
                    parentCommit = GetCommitForChangesetAndNewTree(newTree, parentCommit, c);
                }

                baseTree = thisTree;
            }

            return parentCommit;
        }

        private Tree BuildTreeFromChangeset(Changeset baseChangeset)
        {
            Workspace w = _vcServer.GetWorkspace(_tfsWorkspacePath);
            GetStatus status = w.Get(ChangesetVersion(baseChangeset.ChangesetId), GetOptions.NoAutoResolve);

            // Debug.Assert(status.NumConflicts == 0, "Conflicts During Sync: {0}!", status);
            // Debug.Assert(status.NumFailures == 0, "Failures During Sync: {0}!", status);
            // Debug.Assert(status.NumWarnings == 0, "Warnings During Sync: {0}!", status);

            return GitUtils.BuildTreeFromTfsWorkspace(_tfsWorkspacePath, _objectDatabase);

        }

        /// <summary>
        /// Returns the changesets (oldest to newest) which are present in TFS but not in Git as well as the Sha of
        /// the commit we should port these on top of.
        /// </summary>
        private PortingData GetChangesetsToPort(IEnumerable<Changeset> history)
        {
            Commit gitMasterHead = _gitRepo.Refs[RefPathForHeadOfBranch(_config.GitTargetBranch)].ResolveAs<Commit>();

            Stack<Changeset> newChanges = new Stack<Changeset>();
            Stack<Changeset> missedChanges = new Stack<Changeset>();

            Changeset newBase = null;
            Changeset missedBase = null;

            string sha = null;

            Stack<Changeset> curChanges = newChanges;

            foreach (Changeset c in history)
            {
                Match m = s_PortedFromGitRegex.Match(c.Comment);

                if (m.Success)
                {
                    sha = sha ?? m.Groups["sha"].Value;

                    if (curChanges == newChanges)
                    {
                        newBase = c;
                        curChanges = missedChanges;
                    }
                    else
                    {
                        missedBase = c;
                        break;
                    }

                    if (m.Groups["initialcommittag"].Success)
                    {
                        // The tag is of the form [git-commit-sha: ABCD...XYZ is-initial-commit] which is
                        // the special tag that represents the first changeset that was ported from TFS to
                        // Git during the onboarding process. So it is safe to stop our search now.
                        break;
                    }

                    // We don't add this commit to the list to port because it is already in Git, since it
                    // came from Git.
                    continue;
                }

                if (!IsMirrorableChangeset(c))
                {
                    _logger.Verbose("Ignoring {ChangesetId} as it has no mirrorable files.", c.ChangesetId);
                    continue;
                }

                string changesetShaInGit;

                if (IsChangesetReachableFromCommit(c, gitMasterHead, out changesetShaInGit))
                {
                    // If we haven't seen a Git -> TFS commit yet, then we should base the changes were
                    // are going to port on this commit.
                    sha = sha ?? changesetShaInGit;

                    if (curChanges == newChanges)
                    {
                        newBase = c;
                    }
                    else
                    {
                        missedBase = c;
                    }

                    // By construction if this changeset is in Git than all changesets that precede it are 
                    // also in Git.                                       
                    break;
                }

                _logger.Verbose("Need to port {ChangesetId}", c.ChangesetId);

                // We need to mirror it.
                curChanges.Push(c);
            }

            Debug.Assert(sha != null);
            Debug.Assert(newBase != null);
            Debug.Assert(!missedChanges.Any() || missedBase != null);

            return new PortingData
            {
                NewChanges = newChanges,
                NewBase = newBase,
                MissedChanges = missedChanges,
                MissedBase = missedBase,
                Sha = sha
            };
        }

        private bool IsMirrorableChangeset(Changeset candidateChangeset)
        {
            Match m = s_PortedFromGitRegex.Match(candidateChangeset.Comment);

            if (m.Success)
            {
                // If the changeset is marked with the mirrored from git tag, it is not
                // mirrorable.  Since this changeset will never appear in Git.  However,
                // we do consider the initial commit changeset as mirrorable (since it 
                // was mirrored manually.

                return m.Groups["initialcommittag"].Success;
            }

            HashSet<string> tfsUnmirroredPaths = new HashSet<string>();

            VersionSpec thisVersion = ChangesetVersion(candidateChangeset.ChangesetId);

            foreach (Change c in candidateChangeset.Changes.Where(c => c.Item.ItemType == ItemType.File))
            {
                string[] itemComponents = c.Item.ServerItem.Substring(1).Split('/');

                for (int i = itemComponents.Length - 1; i > 0; i--)
                {
                    string candidateTfsRoot = "$" + string.Join("/", itemComponents, 0, i);

                    if (tfsUnmirroredPaths.Contains(candidateTfsRoot))
                    {
                        break;
                    }

                    // We only mirror a file from a .gitmirror file if it's in the same directory.
                    if (i == itemComponents.Length - 1)
                    {
                        string candidateGitMirrorPath = candidateTfsRoot + "/.gitmirror";

                        if (_vcServer.ServerItemExists(candidateGitMirrorPath, thisVersion,
                                                       DeletedState.NonDeleted, ItemType.File))
                        {
                            return true;
                        }
                    }

                    string candidateGitMirrorAllPath = candidateTfsRoot + "/.gitmirrorall";

                    if (_vcServer.ServerItemExists(candidateGitMirrorAllPath, thisVersion,
                                                   DeletedState.NonDeleted, ItemType.File))
                    {
                        return true;
                    }

                    if (candidateTfsRoot.Equals(_tfsRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        tfsUnmirroredPaths.Add(candidateTfsRoot);
                        break;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Construct the Git Commit for a coresponding TFS change and the git tree.
        /// </summary>
        private Commit GetCommitForChangesetAndNewTree(Tree newTree, Commit parent, Changeset tfsChange)
        {
            Signature sig = GetSignitureForChangeset(tfsChange);

            string message = tfsChange.Comment + Environment.NewLine + Environment.NewLine + string.Format("[tfs-changeset: {0}]", tfsChange.ChangesetId);

            Commit c = _objectDatabase.CreateCommit(sig, sig, message, newTree, new Commit[] { parent }, true);

            _logger.Information("{ChangesetId} -> {Sha} ({CommiterName} <{CommiterEmail}>).", tfsChange.ChangesetId, c.Sha.Substring(0, 6), sig.Name, sig.Email);

            return c;
        }

        /// <summary>
        /// Given a TFS Changeset, construct the Signature for the coresponding Git change.  The strategy here
        /// is we have a mapping of TFS Names to Git Name, E-mail Pairs.
        /// 
        /// If the owner of the changeset is not in our mappingm we use the default user instead.
        /// </summary>
        private Signature GetSignitureForChangeset(Changeset c)
        {
            Config.NameEmailPair pair;

            if (_config.UserMapping.TryGetValue(c.Owner, out pair))
            {
                return new Signature(pair.Name, pair.Email, (DateTimeOffset)c.CreationDate);
            }
            else
            {
                return new Signature(_config.GitDefaultUserName, _config.GitDefaultUserEmail, (DateTimeOffset)c.CreationDate);
            }
        }

        private void UpdateLocalMasterToLatest()
        {
            _logger.Verbose("Updating Git from upstream/master.");

            Remote upstream = _gitRepo.Network.Remotes["upstream"];

            /*
            _gitRepo.Network.Fetch(upstream, new FetchOptions() { CredentialsProvider = GitHubCredentialsProvider });
            _gitRepo.Checkout(_gitRepo.Branches["master"]);
            _gitRepo.Merge("upstream/master", new Signature(_config.GitDefaultUserName, _config.GitDefaultUserEmail, DateTimeOffset.Now), new LibGit2Sharp.MergeOptions() { FastForwardStrategy = FastForwardStrategy.FastForwardOnly });
            */
        }

        /// <summary>
        /// Simple Case: [BC] - [NC1] - [NC2]
        /// Race Case: [BC] - [MS1] - [MS2] - [MC] - [NC1] - [NC2]
        /// </summary>
        struct PortingData
        {
            public IEnumerable<Changeset> NewChanges { get; set; }
            public IEnumerable<Changeset> MissedChanges { get; set; }

            public Changeset MissedBase { get; set; }
            public Changeset NewBase { get; set; }

            public string Sha { get; set; }
        }

        private IEnumerable<Changeset> GetHistorySnapshot()
        {
            // Force a specific head, otherwise each time the IEnumerable is iterated, we'll pick up
            // new changes, which we don't want.
            return GetHistorySnapshot(ChangesetVersion(GetHistorySnapshot(null).First().ChangesetId));
        }

        private IEnumerable<Changeset> GetHistorySnapshot(VersionSpec versionEnd)
        {
            QueryHistoryParameters queryParameters = new QueryHistoryParameters(_tfsRoot, RecursionType.Full);
            queryParameters.IncludeChanges = true;
            queryParameters.VersionEnd = versionEnd;

            return _vcServer.QueryHistory(queryParameters);
        }

        private string RefPathForHeadOfBranch(string branchName)
        {
            return string.Format("refs/heads/{0}", branchName);
        }

        private static bool IsChangesetReachableFromCommit(Changeset tfsChangeset, Commit baseCommit)
        {
            string ignore = null;
            return IsChangesetReachableFromCommit(tfsChangeset, baseCommit, out ignore);
        }

        /// <summary>
        /// Given a TFS Changeset, determine if it is present in the graph of commits starting at a base.
        /// </summary>
        /// <remarks>
        /// This is a simple BFS over the graph rooted by <paramref name="baseCommit"/>.  We use the
        /// [tfs-changeset: XXXX] metadata in the commit message to figure out if a git commit coresponds
        /// to a TFS change.
        /// </remarks>
        private static bool IsChangesetReachableFromCommit(Changeset tfsChangeset, Commit baseCommit, out string commitSha)
        {
            Queue<Commit> commitsToWalk = new Queue<Commit>();

            commitsToWalk.Enqueue(baseCommit);

            while (commitsToWalk.Any())
            {
                Commit c = commitsToWalk.Dequeue();

                Match m = s_PortedFromTfsRegex.Match(c.Message);

                if (m.Success)
                {
                    int commitNumber = int.Parse(m.Groups["changeset"].Value, CultureInfo.InvariantCulture);
                    if (commitNumber == tfsChangeset.ChangesetId)
                    {
                        commitSha = c.Sha;
                        return true;
                    }
                }

                foreach (Commit toEnqueue in c.Parents)
                {
                    commitsToWalk.Enqueue(toEnqueue);
                }
            }

            commitSha = null;
            return false;
        }

    }
}
