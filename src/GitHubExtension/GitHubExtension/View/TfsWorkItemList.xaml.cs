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

using System.Windows.Controls;
using System.Windows.Input;
using Alteridem.GitHub.Extension.ViewModel;
using Alteridem.Tfs.Model;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

#endregion

namespace Alteridem.GitHub.Extension.View
{
    /// <summary>
    /// Interaction logic for IssueList.xaml
    /// </summary>
    public partial class TfsWorkItemList
    {
        private TfsWorkItemListViewModel _viewModel;

        public TfsWorkItemList()
        {
            InitializeComponent();
            _viewModel = Factory.Get<TfsWorkItemListViewModel>();
            DataContext = _viewModel;

            if (radioButtonActive.IsChecked.GetValueOrDefault())
            {
                _viewModel.TfsApi.FilterStateForIssues = radioButtonActive.Content.ToString();
            }
            else if (radioButtonResolved.IsChecked.GetValueOrDefault())
            {
                _viewModel.TfsApi.FilterStateForIssues = radioButtonResolved.Content.ToString();
            }
            else if (radioButtonClosed.IsChecked.GetValueOrDefault())
            {
                _viewModel.TfsApi.FilterStateForIssues = radioButtonClosed.Content.ToString();
            }
        }

        private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // I am too lazy to create a dependency property for the datagrid
            // so that I can bind to the mouse double click :)
            _viewModel.OpenIssueViewer();
        }

        private void OnSelectedItemChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _viewModel.TfsApi.SetProjectByName(ProjectComboBox.SelectedValue.ToString());
        }

        private void RadioButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                var state = ((RadioButton)sender).Content.ToString();
                _viewModel.TfsApi.FilterStateForIssues = state;
            }
        }
    }
}
