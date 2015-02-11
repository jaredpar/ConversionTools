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

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Alteridem.GitHub.Annotations;
using Alteridem.GitHub.Extension.Interfaces;
using Alteridem.GitHub.Extension.View;
using Alteridem.GitHub.Model;
using Alteridem.Tfs.Model;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.VisualStudio.Shell;

using GitIssue = Octokit.Issue;
using NewGitIssue = Octokit.NewIssue;

#endregion

namespace Alteridem.GitHub.Extension.ViewModel
{
    public class TfsWorkItemListViewModel : BaseUserViewModel
    {
        public TfsWorkItemListViewModel()
        {
            RefreshCommand = new RelayCommand(p => Refresh(), p => CanRefresh());
            OpenGitIssueCommand = new RelayCommand(p => OpenGitIssue(), p => CanAddIssue() );
        }

        public ICommand RefreshCommand { get; private set; }
        public ICommand OpenGitIssueCommand { get; private set; }

        public string TfsRepositoryName
        {
            get { return TfsApi.SelectedProject.Name; }
            set { TfsApi.SetProjectByName(value); }
        }

        public RepositoryWrapper GitRepository
        {
            get { return GitHubApi.Repository; }
        }

        public string SearchId
        {
            get { return TfsApi.SearchId; }
            set { TfsApi.SearchId = value; }
        }

        public string SearchTitle
        {
            get { return TfsApi.SearchTitle; }
            set { TfsApi.SearchTitle = value; }
        }

        public string SearchDescription
        {
            get { return TfsApi.SearchDescription; }
            set { TfsApi.SearchDescription = value; }
        }

        public string SearchAreaPath
        {
            get { return TfsApi.SearchAreaPath; }
            set { TfsApi.SearchAreaPath = value; }
        }

        [NotNull]
        public IEnumerable<string> TfsRepositoryNames { get { return TfsApi.Projects.Select(p => p.Name); } }

        public QueryDefinition SelectedQuery
        {
            get { return TfsApi.SelectedQuery; }
            set { TfsApi.SelectedQuery = value; }
        }

        public TfsWorkItem Issue
        {
            get { return TfsApi.SelectedWorkItem; }
            set { TfsApi.SelectedWorkItem = value; }
        }

        [NotNull]
        public IEnumerable<TfsWorkItem> Issues { get { return TfsApi.Issues; } }

        public void OpenIssueViewer()
        {
            //var viewer = ServiceProvider.GlobalProvider.GetService(typeof(ITfsIssueToolWindow)) as TfsIssueToolWindow;
            //if (viewer != null)
            //{
            //    viewer.Show();
            //}

            OpenGitIssue();
        }

        private void Refresh()
        {
            var unused = TfsApi.Issues;
            OnPropertyChanged();
            OnPropertyChanged("Issues");
        }

        private bool CanRefresh()
        {
            return true;
        }

        public void OpenGitIssue()
        {
            var add = Factory.Get<IIssueEditor>();
            add.SetTfsIssue(TfsApi.SelectedWorkItem);
            add.ShowModal();
        }

        public bool CanAddIssue()
        {
            return LoggedIn && GitRepository != null && TfsApi.SelectedWorkItem != null;
        }
    }
}