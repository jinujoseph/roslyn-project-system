using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    internal interface IPropertyProvider
    {
        Task<string> GetEvaluatedPropertyValueAsync(string name);
        Task<string> GetEvaluatedPropertyValueAsync(string schema, string name);
        Task<IProperty> GetPropertyAsync(string name);
        Task SetPropertyValueAsync(string name, object value);
        Task SetPropertyValueAsync(string schema, string name, object value);
        Task DeletePropertyAsync(string schema, string name);
    }
}
