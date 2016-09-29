// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.VisualStudio.Shell;
using System;

namespace Microsoft.VisualStudio.ProjectSystem.VS.Generators
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    internal sealed class ClassRegistrationAttribute : RegistrationAttribute
    {
        private readonly string _clsId;
        private readonly string _assemblyInfo;
        private readonly string _classInfo;

        public ClassRegistrationAttribute(string clsId, string assemblyInfo, string classInfo)
        {
            if (clsId == null)
                throw new ArgumentNullException(nameof(clsId));
            if (assemblyInfo == null)
                throw new ArgumentNullException(nameof(assemblyInfo));
            if (classInfo == null)
                throw new ArgumentNullException(nameof(classInfo));

            _clsId = clsId;
            _assemblyInfo = assemblyInfo;
            _classInfo = classInfo;
        }

        public override void Register(RegistrationContext context)
        {
            using (Key childKey = context.CreateKey($"CLSID\\{_clsId}"))
            {
                childKey.SetValue("Assembly", _assemblyInfo);
                childKey.SetValue("Class", _classInfo);
                childKey.SetValue("InprocServer32", "$System$\\mscoree.dll");
                childKey.SetValue("ThreadingModel", "Both");
            }
        }

        public override void Unregister(RegistrationContext context)
        {
            context.RemoveKey(_clsId);
        }
    }
}
