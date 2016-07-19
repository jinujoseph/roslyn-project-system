// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.Properties
{
    [ExportInterceptingPropertyValueProvider("GenerateSerializationAssemblies")]
    internal sealed class GenerateSerializationAssembliesValueProvider : InterceptingPropertyValueProviderBase
    {
        public override Task<string> OnSetPropertyValueAsync(string unevaluatedPropertyValue, IProjectProperties defaultProperties, IReadOnlyDictionary<string, string> dimensionalConditions = null)
        {
            string generateSerializationAssemblies= unevaluatedPropertyValue;
           if (!GenerateSerializationAssemblieOption.IsDefined(typeof(GenerateSerializationAssemblieOption), unevaluatedPropertyValue))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.enum_out_of_range, "GenerateSerializationAssemblies"));
            }
            return Task.FromResult(generateSerializationAssemblies);
        }
        private enum GenerateSerializationAssemblieOption
        {
            Auto,
            On,
            Off
        };
    }
}