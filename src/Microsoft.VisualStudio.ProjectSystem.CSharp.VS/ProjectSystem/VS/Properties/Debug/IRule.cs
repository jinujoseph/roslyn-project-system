using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    public interface IRule
    {
        IPropertyGroup this[string categoryName] { get; }

        IReadOnlyList<ICategory> Categories { get; }
        IProjectPropertiesContext Context { get; }
        string Description { get; }
        string DisplayName { get; }
        string File { get; }
        string HelpString { get; }
        string ItemName { get; }
        string ItemType { get; }
        string Name { get; }
        int Order { get; }
        string PageTemplate { get; }
        IEnumerable<IProperty> Properties { get; }
        IReadOnlyList<IPropertyGroup> PropertyGroups { get; }
        bool PropertyPagesHidden { get; }
        // Rule Schema { get; } //commented by jinu 
        string Separator { get; }
        string SwitchPrefix { get; }

        IProperty GetProperty(string propertyName);
        Task<string> GetPropertyValueAsync(string propertyName);
    }
}
