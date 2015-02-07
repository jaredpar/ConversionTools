using Microsoft.TeamFoundation.VersionControl.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OneWayMirror.Core
{
    internal sealed class Config
    {
        private Config()
        {

        }

        public Uri TfsCollection
        {
            get;
            private set;
        }

        public string TfsRoot
        {
            get;
            private set;
        }

        public string TfsWorkspacePath
        {
            get;
            private set;
        }

        public int TfsBaseChangeset
        {
            get;
            private set;
        }

        public string GitRepoPath
        {
            get;
            private set;
        }

        public string GitTargetBranch
        {
            get;
            private set;
        }

        public string GitPendingBranch
        {
            get;
            private set;
        }

        public string GitDefaultUserName
        {
            get;
            private set;
        }

        public string GitDefaultUserEmail
        {
            get;
            private set;
        }

        public string GitHubProject
        {
            get;
            private set;
        }

        public string GitHubUpstreamOwner
        {
            get;
            private set;
        }

        public string GitHubOriginOwner
        {
            get;
            private set;
        }

        public string GitHubUpstreamDestinationBranch
        {
            get;
            private set;
        }

        public string GitHubApiKey
        {
            get;
            private set;
        }

        public bool Verbose
        {
            get;
            private set;
        }

        public bool SubmitPullRequest
        {
            get;
            private set;
        }

        public bool CheckInToTfs
        {
            get;
            private set;
        }

        public string LogRoot
        {
            get;
            private set;
        }

        private string UserMapFilePath
        {
            get;
            set;
        }

        public Dictionary<string, NameEmailPair> UserMapping
        {
            get;
            private set;
        }

        public static Config LoadConfig(bool forceVerbose)
        {
            /*
            FileIniDataParser parser = new FileIniDataParser();
            IniData data = parser.ReadFile("config.ini");

            Config c = new Config();
            c.TfsCollection = new Uri(data["tfs"]["collection"]);
            c.TfsRoot = data["tfs"]["root"];
            c.TfsWorkspacePath = data["tfs"]["workspace"];
            c.TfsBaseChangeset = int.Parse(data["tfs"]["basechangeset"]);
            c.GitRepoPath = AppendDotGitIfNeeded(data["git"]["repo"]);
            c.GitTargetBranch = data["git"]["targetbranch"];
            c.GitPendingBranch = data["git"]["pendingbranch"];
            c.GitDefaultUserName = data["git"]["username"];
            c.GitDefaultUserEmail = data["git"]["useremail"];
            c.GitHubProject = data["github"]["project"];
            c.GitHubUpstreamOwner = data["github"]["upstream"];
            c.GitHubOriginOwner = data["github"]["origin"];
            c.GitHubUpstreamDestinationBranch = data["github"]["upstreamdestinationbranch"];
            c.GitHubApiKey = data["github"]["apikey"];
            c.Verbose = (forceVerbose || bool.Parse(data["config"]["verbose"]));
            c.SubmitPullRequest = bool.Parse(data["config"]["submitpr"]);
            c.CheckInToTfs = bool.Parse(data["config"]["checkintotfs"]);
            c.LogRoot = data["config"]["logroot"];

            c.UserMapFilePath = data["config"]["usermap"];

            c.Validate();

            return c;
            */
            return null; 
        }

        public void LoadUserMap(VersionControlServer vcServer)
        {
            UserMapping = ParseMappingFromFile(UserMapFilePath, vcServer);
        }

        private static Dictionary<string, NameEmailPair> ParseMappingFromFile(string path, VersionControlServer vcServer)
        {
            Dictionary<string, NameEmailPair> mapping = new Dictionary<string, NameEmailPair>();

            string[] mapFileLines = null;

            if (path.StartsWith("$"))
            {
                string tempFile = Path.GetTempFileName();
                vcServer.DownloadFile(path, tempFile);
                mapFileLines = File.ReadAllLines(tempFile);
                File.Delete(tempFile);
            }
            else
            {
                mapFileLines = File.ReadAllLines("path");
            }

            foreach (string line in mapFileLines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                {
                    continue;
                }

                string[] splitLine = line.Split(';');

                Release.Assert(!mapping.ContainsKey(splitLine[0]), "Name '{0}' already present in mapping!");

                mapping[splitLine[0]] = new NameEmailPair(splitLine[1], splitLine[2]);
            }

            return mapping;
        }

        private static string AppendDotGitIfNeeded(string s)
        {
            if (!s.EndsWith(@"\.git", StringComparison.OrdinalIgnoreCase))
            {
                s = s + @"\.git";
            }

            return s;
        }

        private void Validate()
        {
            Release.Assert(!string.IsNullOrEmpty(TfsRoot));
            Release.Assert(!string.IsNullOrEmpty(TfsWorkspacePath));
            Release.Assert(!string.IsNullOrEmpty(GitRepoPath));
            Release.Assert(!string.IsNullOrEmpty(GitTargetBranch));
            Release.Assert(!string.IsNullOrEmpty(GitPendingBranch));
            Release.Assert(!string.IsNullOrEmpty(GitDefaultUserName));
            Release.Assert(!string.IsNullOrEmpty(GitDefaultUserEmail));
            Release.Assert(!string.IsNullOrEmpty(GitHubProject));
            Release.Assert(!string.IsNullOrEmpty(GitHubUpstreamOwner));
            Release.Assert(!string.IsNullOrEmpty(GitHubOriginOwner));
            Release.Assert(!string.IsNullOrEmpty(GitHubUpstreamDestinationBranch));
            Release.Assert(!string.IsNullOrEmpty(GitHubApiKey));
            Release.Assert(!string.IsNullOrEmpty(LogRoot));
            Release.Assert(!string.IsNullOrEmpty(UserMapFilePath));
        }

        public struct NameEmailPair
        {
            public NameEmailPair(string name, string email)
                : this()
            {
                Name = name;
                Email = email;
            }

            public string Name
            {
                get;
                private set;
            }

            public string Email
            {
                get;
                private set;
            }
        }
    }

}
