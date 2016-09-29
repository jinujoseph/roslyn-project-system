using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    public interface IProjectPropertiesContext
    {
        string File { get; }
        bool IsProjectFile { get; }
        string ItemName { get; }
        string ItemType { get; }
    }
}
