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
using Octokit;

#endregion

namespace Alteridem.Tfs.Model
{
    [Export]
    public class TfsWorkItem
    {
        public TfsWorkItem(WorkItem item)
        {
            Item = item;
        }

        public WorkItem Item { get; private set; }

        public int Id { get { return Item.Id; } }

        public DateTime ChangedDate { get { return Item.ChangedDate; } }
        public DateTime CreatedDate { get { return Item.CreatedDate; } }

        public int Rev { get { return Item.Rev; } }

        public string Title { get { return Item.Title; } }
        public string Description { get { return Item.Description; } }
        public string AreaPath { get { return Item.AreaPath; } }

        public string ChangedBy { get { return Item.ChangedBy; } }
        public string CreatedBy { get { return Item.CreatedBy; } }
        public string AssignedTo
        {
            get { return Item.Fields[CoreFieldReferenceNames.AssignedTo].Value.ToString(); }
            private set { Item.Fields[CoreFieldReferenceNames.AssignedTo].Value = value; }
        }

        public string State { get { return Item.State; } set { Item.State = value; } }

        public string Resolution
        {
            get { return Item.Fields.Contains("Resolution") ? Item.Fields["Resolution"].Value.ToString() : null; }
            set { if (Item.Fields.Contains("Resolution")) Item.Fields["Resolution"].Value = value; }
        }

        public string DuplicateBugID
        {
            get { return Item.Fields.Contains("Microsoft.DevDiv.DuplicateBugID") ?
                    Item.Fields["Microsoft.DevDiv.DuplicateBugID"].Value.ToString() :
                    null; }
            set { if (Item.Fields.Contains("Microsoft.DevDiv.DuplicateBugID")) Item.Fields["Microsoft.DevDiv.DuplicateBugID"].Value = value; }
        }

        public string Milestone { get { return Item.Fields["Milestone"].Value.ToString(); } }
        public string Release { get { return Item.Fields["Release"].Value.ToString(); } }
        
        public string Body {
            get
            {
                var sectionSeparator = "\n\n------------------------------";
                var tfsUri = "Ported from TFS WorkItem: <b>" + Item.Id.ToString() + "</b>" + sectionSeparator;
                string reproSteps = string.Empty;
                if (Item.Type.Name.Equals("Bug"))
                {
                    reproSteps = "\n\n<p><b>Repro Steps:</b></p>\n" + Item.Fields["Repro Steps"].Value + sectionSeparator;
                }
                else if (Item.Type.Name.Equals("User Story") && !string.IsNullOrEmpty(Description))
                {
                    reproSteps = "\n\n<p><b>Description:</b></p>\n" + Description + sectionSeparator;
                }

                var history = "\n\n\n<p><b>Revisions:</b></p>\n";
                int index = 1;
                foreach (Revision revision in Item.Revisions)
                {
                    var revHistory = revision.Fields["History"].Value.ToString();
                    if (index == 1 || !string.IsNullOrEmpty(revHistory))
                    {
                        var tagLine = (index == 1 ? "Created By " : "Edited By ") + 
                            revision.Fields["System.ChangedBy"].Value.ToString();
                        var revisionStr = "\n\n" + index + ") " + tagLine + " (" + revision.Fields["System.ChangedDate"].Value.ToString() + ")\n";
                        revisionStr += "\n" + revHistory + sectionSeparator;
                        history += revisionStr;
                        index++;
                    }
                }
                
                return tfsUri + reproSteps + history;
            }
        }

        public override string ToString()
        {
            return Item.ToString();
        }

        public void Resolve(Issue portedGitHubIssue)
        {
            Item.History = "Ported to GitHub: " + portedGitHubIssue.HtmlUrl;
            
            if (Item.Type.Name.Equals("Bug"))
            {
                if (State == "Active")
                {
                    State = "Resolved";
                    AssignedTo = CreatedBy;
                    Resolution = "Migrated to VSO";
                    DuplicateBugID = portedGitHubIssue.HtmlUrl.ToString();
                }
            }
            else if (Item.Type.Name.Equals("User Story"))
            {
                if (State != "Completed")
                {
                    State = "Completed";
                }
            }

            Item.Save();
        }
    }
}