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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using Alteridem.GitHub.Annotations;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.Framework.Common;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;

#endregion

namespace Alteridem.Tfs.Model
{
    [Export]
    public class TfsApi : INotifyPropertyChanged
    {
        private TfsProject _selectedProject;
        private TfsWorkItem _issue;
        private QueryDefinition _queryItem;
        private string _searchId, _searchTitle, _searchDescription, _searchAreaPath;
        private string _state;

        private ObservableCollection<TfsQueryItem> _queryItems;
        private readonly WorkItemStore _workItemStore;

        private static readonly string[] fieldsToQuery = new[]
            {
                "[Id]",
                "[Title]",
                "[Milestone]",
                "[Release]",
                "[Description]",
                "[History]",
                "[Assigned To]",
                "[State]",
                "[Resolution]",
                "[Area Path]",
                "[Changed Date]",
                "[Changed By]",
                "[Created By]",
                "[Resolved By]",
                "[Rev]",
            };


        public TfsApi()
        {
            Server = new Uri("http://vstfdevdiv:8080");

            var configurationServer = TfsConfigurationServerFactory.GetConfigurationServer(Server);

            // Get the catalog of team project collections
            ReadOnlyCollection<CatalogNode> collectionNodes = configurationServer.CatalogNode.QueryChildren(
                new[] { CatalogResourceTypes.ProjectCollection },
                false, CatalogQueryOptions.None);

            TfsTeamProjectCollection tpc = null;

            // List the team project collections
            foreach (CatalogNode collectionNode in collectionNodes)
            {
                // Use the InstanceId property to get the team project collection
                Guid collectionId = new Guid(collectionNode.Resource.Properties["InstanceId"]);
                TfsTeamProjectCollection teamProjectCollection = configurationServer.GetTeamProjectCollection(collectionId);
                if (teamProjectCollection.Name.ToLower().EndsWith("devdiv2"))
                {
                    tpc = teamProjectCollection;
                    break;
                }
            }

            if (tpc == null)
            {
                return;
            }

            _workItemStore = tpc.GetService<WorkItemStore>();

            Projects = new BindingList<TfsProject>();
            foreach (Project project in _workItemStore.Projects)
            {
                var tfsProject = new TfsProject(project);
                if (project.Name.Equals("DevDiv"))
                {
                    Projects.Insert(0, tfsProject);
                }
                else
                {
                    Projects.Add(tfsProject);
                }
            }

            _selectedProject = Projects[0];
            _queryItems = TfsQueryItem.CreateTreeView(_selectedProject);
            _state = null;
        }

        [NotNull]
        public Uri Server { get; private set; }

        [NotNull]
        public BindingList<TfsProject> Projects { get; private set; }

        public TfsProject SelectedProject
        {
            get { return _selectedProject; }
            set
            {
                if (Equals(value, _selectedProject)) return;
                _selectedProject = value;
                _queryItems = null;
                OnPropertyChanged();
                OnPropertyChanged("QueryItems");
            }
        }

        public void SetProjectByName(string projectName)
        {
            var project = Projects.SingleOrDefault(p => p.Name == projectName);
            if (project != null)
            {
                SelectedProject = project;
            }
        }

        [NotNull]
        public ObservableCollection<TfsQueryItem> QueryItems
        {
            get
            {
                if (_queryItems == null)
                {
                    _queryItems = TfsQueryItem.CreateTreeView(_selectedProject);
                }

                return _queryItems;
            }
        }

        private static readonly IEnumerable<TfsWorkItem> EmptyIssuesList = new List<TfsWorkItem>();
        /// <summary>
        /// The filtered list of issues
        /// </summary>
        [NotNull]
        public IEnumerable<TfsWorkItem> Issues
        {
            get
            {
                if (SelectedQuery == null)
                {
                    var queryText = "Select " + string.Join(",", fieldsToQuery) +
   " From WorkItems " +
   "Order By [State] Asc, [Changed Date] Desc ";

                    var conditionalClausePrefix = " Where ";
                    var andClausePrefix = @" And ";

                    int searchId;
                    if (!string.IsNullOrWhiteSpace(SearchId) && int.TryParse(SearchId, out searchId))
                    {
                        queryText = queryText + conditionalClausePrefix + "[Id] = " + SearchId;
                        conditionalClausePrefix = andClausePrefix;
                    }
                    else
                    {
                        if (SelectedProject != null)
                        {
                            queryText = queryText + conditionalClausePrefix + " [Team Project] = '" + SelectedProject.Name + "' ";
                            conditionalClausePrefix = andClausePrefix;
                        }

                        if (FilterStateForIssues != null)
                        {
                            queryText = queryText + conditionalClausePrefix + " [State] = '" + FilterStateForIssues + "' ";
                            conditionalClausePrefix = andClausePrefix;
                        }

                        bool hasSearchText = false;

                        if (!string.IsNullOrWhiteSpace(SearchTitle))
                        {
                            hasSearchText = true;
                            queryText = queryText + conditionalClausePrefix + "[Title] Contains '" + SearchTitle + "'";
                            conditionalClausePrefix = andClausePrefix;
                        }

                        if (!string.IsNullOrWhiteSpace(SearchDescription))
                        {
                            hasSearchText = true;
                            queryText = queryText + conditionalClausePrefix + "([Description] Contains '" + SearchDescription + "' Or [Repro Steps] Contains '" + SearchDescription + "' Or [History] Contains '" + SearchDescription + "')";
                            conditionalClausePrefix = andClausePrefix;
                        }

                        if (!string.IsNullOrWhiteSpace(SearchAreaPath))
                        {
                            hasSearchText = true;
                            queryText = queryText + conditionalClausePrefix + "[Area Path] Under '" + SearchAreaPath + "' ";
                            conditionalClausePrefix = andClausePrefix;
                        }

                        if (!hasSearchText)
                        {
                            return EmptyIssuesList;
                        }
                    }                    

                    try
                    {
                        return _workItemStore.Query(queryText).Cast<WorkItem>().Select(w => new TfsWorkItem(w));
                    }
                    catch
                    {
                        return EmptyIssuesList;
                    }
                }

                IEnumerable<TfsWorkItem> issues;
                try
                {
                    var queryText = SelectedQuery.QueryText;
                    queryText = queryText.Replace("@project", "'" + SelectedProject.Name + "'");
                    issues = _workItemStore.Query(queryText).Cast<WorkItem>().Select(w => new TfsWorkItem(w));
                }
                catch
                {
                    return EmptyIssuesList;
                }

                // Search Text
                if (!string.IsNullOrWhiteSpace(SearchId))
                {
                    int searchId;
                    string search = SearchId.ToLower();

                    issues = issues.Where(i => i.Title.ToLower().Contains(search) || i.Description.ToLower().Contains(search) || 
                        (int.TryParse(SearchId, out searchId) && i.Id == searchId));
                }

                return issues;
            }
        }


        /// <summary>
        /// Text to search the issues for
        /// </summary>
        public string SearchId
        {
            get { return _searchId; }
            set
            {
                if (value == _searchId) return;
                _searchId = value;
                OnPropertyChanged();
                OnPropertyChanged("Issues");
            }
        }

        public string FilterStateForIssues
        {
            get { return _state; }
            set
            {
                if (value == _state) return;
                _state = value;
                OnPropertyChanged();
            }
        }

        public string SearchTitle
        {
            get { return _searchTitle; }
            set
            {
                if (value == _searchTitle) return;
                _searchTitle = value;
                OnPropertyChanged();
            }
        }
        
        public string SearchDescription
        {
            get { return _searchDescription; }
            set
            {
                if (value == _searchDescription) return;
                _searchDescription = value;
                OnPropertyChanged();
            }
        }

        public string SearchAreaPath
        {
            get { return _searchAreaPath; }
            set
            {
                if (value == _searchAreaPath) return;
                _searchAreaPath = value;
                OnPropertyChanged();
            }
        }

        public TfsWorkItem SelectedWorkItem
        {
            get { return _issue; }
            set
            {
                if (Equals(value, _issue)) return;
                _issue = value;
                OnPropertyChanged();
            }
        }

        public QueryDefinition SelectedQuery
        {
            get { return _queryItem; }
            set
            {
                if (Equals(value, _queryItem)) return;
                _queryItem = value;
                OnPropertyChanged();
                OnPropertyChanged("Issues");
            }
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CanBeNull, CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}