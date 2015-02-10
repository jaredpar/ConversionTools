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
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

#endregion

namespace Alteridem.Tfs.Model
{
    [Export]
    public class TfsQueryItem
    {
        private readonly ObservableCollection<TfsQueryItem> _childItems;
        
        public TfsQueryItem(QueryItem item)
        {
            Item = item;
            Name = item.Name;
            _childItems = new ObservableCollection<TfsQueryItem>();

            var folder = item as QueryFolder;
            if (folder != null)
            {
                AppendChildItems(folder, _childItems);
            }
        }

        public static ObservableCollection<TfsQueryItem> CreateTreeView(TfsProject project)
        {
            var childItems = new ObservableCollection<TfsQueryItem>();
            AppendChildItems(project.Project.QueryHierarchy, childItems);
            return childItems;
        }

        private static void AppendChildItems(IEnumerable<QueryItem> item, ObservableCollection<TfsQueryItem> childItems)
        {
            foreach (var childGroup in item.GroupBy(i => i is QueryFolder))
            {
                foreach (var child in childGroup.OrderBy(i => i.Name))
                {
                    childItems.Add(new TfsQueryItem(child));
                }
            }
        }

        public QueryItem Item { get; private set; }
        public string Name { get; private set; }
        public ObservableCollection<TfsQueryItem> ChildItems { get { return _childItems; } }

        public override string ToString()
        {
            return Name;
        }
    }
}