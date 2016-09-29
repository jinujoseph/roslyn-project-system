//--------------------------------------------------------------------------------------------
// LaunchSettingProvider
//
// Manages the set of Debug profiles and web server settings and provides these as a dataflow source. Note 
// that many of the methods are protected so that unit tests can derive from this class and poke them as
// needed w/o making them public
//
// Copyright(c) 2014 Microsoft Corporation
//--------------------------------------------------------------------------------------------
namespace Microsoft.VisualStudio.ProjectSystem.DotNet.Debugger
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.ComponentModel.Composition;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using Microsoft.VisualStudio.ProjectSystem;
    using Microsoft.VisualStudio.ProjectSystem.DotNet;
    using Microsoft.VisualStudio.ProjectSystem.DotNet.Errors;
    using Microsoft.VisualStudio.ProjectSystem.DotNet.PropertyProviders;
    using Microsoft.VisualStudio.ProjectSystem.DotNet.Utilities;
    using Newtonsoft.Json;

#if VS14
    using Microsoft.VisualStudio.ProjectSystem.Utilities;
    using Microsoft.VisualStudio.ProjectSystem.Utilities.Designers;
    using Microsoft.VisualStudio.ProjectSystem.Designers;
#else
    using Microsoft.VisualStudio.ProjectSystem.Properties;
#endif

    [Export (typeof(ILaunchSettingsProvider))]
    [AppliesTo(Capabilities.DotNetCore)]
    internal class LaunchSettingsProvider : OnceInitializedOnceDisposed, ILaunchSettingsProvider
    {

        [Import]
        protected UnconfiguredDotNetProject UnconfiguredDotNetProject { get; set;}

        [Import]
        protected IProjectSubscriptions Subscriptions{ get; set;}

        [Import]
        protected IProjectErrorManager ProjectErrorManager { get; set;}

        [Import(AllowDefault = true)]
        protected Lazy<ISourceCodeControlIntegration> SourceControlIntegration { get; set; }

        [Import]
        protected IDotNetThreadHandling ThreadHandling { get; set;}

        private IActiveConfiguredProjectSubscriptionService ProjectSubscriptionService
        {
            get
            {
                return this.UnconfiguredDotNetProject.ActiveConfiguredProjectSubscriptionService ;
            }
        }

        // The source for our dataflow
        private IReceivableSourceBlock<ILaunchSettings> _changedSourceBlock;
        private BroadcastBlock<ILaunchSettings> _broadcastBlock;

        protected IFileSystem FileManager { get; set; }

        // Used to track our errors so we can flush them later
        public  const string ErrorOwnerString = "LaunchSettingProvider";
        public  const string IISExpressProfileName = "IIS Express";

        // This command name is only present at the serialization boundary. It will never appear in 
        // the actual profiles
        public  const string IISExpressProfileCommandName = "IISExpress";
        public  const string IISProfileCommandName = "IIS";

        // Command that means run this project
        public  const string RunProjectCommandName = "Project";

        public  const string ErrorProfileCommandName = "ErrorProfile";

        // Have a bit of special treatment for the web command
        public const string WebCommandName = "web";

        // Host names which indicate cmd string is a web server
        static string [] ServerHosts = new string [] { "Microsoft.AspNet.Hosting", "Microsoft.AspNet.Server.Kestrel", "Microsoft.AspNet.Server.WebListener"};
        public const string WebUrlOption = "--server.urls";
        public const string WebUrlOptionWithEquals = "--server.urls=";
        public const string DefaultWebUrl = "http://localhost:5000/";

        /// <summary>
        /// The link that represents the project information subscription.
        /// </summary>
        protected IDisposable SubscriptionLink { get; set; }

        protected SimpleFileWatcher FileWatcher { get; set; }

        // When we are saveing the file we set this to minimize noise from the file change
        protected bool IgnoreFileChanges { get; set; }

        private TimeSpan FileChangeProcessingDelay = TimeSpan.FromMilliseconds(500);

        protected ITaskDelayScheduler FileChangeScheduler { get; set; }

        // Tracks when we last read or wrote to the file. Prevents picking up needless changes
        protected DateTime LastSettingsFileSyncTime { get; set; }

        private TaskCompletionSource<bool> _firstSnapshotCompletionSource = new TaskCompletionSource<bool>();

        /// <summary>
        /// Access to the debug settings file
        /// </summary>
        private string _debugSettingsFile;
        public string DebugSettingsFile
        {
            get 
            {
                if(_debugSettingsFile == null)
                {
                    _debugSettingsFile = Path.Combine(UnconfiguredDotNetProject.ProjectFolder, ProjectConstants.ProjectRelativeDebugSettingsFile);
                }
                return _debugSettingsFile;
            }
        }

        /// <summary>
        /// Access to properties
        /// </summary>
        protected IPropertyProvider _propertyProvider;
        public IPropertyProvider PropertyProvider
        {
            get 
            {
                if(_propertyProvider == null)
                {
                    _propertyProvider = new UnconfiguredPropertyProvider(UnconfiguredDotNetProject);
                }
                return _propertyProvider;
            }
        }

        /// <summary>
        /// Link to this source block to be notified when the snapshot is changed.
        /// </summary>
        public IReceivableSourceBlock<ILaunchSettings> SourceBlock
        {
            get
            {
                EnsureInitialized();
                return _changedSourceBlock;
            }
        }
           
        /// <summary>
        /// IOebugProfileProvider
        /// Access to the current set of profile information
        /// </summary>
        private ILaunchSettings _currentSnapshot;
        public ILaunchSettings CurrentSnapshot
        {
            get
            {
                if(!UnitTestHelper.IsRunningUnitTests)
                {
                    EnsureInitialized();
                }
                return _currentSnapshot;
            }
            protected set
            {
                // If this is the first snapshot, complete the taskCompletionSource
                if(_currentSnapshot == null)
                {
                    _firstSnapshotCompletionSource.TrySetResult(true);
                }
                _currentSnapshot = value;
            }
        }

        /// <summary>
        /// IOebugProfileProvider
        /// Returns the active profile if any
        /// </summary>
        public IDebugProfile ActiveProfile 
        { 
            get
            {
                var snapshot = CurrentSnapshot;
                return snapshot ?.ActiveProfile;
            }
        }

        /// <summary>
        /// Creates a default fileManager instance
        /// </summary>
        public LaunchSettingsProvider()
        {
            FileManager = FileSystem.Instance;
        }

        /// <summary>
        /// The DebugProfileProvider sinks 3 sets of information
        /// 1, Changes to the debugsettings.json file
        /// 2. Changes to the ActiveDebugProfile property in the xproj.user file
        /// 3. Changes to the list of commands coming from project metadata
        /// </summary>
        protected override void Initialize()
        {
            
            // Create our broadcast block for subscribers to get new IDebugProfilesInformation
            _broadcastBlock = new BroadcastBlock<ILaunchSettings>(s => s);
            _changedSourceBlock = _broadcastBlock.SafePublicize();

            // Subscribe to project information to track commands and to the ActiveDebugProfile property change. Note that ugly cast is only required when 
            // building from Dev12 (C#5)
            var dependenciesChangedBlock = new ActionBlock<IProjectVersionedValue<Tuple<IProjectInformation, IProjectSubscriptionUpdate>>>(
                (Action<IProjectVersionedValue<Tuple<IProjectInformation, IProjectSubscriptionUpdate>>>)SubscriptionsChanged);
            StandardRuleDataflowLinkOptions evaluationLinkOptions = new StandardRuleDataflowLinkOptions();
            evaluationLinkOptions.RuleNames = evaluationLinkOptions.RuleNames.Add(ProjectDebugger.SchemaName);

#if VS14
            SubscriptionLink = ProjectDataSources.SyncLinkTo(
                Subscriptions.ProjectInformation.ChangedSourceBlock.SyncLinkOptions(),
                ProjectSubscriptionService.ProjectRuleBlock.SyncLinkOptions(evaluationLinkOptions),
                dependenciesChangedBlock,
                new DataflowLinkOptions { PropagateCompletion = true });           
#else

            SubscriptionLink = ProjectDataSources.SyncLinkTo(
                Subscriptions.ProjectInformation.ChangedSourceBlock.SyncLinkOptions(),
                ProjectSubscriptionService.ProjectRuleSource.SourceBlock.SyncLinkOptions(evaluationLinkOptions),
                dependenciesChangedBlock,
                new DataflowLinkOptions { PropagateCompletion = true });
#endif
            // Make sure we are watching the file at this point
            WatchLaunchSettingsFile();
        }

        /// <summary>
        /// Callback to process changes to either the active debug profile, or project information. If we detect any changes we post the new 
        /// set of IDebugProfiles.
        /// </summary>
        protected void SubscriptionsChanged(IProjectVersionedValue<Tuple<IProjectInformation, IProjectSubscriptionUpdate>> update)
        {
            string activeProfile = null;
            if(update.Value.Item2 != null)
            {
                activeProfile = GetActiveProfile(update.Value.Item2);
            }

            // We need to ensure the file is read from disk and default items are created (IIS Express).
            ILaunchSettings existingProfiles = CurrentSnapshot;
            
            UpdateProfiles(existingProfiles, activeProfile, update.Value.Item1);
           
        }

        /// <summary>
        /// Does the processing to update the profiles when changes have been made to either the file, the active profile or projectData. Note that
        /// any of the parmaters can be null. This code must handle that correctly. Setting existingProfiles to null effectively re-intilaizes the
        /// entire list.
        /// </summary>
        protected void UpdateProfiles(ILaunchSettings existingSnapshot, string activeProfile, IProjectInformation projectData)
        {
            try
            {
                bool changeMade = false;
                bool fileNeedsToBeSaved = false;
                List<DebugProfile> curProfiles;
                IISSettingsData  curIISSettings = null;
                // If we don't have an existing snapshot or the file on disk has been modified then make sure we start from the disk file
                if(existingSnapshot == null || SettingsFileHasChanged())
                {
                    var launchSettingData = GetInitialSettings();
                    curProfiles = launchSettingData.Profiles.Select(p => {return new DebugProfile(p.Value);}).ToList();
                    curIISSettings = launchSettingData.IISSettings;
                    changeMade = true;
                }
                else
                {   // Create a list from the existing snapshot, but filter out the dummy NoAction profile
                    curProfiles = existingSnapshot.Profiles.Select(p => {return new DebugProfile(p);}).Where(p => p.Kind != ProfileKind.NoAction).ToList();
                    curIISSettings = existingSnapshot.IISSettings == null? null : IISSettingsData.FromIIISSettings(existingSnapshot.IISSettings);
                }

                // Pick up any new comamands and create profiles for them (function handles a null projectData)
                if(ProcessProjectInformationChanged(curProfiles, projectData))
                {
                    changeMade = true;
                }

                // Once the processing is done above, add in any mandatory profiles
                fileNeedsToBeSaved = EnsureDefaultProfiles(curProfiles, ref curIISSettings) || fileNeedsToBeSaved;

                // Has the active profile changed
                if(existingSnapshot == null || 
                   string.IsNullOrWhiteSpace(activeProfile) || 
                   !DebugProfile.IsSameProfileName(existingSnapshot.ActiveProfileName, activeProfile))
                {
                    // We need to set the profile to something so we pick the first item
                    if(string.IsNullOrWhiteSpace(activeProfile) && curProfiles.Count > 0)
                    {
                        // Set the active profile but we will persist the property after we have finished setting up the snapshot
                        activeProfile = curProfiles[0].Name;
                    }
                    changeMade = true;
                }
                
                // If there are no profiles, like in say classlibraries, we will add a dummy hidden profile to represent it. W/o it our debugger
                // won't be called on F5 and the user will see a poor error message
                if(curProfiles.Count == 0)
                {
                    curProfiles.Add(DebugProfile.NoActionProfile);
                }

                // If the active profile isn't on our list, set it to the first one
                if(curProfiles.Count > 0 && curProfiles.FirstOrDefault(p => {return DebugProfile.IsSameProfileName(p.Name, activeProfile);}) == null)
                {
                    activeProfile = curProfiles[0].Name;
                }

                var newSnapshot = new LaunchSettings(curProfiles, false, curIISSettings == null? null: new IISSettings(curIISSettings), activeProfile);

                FinishUpdate(newSnapshot, changeMade); 

                if(fileNeedsToBeSaved)
                {
                    // We can't just write the file here. If we do we have the potential of overwriting a file being added from 
                    // the wizard (or anywhere else). Instead, we get some information about the file and delay persisting it for a little
                    // bit to see if things have changed. IF they have, a new snapshot would have been produced containing the latest file
                    // changes and we abandon this save completely. 
                    DateTime fileWriteTime = DateTime.MinValue;
                    bool fileDidExist = FileManager.FileExists(DebugSettingsFile);
                    if(fileDidExist)
                    {
                        fileWriteTime = FileManager.LastFileWriteTime(DebugSettingsFile);
                    }
                    ThreadHandling.Run(async () => 
                    {
                        try
                        {
                            // Checkout and then check the timestamp and snapshot. 
                            await CheckoutSettingsFileAsync();

                            var curSnapshot = CurrentSnapshot;  // Shouldn't be null but just in case
                            if(curSnapshot == null || 
                              curSnapshot.ProfilesAreDifferent(newSnapshot.Profiles) || 
                              curSnapshot.IISSettingsAreDifferent(newSnapshot.IISSettings))
                            {
                                // Don't want to save this since the contents are different from the current
                                return;
                            }

                            // We don't want to stomp on an external change
                            if(FileManager.FileExists(DebugSettingsFile))
                            {  
                                if(!fileDidExist || FileManager.LastFileWriteTime(DebugSettingsFile) != fileWriteTime) 
                                {
                                    return;
                                }
                            }

                            SaveSettingsToDisk(newSnapshot);
                        }
                        catch
                        {
                            // Save writes an error list entry and rethrows so it is safe to eat it here
                        }
                    });
                }
            }
            catch
            {

                // Errors are added as error list entries. We don't want to throw out of here
                // However, if we have never created a snapshot it means there is some error in the file and we want
                // to have the user see that, so we add a dummy profile
                if(CurrentSnapshot == null)
                {
                    List<IDebugProfile> profiles = new List<IDebugProfile>() {DebugProfile.ErrorProfile};
                    var snapshot = new LaunchSettings(profiles, false, null, DebugProfile.ErrorProfile.Name);
                    FinishUpdate(snapshot, true);
                }
            }
        }

        /// <summary>
        /// Returns true of the file has changed since we last read it. Note that it returns true if the file
        /// does not exist
        /// </summary>
        private bool SettingsFileHasChanged()
        {
            return !FileManager.FileExists(DebugSettingsFile) || FileManager.LastFileWriteTime(DebugSettingsFile) != LastSettingsFileSyncTime;
        }

        /// <summary>
        /// Helper function to complete the update process. The set of flags govern the pieces that requrie updating. False for
        /// all the parameters does nothing. Note that it does NOT save changes to disk.
        /// </summary>
        protected void FinishUpdate(ILaunchSettings newSnapshot,  bool updateSnapshot)
        {
            // Broadcast the changes, if any
            if(updateSnapshot)
            {
                CurrentSnapshot = newSnapshot;

                // For unit tests this is null
                if(_broadcastBlock != null)
                {
                    _broadcastBlock.Post(newSnapshot);
                }
            }
        }

        /// <summary>
        /// Gets the active profile based on the property changes
        /// </summary>
        protected string GetActiveProfile(IProjectSubscriptionUpdate projectSubscriptionUpdate)
        {
            IProjectRuleSnapshot ruleSnapshot;
            string activeProfile;
            if(projectSubscriptionUpdate.CurrentState.TryGetValue(ProjectDebugger.SchemaName, out ruleSnapshot) && ruleSnapshot.Properties.TryGetValue(ProjectDebugger.ActiveDebugProfileProperty, out activeProfile))
            {
                return activeProfile;
            }
            return null;
        }

        /// <summary>
        /// Creates the intiial set of settings based on the file on disk 
        /// </summary>
        protected LaunchSettingsData GetInitialSettings()
        {
            LaunchSettingsData  settings;
            if(FileManager.FileExists(DebugSettingsFile))
            {
                settings = ReadProfilesFromDisk();
            }
            else
            {
                // Still clear errors even if no file on disk. This handles the case where there was a file with errors on
                // disk and the user deletes the file.
                ProjectErrorManager.ClearErrorsForOwner(ErrorOwnerString);
                settings = new LaunchSettingsData();
            }

            // Make sure there is at least an empty profiles 
            if(settings.Profiles == null)
            {
                settings.Profiles = new Dictionary<string, DebugProfileData>(StringComparer.OrdinalIgnoreCase);
            }

            return settings;
        }

        /// <summary>
        /// Ensures the profile data has the correct set of default values. Note that curIISSettings can be null.
        /// Returns the true if the file needs to be saved to disk
        /// </summary>
        protected bool EnsureDefaultProfiles(List<DebugProfile> curProfiles, ref IISSettingsData  curIISSettings)
        {
            bool fileNeedsToBeSaved = false;
            if(UnconfiguredDotNetProject.IsWebProject)
            {
                fileNeedsToBeSaved = EnsureIISExpressSettings(curProfiles, ref curIISSettings) || fileNeedsToBeSaved;
                fileNeedsToBeSaved = EnsureIISSettings(curProfiles, ref curIISSettings) || fileNeedsToBeSaved;
            }
            else if(!UnconfiguredDotNetProject.IsClasslibraryProject)
            {
                // Console applications should have the project as a choice if there are no other profiles
                EnsureRunProjectProfile(curProfiles);
            }
            return fileNeedsToBeSaved;
        }

        /// <summary>
        /// Makes sure a profile for IIS Express is available in the list. If it finds an existing one, but the exe path
        /// is set, it removes the exe path and marks the profiles as requiring saving. Returns true if it modified the
        /// profiles is some way.
        /// </summary>
        protected bool EnsureIISExpressSettings(List<DebugProfile> curProfiles, ref IISSettingsData  curIISSettings)
        {
            bool modifiedSettings = false;
            DebugProfile existingWebProfile = curProfiles.FirstOrDefault(p => p.IsDefaultIISExpressProfile);
            if(existingWebProfile == null)
            {
                // Always put it in front so it becomes the default for web projects
                curProfiles.Insert(0, new DebugProfile() {
                        Name =IISExpressProfileName,
                        Kind = ProfileKind.IISExpress,
                        LaunchBrowser = true,
                        CommandName = LaunchSettingsProvider.IISExpressProfileCommandName,
                        EnvironmentVariables =ImmutableDictionary<string, string>.Empty.Add(ProjectConstants.ASPNETCORE_ENVIRONMENT, ProjectConstants.DevEnvironment)});
                modifiedSettings = true;
            }
            else if(existingWebProfile.ExecutablePath != null || !string.Equals(existingWebProfile.CommandName, LaunchSettingsProvider.IISExpressProfileCommandName))
            {
                
                existingWebProfile.ExecutablePath = null;
                existingWebProfile.CommandName = LaunchSettingsProvider.IISExpressProfileCommandName;
                modifiedSettings = true;
            }

            // Are there IIS Express settings in there?
            if(curIISSettings == null)
            {
                curIISSettings = new IISSettingsData();
            }

            if(curIISSettings.IISExpressBindingData == null)
            {
                curIISSettings.IISExpressBindingData = new ServerBindingData();
                modifiedSettings = true;
            }

            // Make sure we have an url set
            if(string.IsNullOrWhiteSpace(curIISSettings.IISExpressBindingData.ApplicationUrl))
            {
                // If we have a current snapshot with a url, we use that.
                var curSnapshot = CurrentSnapshot;
                if(curSnapshot != null && curSnapshot.IISSettings != null && curSnapshot.IISSettings.IISExpressBinding != null && 
                   !string.IsNullOrWhiteSpace(curSnapshot.IISSettings.IISExpressBinding.ApplicationUrl))
                {
                    curIISSettings.IISExpressBindingData.ApplicationUrl = curSnapshot.IISSettings.IISExpressBinding.ApplicationUrl;
                }
                else
                {
                    // Assign a new port and set the app url
                    int port =  SocketUtilities.GetNextAvailPort();
                    curIISSettings.IISExpressBindingData.ApplicationUrl = string.Format(ProjectConstants.DefaultIISExpressAppUrlFormatString, port);
                }
                modifiedSettings = true;
            }
            return modifiedSettings;
        }

        /// <summary>
        /// If there is at least one IIS profile, we wan to make sure there are some default bindings set for it
        /// </summary>
        protected bool EnsureIISSettings(List<DebugProfile> curProfiles, ref IISSettingsData  curIISSettings)
        {
            bool modifiedSettings = false;
            var iisProfile = curProfiles.FirstOrDefault(p => p.Kind == ProfileKind.IIS);
            if(iisProfile == null)
            {
                return modifiedSettings;
            }

            // Are there IIS Express settings in there?
            if(curIISSettings == null)
            {
                curIISSettings = new IISSettingsData();
            }

            if(curIISSettings.IISBindingData == null)
            {
                curIISSettings.IISBindingData = new ServerBindingData();
                modifiedSettings = true;
            }

            // Make sure we have an url set
            if(string.IsNullOrWhiteSpace(curIISSettings.IISBindingData.ApplicationUrl))
            {
                // If we have a current snapshot with a url, we use that.
                var curSnapshot = CurrentSnapshot;
                if(curSnapshot != null && curSnapshot.IISSettings != null && curSnapshot.IISSettings.IISBinding != null && 
                   !string.IsNullOrWhiteSpace(curSnapshot.IISSettings.IISBinding.ApplicationUrl))
                {
                    curIISSettings.IISBindingData.ApplicationUrl = curSnapshot.IISSettings.IISBinding.ApplicationUrl;
                }
                else
                {
                    // For IIS the default name is http://localhost/<projectname>
                    curIISSettings.IISBindingData.ApplicationUrl = string.Format(ProjectConstants.DefaultIISAppUrlFormatString, Path.GetFileNameWithoutExtension(UnconfiguredDotNetProject.FullPath));
                }
                modifiedSettings = true;
            }
            return modifiedSettings;
        }

        /// <summary>
        /// Makes sure a profile exists for console applications. The name of the profile is the name of the project
        /// </summary>
        protected bool EnsureRunProjectProfile(List<DebugProfile> curProfiles)
        {
            string projectName = UnconfiguredDotNetProject.ProjectName;
            DebugProfile existingProfile = curProfiles.FirstOrDefault(p => DebugProfile.IsSameProfileName(p.Name, projectName));

            bool modifiedSettings = false;
            if(existingProfile == null)
            {
                curProfiles.Add(new DebugProfile() 
                        {
                            Name =projectName,
                            Kind = ProfileKind.Project,
                            CommandName = LaunchSettingsProvider.RunProjectCommandName,
                        });
                modifiedSettings = true;
            }
            return modifiedSettings;
        }

        /// <summary>
        /// Creates a new IDebugProfile for a built in command. For these ones, we don't add in Executable path, command line args or 
        /// working directory. They are treated special by the debugger.
        /// </summary>
        public  DebugProfile CreateProfileForCommand(string cmdName, string cmdString)
        {
            DebugProfile newProfile = new DebugProfile()
            {
                Name = cmdName,
                Kind = ProfileKind.BuiltInCommand,
                CommandName = cmdName,
            };

            // If this is web based profile, we want to set the ASPNETEnvironment variable and the AppUrl
            ApplyWebServerDataToProfileIfNeeded(newProfile, cmdName, cmdString, isNewProfile: true);

            return newProfile;
        }

        /// <summary>
        /// If the cmdName\cmdString looks like a web command it will ensure profile is configured correctly. Returns true if it
        /// made changes requiring it to be saved
        /// </summary>
        private bool ApplyWebServerDataToProfileIfNeeded(DebugProfile profile, string cmdName, string cmdString, bool isNewProfile)
        {
            bool needsPersistence = false;

// billhie. Enable this will put back the code which treats the web command as a web server like IIS. IsWebServerCmdProfile is only set here
// and it is that flag which makes the rest of the system support the web server profile
#if ENABLE_SELFHOSTSERVER
            // If this is web based profile, we want to set the ASPNETEnvironment variable and the AppUrl
            if (IsWebServerCommand(cmdName, cmdString))
            {
                profile.IsWebServerCmdProfile = true;
                if(profile.ApplicationUrl == null)
                {
                    profile.LaunchBrowser = true;
                    string url = ExtractServerUrlFromCommand( cmdString);
                    if(url == null)
                    {   // Assume the default value
                        profile.ApplicationUrl = DefaultWebUrl;
                    }
                    else
                    {
                        profile.ApplicationUrl = url;
                    }
                    needsPersistence = true;
                }

                // Don't change env variables for existing profiles
                if(isNewProfile)
                {
                    profile.EnvironmentVariables =ImmutableDictionary<string, string>.Empty.Add(ProjectConstants.ASPNETEnvironment, ProjectConstants.DevEnvironment);
                    needsPersistence = true;
                }
            }
#else
            // Add the default environment variable only if there aren't any environment variables already
            if(profile.IsWebBasedProfile)
            {
                if(profile.EnvironmentVariables == null || profile.EnvironmentVariables.Count == 0)
                {
                    profile.EnvironmentVariables =ImmutableDictionary<string, string>.Empty.Add(ProjectConstants.ASPNETCORE_ENVIRONMENT, ProjectConstants.DevEnvironment);
                }
            }

#endif

            return needsPersistence;
        }

        /// <summary>
        /// Looks at the cmdValue (the part on the right) to see if it contains any of the web hosting types. If so, we assume it is a web server
        /// and mark it as such. Currently looks for one of the following strings on the command line:
        ///     Microsoft.AspNet.Hosting
        ///     Microsoft.AspNet.Server.Kestrel
        ///     Microsoft.AspNet.Server.WebListener
        ///  
        /// Also, commands named "web"
        /// </summary>
        public bool IsWebServerCommand(string cmdName, string cmdString)
        {
            if(cmdName.Equals(LaunchSettingsProvider.WebCommandName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.IsNullOrEmpty(cmdString))
            {
                return false;
            }

            // Look for the marker types
            foreach (var serverName in ServerHosts)
            {
                if(cmdString.IndexOf(serverName, StringComparison.Ordinal) != -1)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Looks at the cmdValue for --server.urls and if it finds it, it returns the next token. Two cases to
        /// consider --server.urls http://localhost:5000/ and --server.urls=http://localhost:5000. In both cases
        /// there could be a number of urls specified separated by commas
        /// </summary>
        public string ExtractServerUrlFromCommand(string cmdString)
        {
            if(!string.IsNullOrWhiteSpace(cmdString))
            {
                // Pass null to Split to get whitespace splitting need to cast to remove ambiguity
                string [] tokens = cmdString.Split((char [])null, StringSplitOptions.RemoveEmptyEntries);

                for(int i=0; i < tokens.Length; i++)
                {
                    // If this is the last token nothing else to do for this case
                    if(tokens[i].Equals(WebUrlOption, StringComparison.Ordinal) && i < (tokens.Length -1)) 
                    {
                        if(tokens[i+1].StartsWith("http"))
                        {
                            // Since it can contain many urls separated by comma's, we split it again and return the first one
                            return tokens[i+1].Split(new char [] {','}, StringSplitOptions.RemoveEmptyEntries)[0];
                        }
                    }
                    else if(tokens[i].StartsWith(WebUrlOptionWithEquals, StringComparison.Ordinal))
                    {
                        string [] urls = tokens[i].Split(new char [] {'='}, StringSplitOptions.RemoveEmptyEntries);
                        if(urls.Length != 2)
                        {
                            // There only should be two items
                            return null;
                        }
                        // Since it can contain many urls separated by comma's, we split it again and return the first one
                        return urls[1].Split(new char [] {','}, StringSplitOptions.RemoveEmptyEntries)[0];
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Reads the profiles from the DebugSettingsFile. Adds an error list entry and throws if an exception
        /// </summary>
        protected LaunchSettingsData ReadProfilesFromDisk()
        {
            // Clear errors
            ProjectErrorManager.ClearErrorsForOwner(ErrorOwnerString);
            try
            {
                string jsonString = FileManager.ReadAllText(DebugSettingsFile);

                var launchSettingsData = JsonConvert.DeserializeObject<LaunchSettingsData>(jsonString);
                RemoveInvalidProfilesAndLogErrors(launchSettingsData);

                // Remember the time we are sync'd to
                LastSettingsFileSyncTime = FileManager.LastFileWriteTime(DebugSettingsFile);
                return launchSettingsData;
            }
            catch(JsonReaderException readerEx)
            {
                string err = string.Format(Resources.JsonErrorReadingDebugSettings,  readerEx.Message);
                ProjectErrorManager.AddError(ErrorOwnerString, err, DebugSettingsFile, readerEx.LineNumber, readerEx.LinePosition, false);
                throw;
            }
            catch(JsonException jsonEx)
            {
                string err = string.Format(Resources.JsonErrorReadingDebugSettings,  jsonEx.Message);
                ProjectErrorManager.AddError(ErrorOwnerString, err, DebugSettingsFile, ProjectConstants.UndefinedLineOrColumn, ProjectConstants.UndefinedLineOrColumn, false);
                throw;
            }
            catch(Exception ex)
            {
                string err = string.Format(Resources.ErrorReadingDebugSettings, ProjectConstants.ProjectRelativeDebugSettingsFile, ex.Message);
                ProjectErrorManager.AddError(ErrorOwnerString, err, false);
                throw;
            }
        }

        /// <summary>
        /// Does a quick validation to make sure at least a name is present in each profile. Removes bad ones and
        /// logs errors.
        /// </summary>
        private void RemoveInvalidProfilesAndLogErrors(LaunchSettingsData profilesData)
        {
            if(profilesData.Profiles == null)
            {
                return;
            }

            bool logError = false;
            List<DebugProfileData> validProfiles = new List<DebugProfileData>();
            foreach(var kvp in profilesData.Profiles)
            {
                if(string.IsNullOrWhiteSpace(kvp.Key))
                {
                    logError = true;
                }
                else
                {
                    // We need to set the name and return this one
                    kvp.Value.Name = kvp.Key;
                    validProfiles.Add(kvp.Value);
                }
            }
            if(logError )
            {
                ProjectErrorManager.AddError(ErrorOwnerString, Resources.ProfileMissingName, false);
            }

            // Remove duplicates, if any, and update the launch settings with only valid entries.  Dupes are possible because the 
            // json serializer uses a ordinal string comparer
            var updatedProfiles = new Dictionary<string, DebugProfileData>(StringComparer.OrdinalIgnoreCase);
            for(int i = 0; i< validProfiles.Count; i++)
            {
                // Add will return false if item has alrady been added
                if(updatedProfiles.ContainsKey(validProfiles[i].Name))
                {
                    ProjectErrorManager.AddError(ErrorOwnerString, string.Format(Resources.DuplicateProfileRemoved, validProfiles[i].Name), false);
                }
                else
                {
                    updatedProfiles.Add(validProfiles[i].Name, validProfiles[i]);
                }
            }

            // TODO: Validate IIS Express settings if any

            profilesData.Profiles = updatedProfiles;
        }

        /// <summary>
        /// Saves the launch settings to the launch settings file. Adds an errorstring and throws if an exception. Note
        /// that the caller is responsible for checking out the file
        /// </summary>
        protected void SaveSettingsToDisk(ILaunchSettings newSettings)
        {
           // Clear errors
            ProjectErrorManager.ClearErrorsForOwner(ErrorOwnerString);
            LaunchSettingsData serializationData = GetSettingsToSerialize(newSettings);
            try
            {
                EnsurePropertiesFolder();

                // Don't bother writing null values
                JsonSerializerSettings settings = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore};
                string jsonString = JsonConvert.SerializeObject(serializationData, Formatting.Indented, settings);

                // TODO: Should save the text in a non-destructive way via a temp flie.
                IgnoreFileChanges = true;
                FileManager.WriteAllText(DebugSettingsFile, jsonString);

                // Remmeber the last write time
                LastSettingsFileSyncTime = FileManager.LastFileWriteTime(DebugSettingsFile);
            }
            catch(Exception ex)
            {
                string err = string.Format(Resources.ErrorWritingDebugSettings, ProjectConstants.ProjectRelativeDebugSettingsFile, ex.Message);
                ProjectErrorManager.AddError(ErrorOwnerString, err, false);
                throw;
            }
            finally
            {
                IgnoreFileChanges = false;
            }
        }

        /// <summary>
        /// Gets the serialization object for the set of profiles and settings. It filters out built in profiles for which  
        /// there hasn't been any user customization
        /// </summary>
        protected LaunchSettingsData GetSettingsToSerialize(ILaunchSettings curSettings)
        {
            Dictionary<string, DebugProfileData> profileData = new Dictionary<string,DebugProfileData>();
            foreach(var profile in curSettings.Profiles)
            {
                if(profile.ProfileShouldBePersisted())
                {
                    profileData.Add(profile.Name, DebugProfileData.FromIDebugProfile(profile));
                }
            }

            // Only write IIS Settings if there is something meaningful
            IISSettingsData iisSettings = null;
            if(curSettings.IISSettings != null && !IISSettings.IsEmptySettings(curSettings.IISSettings))
            {
                iisSettings = IISSettingsData.FromIIISSettings(curSettings.IISSettings);
            }
            return new LaunchSettingsData() { Profiles = profileData, IISSettings = iisSettings };
        }

        /// <summary>
        /// Helper to check out the debugsettings.json file
        /// </summary>
        protected async Task CheckoutSettingsFileAsync()
        {
            if (SourceControlIntegration!= null && SourceControlIntegration.Value != null)
            {
                await SourceControlIntegration.Value.CanChangeProjectFilesAsync(new[] { DebugSettingsFile });
            }
        }

        /// <summary>
        /// Called when new project information is received. This is done to ensure there is a profile for each command in the
        /// project.json file. In general it is additive, however, it never remove existing profiles when commands are removed and
        /// those profiles have not be customized in some way.  It returns true if  it made any changes to the list.
        /// It makes sure the BuiltInCommand property is set correctly. It will also removethe executable path from built in 
        /// command profiles including IIS express. Most of these changes don't affect persistence as we don't normally persist 
        /// built in commands
        /// </summary>
        protected bool ProcessProjectInformationChanged(List<DebugProfile> existingProfiles, IProjectInformation projectData)
        {
            bool madeChange = false;
            // If no project data do nothing
            if(projectData == null)
            {
                return madeChange;
            }

            Debug.Assert(existingProfiles != null);

            // Get the list of commands. Then remove all the ones for which we already have a profile. What's left are those
            // that need to be added.
            List<KeyValuePair<string, string>> newCommands  = new List<KeyValuePair<string, string>>(projectData.Commands);
            for(int i =0; i < existingProfiles.Count; i++)
            {
                var profile = existingProfiles[i];
                var foundCmd = newCommands.FirstOrDefault(s => {return DebugProfile.IsSameProfileName(profile.Name, s.Key);});
                if(foundCmd.Key != null)
                {
                    newCommands.Remove(foundCmd);
                    // Make sure the profile is marked correctly. Just ignore (silently remove) the exe path. Consider adding
                    // a warning to the error list for this. Note that cmd's will replace any default "Project" with the same name that
                    // might have been added. This is OK as only dnx projects will have commands. Once support for commands is removed this all 
                    // goes away
                    if(profile.ExecutablePath != null || profile.Kind != ProfileKind.BuiltInCommand)
                    {
                        profile.Kind = ProfileKind.BuiltInCommand;
                        profile.ExecutablePath = null;
                        profile.CommandName = profile.Name;
                        madeChange = true;
                    }
                    madeChange = ApplyWebServerDataToProfileIfNeeded(profile, foundCmd.Key, foundCmd.Value, isNewProfile: false);
                }
                else
                {
                    // If was a built in command but isn't any longer. If the profile has not been customer (ShouldBePersisted is false)
                    // remove it, otherwise, change to a CustomCommand and set the commandto run to be the profile's name since that 
                    // is what it would have done before.
                   if(profile.Kind == ProfileKind.BuiltInCommand)
                   {
                        if(profile.ProfileShouldBePersisted())
                        {
                            profile.Kind = ProfileKind.CustomizedCommand;
                            if(profile.CommandName == null)
                            {
                                profile.CommandName = profile.Name;
                            }
                        }
                        else
                        {
                            // Remove it and adjust index
                            existingProfiles.RemoveAt(i);
                            --i;
                        }
                        madeChange = true;
                   }
                }
            }

            if(newCommands.Count > 0)
            {
                // Add all the new commands. Doesn't require a persist to disk but we do need to indicate a change
                existingProfiles.AddRange(newCommands.Select(cmd => CreateProfileForCommand(cmd.Key, cmd.Value)));
                madeChange = true;
            }

            return madeChange;
        }

        /// <summary>
        /// Handler for when the Launch settings file changes. Actually, we watch the project root so any
        /// file with the name LaunchSettings.json. We don't need to special case because, if a file with this name
        /// changes we will only check if the one we cared about was modified.
        /// </summary>
        protected void LaunchSettingsFile_Changed(object sender, FileSystemEventArgs e)
        {
            
            if(!IgnoreFileChanges)
            {
                // Only do something if the file is truly different than what we synced. Here, we want to 
                // throttle. 
                if(!FileManager.FileExists(DebugSettingsFile) || FileManager.LastFileWriteTime(DebugSettingsFile) != LastSettingsFileSyncTime)
                {
                    FileChangeScheduler.ScheduleAsyncTask(async token => 
                    {

                        if(token.IsCancellationRequested)
                        {
                            return;
                        }

                        var snapshot = CurrentSnapshot;
                        string activeProfile = snapshot ?.ActiveProfileName;
                        var projectSnapshot = Subscriptions.ProjectInformation.CurrentSnapshot;

                        // Always pass null for current snapshot to force a refresh from disk
                        await Task.Run(() => UpdateProfiles(null, activeProfile, projectSnapshot == null? null : projectSnapshot.Value));
                    });
                }
            }
        }

        /// <summary>
        /// Makes sure the properties folder exists on disk. Doesn't add the folder to
        /// the project.
        /// </summary>
        protected void EnsurePropertiesFolder()
        {
            string propertiesFolder = Path.Combine(UnconfiguredDotNetProject.ProjectFolder, "Properties");
            if(!FileManager.DirectoryExists(propertiesFolder))
            {
                FileManager.CreateDirectory(propertiesFolder);
            }
        }

        /// <summary>
        /// Cleans up our watcher on the debugsettings.Json file
        /// </summary>
        private void CleanupFileWatcher()
        {               
            if(FileWatcher != null)
            {
                FileWatcher.Dispose();
                FileWatcher = null;
            }
        }

        /// <summary>
        /// Sets up a file system watcher to look for changes to the launchsettings.json file. It watches at the root of the 
        /// project oltherwise we force the project to have a properties folder.
        /// </summary>
        private void WatchLaunchSettingsFile()
        {
            if(FileWatcher == null)
            {
                // Create our scheduler for processing file chagnes
                FileChangeScheduler = new TaskDelayScheduler(FileChangeProcessingDelay, 
                    UnconfiguredDotNetProject.UnconfiguredProjectAsynchronousTasksService.UnloadCancellationToken, 
                    ThreadHandling,
                    "DebugSettingsFileUpdater");

                FileWatcher = new SimpleFileWatcher(UnconfiguredDotNetProject.ProjectFolder, 
                                                    true, 
                                                    NotifyFilters.FileName | NotifyFilters.Size |  NotifyFilters.LastWrite, 
                                                    ProjectConstants.LaunchSettingsFilename, 
                                                    LaunchSettingsFile_Changed, 
                                                    LaunchSettingsFile_Changed);
            }
        }

        /// <summary>
        /// Need to amke sure we cleanup the dataflow links and file watcher
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                CleanupFileWatcher();
                if(FileChangeScheduler != null)
                {
                    FileChangeScheduler.Dispose();
                    FileChangeScheduler = null;
                }

                if(SubscriptionLink != null)
                {
                    SubscriptionLink.Dispose();
                    SubscriptionLink = null;
                }

                if(_broadcastBlock != null)
                {
                    _broadcastBlock.Complete();
                }
            }
        }

        /// <summary>
        /// Replaces the current set of profiles with the contents of profiles. If changes were
        /// made, the file will be checked out and saved.
        /// </summary>
        public async Task UpdateAndSaveSettingsAsync(ILaunchSettings newSettings)
        {
            // See whether we need to do anything or not
            var snapshot = CurrentSnapshot;
            bool setActiveProfile = snapshot == null? true : !DebugProfile.IsSameProfileName(snapshot.ActiveProfileName, newSettings.ActiveProfileName);
            bool detectedChanges = snapshot == null || snapshot.ProfilesAreDifferent(newSettings.Profiles) || snapshot.IISSettingsAreDifferent(newSettings.IISSettings);

            if(detectedChanges || setActiveProfile)
            {
                // Make sure the profiles are copied. We don't want them to mutate.
                ILaunchSettings newSnapshot = new LaunchSettings(newSettings.Profiles, true, newSettings.IISSettings, newSettings.ActiveProfileName);

                // Being saved and changeMade are different since the active profile change does not require them to be saved.
                if(detectedChanges)
                {
                    await CheckoutSettingsFileAsync();

                    SaveSettingsToDisk(newSettings);
                }

                FinishUpdate(newSnapshot, true);
            }
        }

        /// <summary>
        /// This function blocks until a snapshot is available. It will return null if the timeout occurs
        /// prior to the snapshot is available
        /// </summary>
        public async Task<ILaunchSettings> WaitForFirstSnapshot(CancellationToken token, int timeout)
        {
            if(CurrentSnapshot != null)
            {
                return CurrentSnapshot;
            }
            await _firstSnapshotCompletionSource.Task.TryWaitForCompleteOrTimeout(timeout);
            return CurrentSnapshot;
        }
    }
}
