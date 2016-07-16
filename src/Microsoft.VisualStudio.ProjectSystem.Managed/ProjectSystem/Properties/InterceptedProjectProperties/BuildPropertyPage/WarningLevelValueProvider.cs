// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.Properties
{
    [ExportInterceptingPropertyValueProvider("WarningLevel")]
    internal sealed class WarningLevelValueProvider : InterceptingPropertyValueProviderBase
    {
        public override Task<string> OnSetPropertyValueAsync(string unevaluatedPropertyValue, IProjectProperties defaultProperties, IReadOnlyDictionary<string, string> dimensionalConditions = null)
        {
            int warningLevel;
            if (Int32.TryParse(unevaluatedPropertyValue, out warningLevel))
            {
                if (!Enumerable.Range(0, 4).Contains(warningLevel))
                {
                    warningLevel = 0;
                }
            }
            return Task.FromResult(warningLevel.ToString());
        }
    }
}