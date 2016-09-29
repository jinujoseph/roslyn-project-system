using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem.DotNet.Utilities;

namespace Microsoft.VisualStudio.ProjectSystem.DotNet.Utilities
{
    internal static class ObservableListExtensions
    {
        public static ObservableList<NameValuePair> CreateList(this IDictionary<string, string> dictionary)
        {
            ObservableList<NameValuePair> list = new ObservableList<NameValuePair>();
            foreach (var kvp in dictionary)
            {
                list.Add(new NameValuePair(kvp.Key, kvp.Value, list));
            }
            return list;
        }

        public static IDictionary<string, string> CreateDictionary(this ObservableList<NameValuePair> list)
        {
            var dictionary = new Dictionary<string, string>();
            foreach (var ev in list)
            {
                dictionary.Add(ev.Name, ev.Value);
            }
            return dictionary;
        }
    }
}
