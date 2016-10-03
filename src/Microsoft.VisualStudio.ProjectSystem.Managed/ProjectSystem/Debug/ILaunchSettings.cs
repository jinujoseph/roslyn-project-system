// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.VisualStudio.ProjectSystem.Debug
{
    /// <summary>
    /// Interface definition for an immutable launch settings snapshot.
    /// </summary>
    public interface ILaunchSettings
    {
        string ActiveProfileName { get; }

        ILaunchProfile  ActiveProfile { get; }        

        /// <summary>
        /// Access to the current set of launch profiles
        /// </summary>
        ImmutableList<ILaunchProfile> Profiles { get; }

        /// <summary>
        /// Settings specific to IIS and IIS Express. Note that can be null if there are
        /// no iis profiles
        /// </summary>
        IIISSettings IISSettings { get; }

        /// <summary>
        /// Provides access to custom global launch settings data. The returned value depends
        /// on the section being retrieved. The settingsName matches the section in the
        /// settings file
        /// 
        /// </summary>
        object GetGlobalSetting(string settingsName);
       
        /// <summary>
        /// Provides access to all the global settings
        /// </summary>
        ImmutableDictionary<string, object>  GlobalSettings { get; }

        // Returns true if the profiles in profilesToCompare differ from these ones
        bool ProfilesAreDifferent(IList<ILaunchProfile> profilesToCompare);

        bool IISSettingsAreDifferent(IIISSettings settingsToCompare);
    }
}
