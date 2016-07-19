// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.Properties
{
    [ExportInterceptingPropertyValueProvider("PlatformTarget")]
    internal sealed class PlatformTargetValueProvider : InterceptingPropertyValueProviderBase
    {
        public override Task<string> OnSetPropertyValueAsync(string unevaluatedPropertyValue, IProjectProperties defaultProperties, IReadOnlyDictionary<string, string> dimensionalConditions = null)
        {
            var possibleTargets = new[] {"AnyCPU", "x86", "x64", "Itanium"};
            if (possibleTargets.Contains(unevaluatedPropertyValue, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, Resources.enum_out_of_range, "PlatformTarget"));
            }
            return Task.FromResult("anycpu");
        }
    }
}