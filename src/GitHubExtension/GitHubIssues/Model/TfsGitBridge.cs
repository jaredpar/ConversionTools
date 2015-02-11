// **********************************************************************************
// The MIT License (MIT)
// 
// Copyright (c) 2014 Rob Prouse
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// 
// **********************************************************************************

#region Using Directives

using System;
using System.ComponentModel.Composition;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System.Collections.Generic;
using System.IO;

#endregion

namespace Alteridem.Tfs.Model
{
    [Export]
    public static class TfsGitBridge
    {
        private static readonly string _unknownGitMilestone = "Unknown";

        private static readonly string _milestoneFileName = "MapTfsToGitMilestone.txt";
        private static Dictionary<string, string> TfsToGitMilestoneMap
        {
            get
            {
                return ReadStringToStringDictionary(_milestoneFileName);
            }
        }

        private static readonly string _loginMapFileName = "MapTfsToGitLoginName.txt";
        private static Dictionary<string, string> LoginMap
        {
            get
            {
                return ReadStringToStringDictionary(_loginMapFileName);
            }
        }

        private static readonly string _areaPathToLabelFileName = "MapTfsAreaPathToGitLabel.txt";
        private static Dictionary<string, string> TfsAreaPathToGitLabelMap
        {
            get
            {
                return ReadStringToStringDictionary(_areaPathToLabelFileName);
            }
        }

        private static Dictionary<string, string> ReadStringToStringDictionary(string fileName)
        {
            var map = new Dictionary<string, string>();
            try
            {
                var location = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var path = Path.GetDirectoryName(location);
                var filePath = Path.Combine(path, fileName);
                var lines = File.ReadAllLines(filePath);
                foreach (var line in lines)
                {
                    var parts = line.Split(',');
                    if (parts.Length == 2)
                    {
                        var key = parts[0].TrimStart(' ');
                        var value = parts[1].TrimStart(' ');
                        map.Add(key, value);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ShowMessageBox != null)
                {
                    ShowMessageBox(ex.GetType() + " : " + ex.Message);
                }
            }

            return map;
        }

        public static Action<string> ShowMessageBox { get; set; }

        public static string MapTfsUserNameToGitUserName(string tfsUserName)
        {
            string gitLogin;
            if (LoginMap.TryGetValue(tfsUserName, out gitLogin))
            {
                return gitLogin;
            }

            return null;
        }

        public static string MapTfsMilestoneToGitMilestone(string tfsMilestone)
        {
            string gitMilestone;
            if (TfsToGitMilestoneMap.TryGetValue(tfsMilestone, out gitMilestone))
            {
                return gitMilestone;
            }

            return _unknownGitMilestone;
        }

        public static IEnumerable<string> GetGitLabels(TfsWorkItem issue)
        {
            if (issue.Item.Type != null)
            {
                if (issue.Item.Type.Name.Equals("Bug"))
                {
                    yield return "Bug";
                }
                else if (issue.Item.Type.Name.Equals("User Story"))
                {
                    yield return "Backlog";
                }
            }

            var areaPath = issue.AreaPath;
            if (areaPath != null)
            {
                var foundLabel = false;
                foreach (var kvp in TfsAreaPathToGitLabelMap)
                {
                    if (areaPath.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        foundLabel = true;
                        yield return kvp.Value;
                    }
                }

                if (!foundLabel)
                {
                    yield return "Area-External";
                }
            }

            var state = issue.State;
            var resolution = issue.Resolution;
            if (state != null && state == "Resolved" && resolution != null)
            {
                switch (resolution)
                {
                    case "By Design":
                        yield return "Resolution-By Design";
                        break;

                    case "Duplicate":
                        yield return "Resolution-Duplicate";
                        break;

                    case "External":
                        yield return "Resolution-External";
                        break;

                    case "Not Repro":
                        yield return "Resolution-Not Reproducible";
                        break;

                    case "Won't Fix":
                        yield return "Resolution-Won't Fix";
                        break;

                    default:
                        yield return "Resolution-Not Applicable";
                        break;
                }
            }
        }
    }
}