using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    public interface IPageMetadata
    {
        bool HasConfigurationCondition { get; }
        string Name { get; }
        Guid PageGuid { get; }
        int PageOrder { get; }
    }
}