// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.Properties
{
    [ExportInterceptingPropertyValueProvider("OutputPath")]
    internal sealed class OutputPathValueProvider : InterceptingPropertyValueProviderBase
    {
        public override Task<string> OnSetPropertyValueAsync(string unevaluatedPropertyValue, IProjectProperties defaultProperties, IReadOnlyDictionary<string, string> dimensionalConditions = null)
        {
            string outputPath = ".";
            if (!string.IsNullOrWhiteSpace(unevaluatedPropertyValue))
            {
                outputPath = unevaluatedPropertyValue;
            }
            return Task.FromResult(EnsureFinalBackslash(outputPath));
        }
        private static string EnsureFinalBackslash(string path)
        {
            if (!path.EndsWith("\\"))
            {
                path += "\\";
            }
            return path;
        }
    }
}