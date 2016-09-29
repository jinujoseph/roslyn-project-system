//--------------------------------------------------------------------------------------------
// ILaunchSettings
//
// Interface definition for a launch settings snapshot.
//
// Copyright(c) 2014 Microsoft Corporation
//--------------------------------------------------------------------------------------------
namespace Microsoft.VisualStudio.ProjectSystem.DotNet
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    
    internal interface ILaunchSettings
    {
        string  ActiveProfileName { get; }        
        IDebugProfile  ActiveProfile { get; }        

        ImmutableList<IDebugProfile> Profiles { get; }
        
        /// <summary>
        /// Settings specific to IIS and IIS Express. Note that can be null if there are
        /// no iis profiles
        /// </summary>
        IIISSettings IISSettings { get; }

        // Returns true if the profiles in profilesToCompare differ from these ones
        bool ProfilesAreDifferent(IList<IDebugProfile> profilesToCompare);

        bool IISSettingsAreDifferent(IIISSettings settingsToCompare);
    }
}
