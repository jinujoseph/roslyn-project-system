// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.Properties
{
    [ExportInterceptingPropertyValueProvider("PlatformTarget")]
    internal sealed class PlatformTargetValueProvider : InterceptingPropertyValueProviderBase
    {
        private readonly UnconfiguredProject _unconfiguredProject;

        [ImportingConstructor]
        public PlatformTargetValueProvider(UnconfiguredProject unconfiguredProject)
        {
            Requires.NotNull(unconfiguredProject, nameof(unconfiguredProject));

            _unconfiguredProject = unconfiguredProject;
        }

        public override Task<string> OnSetPropertyValueAsync(string unevaluatedPropertyValue, IProjectProperties defaultProperties, IReadOnlyDictionary<string, string> dimensionalConditions = null)
        {
            var possibleTargets = new[] {"AnyCPU", "x86", "x64", "Itanium"};
            if (possibleTargets.Contains(unevaluatedPropertyValue, StringComparer.OrdinalIgnoreCase))
            {
                return Task.FromResult(unevaluatedPropertyValue);
            }
            return Task.FromResult("anycpu");
        }
    }
}