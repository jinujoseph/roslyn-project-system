using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    public interface IDataSource
    {
        bool HasConfigurationCondition { get; }
        string ItemType { get; }
        string Label { get; }
        string PersistedName { get; }
        string Persistence { get; }
        DefaultValueSourceLocation SourceOfDefaultValue { get; }
        string SourceType { get; }

        Task<string> GetPersistedFileAsync();
    }
}
