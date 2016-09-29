using Microsoft.VisualStudio.ProjectSystem.DotNet.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    public interface IValueEditor
    {
        string DisplayName { get; }
        string EditorType { get; }
        //IList<NameValuePair> Metadata { get; }
    }
}
