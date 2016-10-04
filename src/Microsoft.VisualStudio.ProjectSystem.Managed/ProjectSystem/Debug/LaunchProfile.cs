// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.VisualStudio.ProjectSystem.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.VisualStudio.ProjectSystem.Debug
{

    /// <summary>
    /// Represents one launch profile read from the launchSettings file.
    /// </summary>
    internal class LaunchProfile : ILaunchProfile
    {
        public LaunchProfile()
        {
        }

        public LaunchProfile(LaunchProfileData data)
        {
            Name = data.Name;
            Kind = data.Kind;
            ExecutablePath = data.ExecutablePath;
            CommandName = data.CommandName;
            CommandLineArgs = data.CommandLineArgs; 
            WorkingDirectory = data.WorkingDirectory;
            LaunchBrowser = data.LaunchBrowser?? false;
            LaunchUrl = data.LaunchUrl;
            EnvironmentVariables = data.EnvironmentVariables == null ? null : ImmutableDictionary<string, string>.Empty.AddRange(data.EnvironmentVariables);
            OtherSettings = data.OtherSettings == null ? null : ImmutableDictionary<string, object>.Empty.AddRange(data.OtherSettings);
        }


        /// <summary>
        /// Useful to create a mutable version from an existing immutable profile
        /// </summary>
        public LaunchProfile(ILaunchProfile existingProfile)
        {
           Name = existingProfile.Name;
           Kind = existingProfile.Kind;
           ExecutablePath = existingProfile.ExecutablePath;
           CommandName = existingProfile.CommandName;
           CommandLineArgs = existingProfile.CommandLineArgs; 
           WorkingDirectory = existingProfile.WorkingDirectory;
           LaunchBrowser = existingProfile.LaunchBrowser;
           LaunchUrl = existingProfile.LaunchUrl;
           EnvironmentVariables = existingProfile.EnvironmentVariables;
           OtherSettings = existingProfile.OtherSettings;
        }

        /// <summary>
        /// IDebug profile members
        /// </summary>
        public string Name { get; set; }
        public string CommandName { get; set; }
        public string ExecutablePath { get; set; }
        public string CommandLineArgs { get; set; }
        public string WorkingDirectory { get; set; }
        public bool LaunchBrowser { get; set; }
        public string LaunchUrl { get; set; }

        private IDictionary<string, string> _environmentVariables;
        public ImmutableDictionary<string, string> EnvironmentVariables
        {
            get
            {
                return _environmentVariables == null ?
                    null: ImmutableDictionary<string, string>.Empty.AddRange(_environmentVariables);
            }
            set
            {
                _environmentVariables = value;
            }
        }

        private IDictionary<string, object> _otherSettings;
        public ImmutableDictionary<string, object> OtherSettings
        {
            get
            {
                return _otherSettings == null ?
                    null: ImmutableDictionary<string, object>.Empty.AddRange(_otherSettings);
            }
            set
            {
                _otherSettings = value;
            }
        }

        public ProfileKind Kind { get; set; }

        public string ApplicationUrl { get; set; }

        public string SDKVersion { get; set; }

        public bool IsDefaultIISExpressProfile
        {
            get
            {
                return Kind == ProfileKind.IISExpress && IsDefaultWebProfileName(Name);
            }
        }

        internal IDictionary<string, string> MutableEnvironmentVariables
        {
            get
            {
                return _environmentVariables;
            }
            set
            {
                if (value == null)
                {
                    _environmentVariables = null;
                }
                else
                {
                    _environmentVariables = new Dictionary<string, string>(value);
                }
            }
        }

        /// <summary>
        /// Quick check if this is a web profile (selfhost web command, iis express, or iis)
        /// </summary>
        public bool IsWebBasedProfile
        {
            get
            {
#if ENABLE_SELFHOSTSERVER
                return Kind == ProfileKind.IISExpress || Kind == ProfileKind.IIS || IsWebServerCmdProfile;
#else
                return Kind == ProfileKind.IISExpress ||
                       ((Kind == ProfileKind.BuiltInCommand || Kind == ProfileKind.CustomizedCommand) && string.Equals(CommandName, LaunchSettingsProvider.WebCommandName, StringComparison.OrdinalIgnoreCase));

#endif        
            }
        }

        /// <summary>
        /// Special treatment for command profiles that use self host servers like "web"
        /// NOTE That this command will only be set once code in #if ENABLE_SELFHOSTSERVER
        /// blocks is enabled. There is no code to set it
        /// </summary>
        public bool IsWebServerCmdProfile { get; set; }

        /// <summary>
        /// Compares two profile names. Using this function ensures case comparison consistency
        /// </summary>
        public static bool IsSameProfileName(string name1, string name2)
        {
            return string.Equals(name1, name2, StringComparison.Ordinal);
        }

        public static bool IsDefaultWebProfileName(string name)
        {
            return IsSameProfileName(name, LaunchSettingsProvider.IISExpressProfileName);
        }

        /// <summary>
        /// If this is a built in profile or the IIS Express one, we don't write it out unless there are customizations. All
        /// other profiles we do want to persist
        /// </summary>
        public Boolean ProfileShouldBePersisted()
        {
            // Never persist our dummy NoAction profile
            if (Kind == ProfileKind.NoAction)
            {
                return false;
            }
            else if (Kind == ProfileKind.BuiltInCommand || Kind == ProfileKind.IISExpress || Kind == ProfileKind.Project)
            {
                if ((EnvironmentVariables == null || EnvironmentVariables.Count == 0) &&
                   string.IsNullOrEmpty(CommandLineArgs) &&
                   string.IsNullOrEmpty(WorkingDirectory) &&
                   string.IsNullOrEmpty(LaunchUrl) &&
                   string.IsNullOrEmpty(SDKVersion) &&
                   string.IsNullOrEmpty(ApplicationUrl) &&
                   (Kind != ProfileKind.IISExpress || IsDefaultWebProfileName(Name)) &&
                   ((Kind == ProfileKind.BuiltInCommand && LaunchBrowser == false) || (Kind == ProfileKind.IISExpress && LaunchBrowser)))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Compares two IDebugProfiles to see if they contain the same values. Note that it doesn't compare
        /// the KInd field unless includeProfileKind is true
        /// </summary>
        public static bool ProfilesAreEqual(ILaunchProfile debugProfile1, ILaunchProfile debugProfile2, bool includeProfileKind)
        {
            // Same instance better return they are equal
            if (debugProfile1 == debugProfile2)
            {
                return true;
            }

            if (!string.Equals(debugProfile1.Name, debugProfile2.Name, StringComparison.Ordinal) ||
               !string.Equals(debugProfile1.CommandName, debugProfile2.CommandName, StringComparison.Ordinal) ||
               !string.Equals(debugProfile1.ExecutablePath, debugProfile2.ExecutablePath, StringComparison.Ordinal) ||
               !string.Equals(debugProfile1.CommandLineArgs, debugProfile2.CommandLineArgs, StringComparison.Ordinal) ||
               !string.Equals(debugProfile1.WorkingDirectory, debugProfile2.WorkingDirectory, StringComparison.Ordinal) ||
               !string.Equals(debugProfile1.LaunchUrl, debugProfile2.LaunchUrl, StringComparison.Ordinal) ||
               !string.Equals(debugProfile1.ApplicationUrl, debugProfile2.ApplicationUrl, StringComparison.Ordinal) ||
               (debugProfile1.LaunchBrowser != debugProfile2.LaunchBrowser) ||
               !string.Equals(debugProfile1.SDKVersion, debugProfile2.SDKVersion, StringComparison.Ordinal) ||
               (includeProfileKind && debugProfile1.Kind != debugProfile2.Kind))
            {
                return false;
            }

            // Same collection or both null
            if (debugProfile1.EnvironmentVariables == debugProfile2.EnvironmentVariables)
            {
                return true;
            }

            // XOR. One null the other non-null. Consider. Should empty dictionary be treated the same as null??
            if ((debugProfile1.EnvironmentVariables == null) ^ (debugProfile2.EnvironmentVariables == null))
            {
                return false;
            }
            return DictionaryEqualityComparer<string, string>.Instance.Equals(debugProfile1.EnvironmentVariables, debugProfile2.EnvironmentVariables);
        }
    }
}
