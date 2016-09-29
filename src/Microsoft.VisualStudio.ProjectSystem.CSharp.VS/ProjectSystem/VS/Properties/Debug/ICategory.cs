using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    public interface ICategory
    {
        string Description { get; }
        string DisplayName { get; }
        string HelpString { get; }
        string Name { get; }
        int Order { get; }
        object Schema { get; }
        string Subtype { get; }
    }
}
