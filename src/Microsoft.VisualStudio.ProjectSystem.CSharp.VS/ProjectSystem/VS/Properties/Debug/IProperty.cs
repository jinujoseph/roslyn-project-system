using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    public interface IProperty
    {
        IList<IArgument> Arguments { get; }
        string Category { get; }
        IRule ContainingRule { get; }
        IProjectPropertiesContext Context { get; }
        IDataSource DataSource { get; }
        string DefaultValue { get; }
        string Description { get; }
        string DisplayName { get; }
        string F1Keyword { get; }
        int HelpContext { get; }
        string HelpFile { get; }
        string HelpUrl { get; }
        bool IncludeInCommandLine { get; }
        bool IsReadOnly { get; }
        //IList<NameValuePair> Metadata { get; }
        string Name { get; }
        string Separator { get; }
        string Subcategory { get; }
        string Switch { get; }
        string SwitchPrefix { get; }
        ReadOnlyCollection<IValueEditor> ValueEditors { get; }
        bool Visible { get; }

        Task DeleteAsync();
        Task<string> GetDisplayValueAsync();
        Task<object> GetValueAsync();
        Task<bool> IsDefinedInContextAsync();
        Task<IsValidValueResult> IsValidValueAsync(object userSuppliedValue);
        Task SetValueAsync(object value);
    }
}
