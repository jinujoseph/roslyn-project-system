using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    public interface IArgument
    {
        bool IsRequired { get; }
        string PropertyName { get; }
        string Separator { get; }
    }
}
