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
using System.Windows.Input;
using Alteridem.GitHub.Extension.Interfaces;
using Alteridem.GitHub.Extension.View;
using Alteridem.Tfs.Model;

#endregion

namespace Alteridem.GitHub.Extension.ViewModel
{
    public class TfsWorkItemViewModel : BaseGitHubViewModel
    {
        private ICommand _addCommentCommand;
        private ICommand _editIssueCommand;
        //private ICommand _createIssueCommand;

        public TfsWorkItemViewModel()
        {
            AddCommentCommand = new RelayCommand(p => AddComment(), p => IssueIsNotNull());
            EditIssueCommand = new RelayCommand(p => EditIssue(), p => CanEditIssue());
            // CreateIssueCommand = new RelayCommand(p => EditIssue(), p => CanEditIssue());
        }

        public TfsWorkItem Issue {
            get { return TfsApi.SelectedWorkItem; }
            set { TfsApi.SelectedWorkItem = value; }
        }

        public int Id { get { return TfsApi.SelectedWorkItem.Id; } }

        public string Title { get { return TfsApi.SelectedWorkItem.Title; } }

        public DateTime ChangedDate { get { return TfsApi.SelectedWorkItem.ChangedDate; } }

        public string CreatedBy { get { return TfsApi.SelectedWorkItem.CreatedBy; } }

        public string ChangedBy { get { return TfsApi.SelectedWorkItem.ChangedBy; } }

        public string AssignedTo { get { return TfsApi.SelectedWorkItem.AssignedTo; } }

        public int Rev { get { return TfsApi.SelectedWorkItem.Rev; } }

        public string State { get { return TfsApi.SelectedWorkItem.State; } }

        public string Milestone { get { return TfsApi.SelectedWorkItem.Milestone; } }

        public string Release { get { return TfsApi.SelectedWorkItem.Release; } }

        public string Body { get { return TfsApi.SelectedWorkItem.Body; } }

        public ICommand AddCommentCommand
        {
            get { return _addCommentCommand; }
            private set
            {
                if (Equals(value, _addCommentCommand)) return;
                _addCommentCommand = value;
                OnPropertyChanged();
            }
        }

        public ICommand EditIssueCommand
        {
            get { return _editIssueCommand; }
            set
            {
                if (Equals(value, _editIssueCommand)) return;
                _editIssueCommand = value;
                OnPropertyChanged();
            }
        }

        public void AddComment()
        {
            var dlg = Factory.Get<IAddComment>();
            dlg.ShowModal();
        }

        public bool IssueIsNotNull()
        {
            return TfsApi.SelectedWorkItem != null;
        }

        public void EditIssue()
        {
            var dlg = Factory.Get<IIssueEditor>();
            dlg.SetIssue(GitHubApi.Issue);
            dlg.ShowModal();
        }

        private bool GitHubEnabled()
        {
            return GitHubApi.Repository != null;
        }
        public bool CanEditIssue()
        {
            return TfsApi.SelectedWorkItem != null && GitHubEnabled();
        }

        public bool CanCreateIssue()
        {
            return TfsApi.SelectedWorkItem != null && GitHubEnabled();
        }

        //public void OpenOnGitHub()
        //{
        //    var url = GitHubApi.Issue != null ? GitHubApi.Issue.HtmlUrl : null;
        //    if (url != null)
        //    {
        //        try
        //        {
        //            Process.Start(url.ToString());
        //        }
        //        catch (Exception e)
        //        {
        //            _log.Write(LogLevel.Error, "Failed to open issue.", e);
        //        }
        //    }
        //}
    }
}