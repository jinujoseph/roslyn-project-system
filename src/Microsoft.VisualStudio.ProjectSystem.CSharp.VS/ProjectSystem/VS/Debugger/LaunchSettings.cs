//--------------------------------------------------------------------------------------------
// LaunchSettings
//
// Represents the current set of profiles and server settings
//
// Copyright(c) 2014 Microsoft Corporation
//--------------------------------------------------------------------------------------------
namespace Microsoft.VisualStudio.ProjectSystem.VS.Debugger
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.VisualStudio.ProjectSystem.DotNet;
    using Microsoft.VisualStudio.ProjectSystem.DotNet.Debugger;

    internal class LaunchSettings : ILaunchSettings
    {
        public string  ActiveProfileName { get; private set; } 

        public ImmutableList<IDebugProfile> Profiles { get; private set; }

        public IIISSettings IISSettings { get; private set; }

        public IDebugProfile ActiveProfile { get; private set; }

        /// <summary>
        /// Creation from an existing set of profiles. Note that it takes ownership of the profiles, it
        /// does not copy them, unless makeCopy is true. Note that iisSettings can be null if there are no
        /// settings
        /// </summary>
        public LaunchSettings(IEnumerable<IDebugProfile> profiles, bool makeCopy, IIISSettings iisSettings, string activeProfile = null)
        {
            if(makeCopy)
            {
                Profiles = ImmutableList<IDebugProfile>.Empty;
                foreach(var profile in profiles)
                {
                    Profiles = Profiles.Add(new DebugProfile(profile));
                }
                IISSettings = iisSettings == null? null : new IISSettings(iisSettings);
            }
            else
            {
                Profiles = profiles.ToImmutableList();
                IISSettings = iisSettings;
            }

            // If not active project specifed, assume the first one
            if(string.IsNullOrWhiteSpace(activeProfile) && Profiles.Count > 0)
            {
                ActiveProfileName = Profiles[0].Name;
                ActiveProfile = Profiles[0];
            }
            else
            {
                ActiveProfileName = activeProfile;
                if(!string.IsNullOrWhiteSpace(activeProfile))
                {
                    ActiveProfile = Profiles.FirstOrDefault(p => DebugProfile.IsSameProfileName(p.Name, activeProfile));
                }
            }
        }

        

        /// <summary>
        /// Convenent way to detect if theere are changes to the profiles
        /// </summary>
        public bool ProfilesAreDifferent(IList<IDebugProfile> profilesToCompare)
        {
            bool detectedChanges = Profiles == null || Profiles.Count != profilesToCompare.Count;
            if(!detectedChanges)
            {
                // Now compare each item
                foreach(var profile in profilesToCompare)
                {
                    var existingProfile = Profiles.FirstOrDefault(p => DebugProfile.IsSameProfileName(p.Name, profile.Name));
                    if(existingProfile == null || !DebugProfile.ProfilesAreEqual(profile, existingProfile, true))
                    {
                        detectedChanges = true;
                        break;
                    }
                }
            }
            return detectedChanges;
        }

        /// <summary>
        /// Convenient way to detect if there are changes to iis settings. 
        /// </summary>
        public bool IISSettingsAreDifferent(IIISSettings settingsToCompare)
        {
            if(IISSettings == null)
            {
                // Treat empty and null as equivalent
                return !(settingsToCompare == null || Debugger.IISSettings.IsEmptySettings(settingsToCompare));
            }
            else if(settingsToCompare == null)
            {
                return !Debugger.IISSettings.IsEmptySettings(IISSettings);
            }
        
            // Compare each item
            return Debugger.IISSettings.SettingsDiffer(IISSettings, settingsToCompare);
        }
    }
}
