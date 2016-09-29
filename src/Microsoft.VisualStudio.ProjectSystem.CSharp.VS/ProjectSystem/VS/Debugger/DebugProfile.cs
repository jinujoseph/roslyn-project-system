//--------------------------------------------------------------------------------------------
// DebugProfileData
//
// Represents one debug profile
//
// Copyright(c) 2014 Microsoft Corporation
//--------------------------------------------------------------------------------------------
namespace Microsoft.VisualStudio.ProjectSystem.DotNet.Debugger
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Newtonsoft.Json;
    using Microsoft.VisualStudio.ProjectSystem.DotNet.Utilities;
    using Microsoft.VisualStudio.ProjectSystem.DotNet;

    // Test Adapter has its own copy of these classes for de-serialization. If there is any change to these classes, 
    // Test Adapter also needs to be updated.
    [JsonObject(MemberSerialization.OptIn)]
    internal class DebugProfileData
    {
        // We don't serialize the name as it the dictionary index
        public string Name { get; set; }

        [JsonProperty(PropertyName="executablePath")]
        public string ExecutablePath { get; set; }

        [JsonProperty(PropertyName="commandName")]
        public string CommandName { get; set; }

        [JsonProperty(PropertyName="commandLineArgs")]
        public string CommandLineArgs{ get; set; }

        [JsonProperty(PropertyName="workingDirectory")]
        public string WorkingDirectory{ get; set; }

        [JsonProperty(PropertyName="launchBrowser")]
        public bool?  LaunchBrowser { get; set; }

        [JsonProperty(PropertyName="launchUrl")]
        public string LaunchUrl { get; set; }

        [JsonProperty(PropertyName="applicationUrl")]
        public string ApplicationUrl { get; set; }

        [JsonProperty(PropertyName="environmentVariables")]
        public IDictionary<string, string>  EnvironmentVariables { get; set; }

        [JsonProperty(PropertyName="sdkVersion")]
        public string SDKVersion{ get; set; }

        /// <summary>
        /// Helper to convert an IDebugProfile back to its serializable form. It deos some
        /// fixup. Like setting empty values to null, and for IIS Express profiles sets the 
        /// command name to the IIS Epress one so we recognize oa such on deserialize.
        /// </summary>
        public static DebugProfileData FromIDebugProfile(IDebugProfile profile)
        {
            DebugProfileData data = new DebugProfileData();
            data.Name = profile.Name;
            data.ExecutablePath = string.IsNullOrEmpty(profile.ExecutablePath) ? null : profile.ExecutablePath;
            if(profile.Kind == ProfileKind.IISExpress)
            {
                //data.CommandName = LaunchSettingsProvider.IISExpressProfileCommandName;
            }
            else if(profile.Kind == ProfileKind.Project)
            {
                //data.CommandName = LaunchSettingsProvider.RunProjectCommandName;
            }
            else
            {
                data.CommandName = string.IsNullOrEmpty(profile.CommandName) ? null : profile.CommandName;
            }
            data.CommandLineArgs = string.IsNullOrEmpty(profile.CommandLineArgs) ? null : profile.CommandLineArgs;
            data.WorkingDirectory = string.IsNullOrEmpty(profile.WorkingDirectory) ? null : profile.WorkingDirectory;
            data.LaunchBrowser = profile.LaunchBrowser? true : (bool?)null;
            data.LaunchUrl= string.IsNullOrEmpty(profile.LaunchUrl) ? null : profile.LaunchUrl; 
            data.ApplicationUrl= string.IsNullOrEmpty(profile.ApplicationUrl) ? null : profile.ApplicationUrl; 
            data.EnvironmentVariables = profile.EnvironmentVariables;
            data.SDKVersion = string.IsNullOrEmpty(profile.SDKVersion)? null : profile.SDKVersion;
            return data;
        }
    }

    internal class DebugProfile : IDebugProfile
    {

        private IDictionary<string, string> _environmentVariables;
        
        /// <summary>
        /// Singleton profiles to represent NoAction (classlibrary) and Error conditions.
        /// </summary>
       // public static DebugProfile NoActionProfile = new DebugProfile() {Name = Resources.ProfileKindNoActionName, Kind = ProfileKind.NoAction};
        public static DebugProfile ErrorProfile = new DebugProfile();// {Name = Resources.ProfileKindNoActionName, Kind = ProfileKind.NoAction, CommandName=LaunchSettingsProvider.ErrorProfileCommandName};

        /// <summary>
        /// IDebug profile members
        /// </summary>
        public string Name { get; set; }
        public ProfileKind Kind { get; set; }
        public string CommandName { get; set; }
        public string ExecutablePath { get; set; }
        public string CommandLineArgs { get; set; }
        public string WorkingDirectory { get; set; }
        public bool LaunchBrowser { get; set; }
        public string LaunchUrl { get; set; }
        public string ApplicationUrl { get; set; }

        public ImmutableDictionary<string, string> EnvironmentVariables
        {
            get
            {
                return _environmentVariables == null ?
                    null : ImmutableDictionary<string, string>.Empty.AddRange(_environmentVariables);
            }
            set
            {
                MutableEnvironmentVariables = value;
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

        public string SDKVersion{ get; set; }

        public bool IsDefaultIISExpressProfile
        {
            get
            {
                return Kind == ProfileKind.IISExpress && IsDefaultWebProfileName(Name);
            }
        }

        /// <summary>
        /// Special treatment for command profiles that use self host servers like "web"
        /// NOTE That this command will only be set once code in #if ENABLE_SELFHOSTSERVER
        /// blocks is enabled. There is no code to set it
        /// </summary>
        public bool IsWebServerCmdProfile { get; set; }

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
                       ((Kind == ProfileKind.BuiltInCommand || Kind == ProfileKind.CustomizedCommand) //&& string.Equals(CommandName, LaunchSettingsProvider.WebCommandName, StringComparison.OrdinalIgnoreCase)
                       );

#endif        
            }
        }

        public DebugProfile()
        {
            Kind = ProfileKind.Executable;
        }

        public DebugProfile(DebugProfileData data)
        {
            Name = data.Name;
            ExecutablePath = data.ExecutablePath;
            CommandName = data.CommandName;
            CommandLineArgs = data.CommandLineArgs; 
            WorkingDirectory = data.WorkingDirectory;
            LaunchBrowser = data.LaunchBrowser?? false;
            LaunchUrl = data.LaunchUrl;
            ApplicationUrl = data.ApplicationUrl;
            EnvironmentVariables = data.EnvironmentVariables == null ? null : ImmutableDictionary<string, string>.Empty.AddRange(data.EnvironmentVariables);
            SDKVersion = data.SDKVersion;
            FixProfile();
        }


        /// <summary>
        /// Useful to create a mutable version from an existing immutable profile
        /// </summary>
        public DebugProfile(IDebugProfile existingProfile)
        {
           Name = existingProfile.Name;
           ExecutablePath = existingProfile.ExecutablePath;
           Kind = existingProfile.Kind;
           CommandName = existingProfile.CommandName;
           CommandLineArgs = existingProfile.CommandLineArgs; 
           WorkingDirectory = existingProfile.WorkingDirectory;
           LaunchBrowser = existingProfile.LaunchBrowser;
           LaunchUrl = existingProfile.LaunchUrl;
           ApplicationUrl = existingProfile.ApplicationUrl;
           EnvironmentVariables = existingProfile.EnvironmentVariables;
           SDKVersion = existingProfile.SDKVersion;
           IsWebServerCmdProfile = existingProfile.IsWebServerCmdProfile;
        }

        /// <summary>
        /// Sets the command type based on the type of data we have
        /// </summary>
        private void FixProfile()
        {
            if(!string.IsNullOrWhiteSpace(CommandName))
            {
                if(IsIISExpressCommandName(CommandName))
                {
                    Kind = ProfileKind.IISExpress;
                    //CommandName = LaunchSettingsProvider.IISExpressProfileCommandName;
                }
                else if(IsRunProjectCommandName(CommandName))
                {
                    Kind = ProfileKind.Project;
                    //CommandName = LaunchSettingsProvider.RunProjectCommandName;
                }
// billhie: this is the only place the ProfileKind.IIS is set. IIS is disabled until RC2 once we have sorted out
// the hosting story.
#if IISSUPPORT
                else if(IsIISCommandName(CommandName))
                {
                    Kind = ProfileKind.IIS;
                    CommandName = LaunchSettingsProvider.IISProfileCommandName;
                }
#endif
                else
                {
                    // Note that this one may be converted to a built in command the DebugProfileProvider if
                    // the name matches a built in command
                    Kind = ProfileKind.CustomizedCommand;
                }
            }
            else
            {
                Kind = ProfileKind.Executable;
            }
        }

        /// <summary>
        /// If this is a built in profile or the IIS Express one, we don't write it out unless there are customizations. All
        /// other profiles we do want to persist
        /// </summary>
        public bool ProfileShouldBePersisted()
        {
            // Never persist our dummy NoAction profile
            if(Kind == ProfileKind.NoAction)
            {
                return false;
            }
            else if(Kind == ProfileKind.BuiltInCommand || Kind == ProfileKind.IISExpress || Kind == ProfileKind.Project)
            {
                if((EnvironmentVariables == null || EnvironmentVariables.Count == 0) &&
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
        /// Compares two profile names. Using this function ensures case comparison consistency
        /// </summary>
        public static bool IsSameProfileName(string name1, string name2)
        {
            return string.Equals(name1, name2, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDefaultWebProfileName(string name)
        {
            return false; // IsSameProfileName(name, LaunchSettingsProvider.IISExpressProfileName);
        }
        /// <summary>
        /// IIS Expresss profiles are serialized with the command = IISExpress. This only 
        /// use for serializing\deserializing purposes. It is not present in the profiles normally
        /// </summary>
        public static bool IsIISExpressCommandName(string name)
        {
            return false;// IsSameProfileName(name, LaunchSettingsProvider.IISExpressProfileCommandName);
        }

        public static bool IsRunProjectCommandName(string name)
        {
            return false; // IsSameProfileName(name, LaunchSettingsProvider.RunProjectCommandName);
        }

        public static bool IsIISCommandName(string name)
        {
            return false; // IsSameProfileName(name, LaunchSettingsProvider.IISProfileCommandName);
        }

        /// <summary>
        /// Compares two IDebugProfiles to see if they contain the same values. Note that it doesn't compare
        /// the KInd field unless includeProfileKind is true
        /// </summary>
        public static bool ProfilesAreEqual(IDebugProfile debugProfile1, IDebugProfile debugProfile2, bool includeProfileKind)
        {
            // Same instance better return they are equal
            if(debugProfile1 == debugProfile2)
            {
                return true;
            }

            if(!string.Equals(debugProfile1.Name, debugProfile2.Name, StringComparison.Ordinal) ||
               !string.Equals(debugProfile1.CommandName, debugProfile2.CommandName, StringComparison.Ordinal) || 
               !string.Equals(debugProfile1.ExecutablePath, debugProfile2.ExecutablePath, StringComparison.Ordinal) || 
               !string.Equals(debugProfile1.CommandLineArgs, debugProfile2.CommandLineArgs, StringComparison.Ordinal) ||
               !string.Equals(debugProfile1.WorkingDirectory, debugProfile2.WorkingDirectory, StringComparison.Ordinal) ||
               !string.Equals(debugProfile1.LaunchUrl, debugProfile2.LaunchUrl, StringComparison.Ordinal) ||
               !string.Equals(debugProfile1.ApplicationUrl, debugProfile2.ApplicationUrl, StringComparison.Ordinal) ||
               (debugProfile1.LaunchBrowser != debugProfile2.LaunchBrowser) ||
               !string.Equals(debugProfile1.SDKVersion, debugProfile2. SDKVersion, StringComparison.Ordinal) ||
               (includeProfileKind && debugProfile1.Kind != debugProfile2.Kind))
            {
                return false;
            }

            // Same collection or both null
            if(debugProfile1.EnvironmentVariables == debugProfile2.EnvironmentVariables)
            {
                return true;
            }

            // XOR. One null the other non-null. Consider. Should empty dictionary be treated the same as null??
            if((debugProfile1.EnvironmentVariables == null) ^ (debugProfile2.EnvironmentVariables == null))
            {
                return false;
            }
            return DictionaryEqualityComparer<string, string>.Instance.Equals(debugProfile1.EnvironmentVariables, debugProfile2.EnvironmentVariables);
        }
    }
}
