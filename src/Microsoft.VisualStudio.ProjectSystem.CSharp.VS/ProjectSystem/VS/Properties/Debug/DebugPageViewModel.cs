//--------------------------------------------------------------------------------------------
// DebugPageViewModel
//
// ViewModel for DebugPageControl
//
// Copyright(c) 2014 Microsoft Corporation
//--------------------------------------------------------------------------------------------

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using System.Windows;
    using System.Windows.Input;
    using Microsoft.VisualStudio.ProjectSystem.DotNet;
    using Microsoft.VisualStudio.ProjectSystem.DotNet.Debugger;
    using Microsoft.VisualStudio.ProjectSystem.DotNet.Utilities;
    using Microsoft.VisualStudio.ProjectSystem.VS.Debugger;

    internal class DebugPageViewModel : PropertyPageViewModel
    {
        private readonly string ExecutableFilter = String.Format("{0} (*.exe)|*.exe|{1} (*.*)|*.*", Resources.ExecutableFiles, Resources.AllFiles);
        private readonly IProjectThreadingService _threadingService;

        public event EventHandler ClearEnvironmentVariablesGridError;
        public event EventHandler FocusEnvironmentVariablesGridRow;

        private string _defaultDNXVersion;
        private IDisposable _debugProfileProviderLink;
        private bool _useTaskFactory = true;

        // This holds the set of shared IIS settings. This affects windows\anon auth (shared across iis\iisExpress) and
        // the IIS and IIS Express bindings. 
        private IISSettingsData _currentIISSettings;

        public DebugPageViewModel()
        {
        }

        // for unit testing
        internal DebugPageViewModel(bool useTaskFactory)
        {
            _useTaskFactory = useTaskFactory;
        }

        public string SelectedProfileDnxVersion
        {
            get
            {
                if (!IsProfileSelected)
                {
                    return null;
                }
                return SelectedDebugProfile.SDKVersion;
            }
            set
            {
                if (SelectedDebugProfile != null && SelectedDebugProfile.SDKVersion != value)
                {
                    SelectedDebugProfile.SDKVersion = value;
                    OnPropertyChanged(nameof(SelectedProfileDnxVersion));
                }
            }
        }

        private string selectedDnxVersion;
        public string SelectedDnxVersion
        {
            get
            {
                return selectedDnxVersion;
            }
            set
            {
                if (OnPropertyChanged(ref selectedDnxVersion, value))
                {
                    SelectedProfileDnxVersion = UseSpecificRuntime ? value : null;
                }
            }
        }

        private ObservableCollection<string> dnxVersions;
        public ObservableCollection<string> DnxVersions
        {
            get
            {
                return dnxVersions;
            }
            set
            {
                OnPropertyChanged(ref dnxVersions, value);
            }
        }

        private bool useSpecificRuntime;
        public bool UseSpecificRuntime
        {
            get
            {
                return useSpecificRuntime;
            }
            set
            {
                OnPropertyChanged(ref useSpecificRuntime, value);
            }
        }

        public string SelectedCommandName
        {
            get
            {
                if (!IsProfileSelected)
                {
                    return string.Empty;
                }

                return SelectedDebugProfile.CommandName;
            }
            set
            {
                if (SelectedDebugProfile != null && SelectedDebugProfile.CommandName != value)
                {
                    SelectedDebugProfile.CommandName = value;
                    OnPropertyChanged(nameof(SelectedCommandName));
                }
            }
        }

        private ObservableCollection<string> commandNames;
        public ObservableCollection<string> CommandNames
        {
            get
            {
                return commandNames;
            }
            set
            {
                OnPropertyChanged(ref commandNames, value);
            }
        }

        /// <summary>
        /// Where the data is bound depends on the currently active profile. IIS\IIS Express have specific binding information
        /// in the IISSEttings. However, profiles (web based) have an ApplicationUrl property
        /// </summary>
        public string ApplicationUrl
        {
            get
            {
                if(SelectedDebugProfile != null)
                {
                    if(SelectedDebugProfile.Kind == ProfileKind.IISExpress)
                    {
                        return _currentIISSettings ?.IISExpressBindingData ?.ApplicationUrl;
                    }
                    else if(SelectedDebugProfile.Kind == ProfileKind.IIS)
                    {
                        return _currentIISSettings ?.IISBindingData ?.ApplicationUrl;
                    }
                    else if(SelectedDebugProfile.IsWebServerCmdProfile)
                    {
                        return SelectedDebugProfile.ApplicationUrl;
                    }
                }
                return string.Empty;
            }
            set
            {
                if (value != ApplicationUrl)
                {
                    if(SelectedDebugProfile != null)
                    {
                        if(SelectedDebugProfile.Kind == ProfileKind.IISExpress)
                        {
                            if(_currentIISSettings != null && _currentIISSettings.IISExpressBindingData != null)
                            {
                                _currentIISSettings.IISExpressBindingData.ApplicationUrl = value;
                                OnPropertyChanged(nameof(ApplicationUrl));
                            }
                        }
                        else if(SelectedDebugProfile.Kind == ProfileKind.IIS)
                        {
                            if(_currentIISSettings != null && _currentIISSettings.IISBindingData != null)
                            {
                                _currentIISSettings.IISBindingData.ApplicationUrl = value;
                                OnPropertyChanged(nameof(ApplicationUrl));
                            }
                        }
                        else if(SelectedDebugProfile.IsWebServerCmdProfile)
                        {
                            SelectedDebugProfile.ApplicationUrl = value;
                            OnPropertyChanged(nameof(ApplicationUrl));
                        }
                    }
                }
            }
        }

        private List<LaunchType> launchTypes;
        public List<LaunchType> LaunchTypes
        {
            get
            {
                return launchTypes;
            }
            set
            {
                OnPropertyChanged(ref launchTypes, value);
            }
        }


        private LaunchType selectedLaunchType;
        public LaunchType SelectedLaunchType
        {
            get
            {
                return selectedLaunchType;
            }
            set
            {
                if (OnPropertyChanged(ref selectedLaunchType, value))
                {
                    // Need to deal with dnx commands. Existing commands are shown in the UI as project
                    // since that is what is run when that command is selected. However, we don't want to just update the actual
                    // profile to this value - we want to treat them as equivalent.
                    if (selectedLaunchType != null && !IsEquivalentProfileKind(SelectedDebugProfile, selectedLaunchType.Kind))
                    {
                        SelectedDebugProfile.Kind = selectedLaunchType.Kind;
                        if (selectedLaunchType.Kind == ProfileKind.Executable)
                        {
                            ExecutablePath = String.Empty;
                            SelectedCommandName = null;
                            SelectedProfileDnxVersion = null;
                        }
                        else if (selectedLaunchType.Kind == ProfileKind.IISExpress)
                        {
                            ExecutablePath = String.Empty;
                            //SelectedCommandName = LaunchSettingsProvider.IISExpressProfileCommandName;
                            HasLaunchOption = true;
                        }
                        else
                        {
                            SelectedCommandName = CommandNames[0];
                            ExecutablePath = null;
                        }

                        OnPropertyChanged(nameof(IsCommand));
                        OnPropertyChanged(nameof(IsExecutable));
                        OnPropertyChanged(nameof(IsProject));
                        OnPropertyChanged(nameof(IsIISExpress));
                        OnPropertyChanged(nameof(ShowVersionSelector));
                    }
                }
            }
        }
        
        /// <summary>
        /// More hackery to deal with dnx commands. And existing commands is shown in the UI as project
        /// since that is what is run when that command is selected. However, we don't want tojust update the actual
        /// profile to this value - we want to treat them as equivalent.
        //</summary>
        private bool IsEquivalentProfileKind(DebugProfile profile, ProfileKind kind)
        {
            
            if(kind == ProfileKind.Project)
            {
                return profile.Kind == ProfileKind.Project || 
                       profile.Kind == ProfileKind.BuiltInCommand || 
                       profile.Kind == ProfileKind.CustomizedCommand;
            }

            return profile.Kind == kind;
        }

        public bool IsBuiltInProfile
        {
            get
            {
                if (!IsProfileSelected)
                {
                    return false;
                }

                return SelectedDebugProfile.Kind == ProfileKind.BuiltInCommand;
            }
        }

        public string CommandLineArguments
        {
            get
            {
                if (!IsProfileSelected)
                {
                    return string.Empty;
                }

                return SelectedDebugProfile.CommandLineArgs;
            }
            set
            {
                if (SelectedDebugProfile != null && SelectedDebugProfile.CommandLineArgs != value)
                {
                    SelectedDebugProfile.CommandLineArgs = value;
                    OnPropertyChanged(nameof(CommandLineArguments));
                }
            }
        }

        public string ExecutablePath
        {
            get
            {
                if (!IsProfileSelected)
                {
                    return string.Empty;
                }
            
                return SelectedDebugProfile.ExecutablePath;
            }
            set
            {
                if (SelectedDebugProfile != null && SelectedDebugProfile.ExecutablePath != value)
                {
                    SelectedDebugProfile.ExecutablePath = value;
                    OnPropertyChanged(nameof(ExecutablePath));
                }
            }
        }

        public string LaunchPage
        {
            get
            {
                if (!IsProfileSelected)
                {
                    return string.Empty;
                }

                return SelectedDebugProfile.LaunchUrl;
            }
            set
            {
                if (SelectedDebugProfile != null && SelectedDebugProfile.LaunchUrl != value)
                { 
                    SelectedDebugProfile.LaunchUrl = value;
                    OnPropertyChanged(nameof(LaunchPage));
                }
            }
        }

        public string WorkingDirectory
        {
            get
            {
                if (!IsProfileSelected)
                {
                    return string.Empty;
                }
            
                return SelectedDebugProfile.WorkingDirectory;
            }
            set
            {
                if (SelectedDebugProfile != null && SelectedDebugProfile.WorkingDirectory != value)
                {
                    SelectedDebugProfile.WorkingDirectory = value;
                    OnPropertyChanged(nameof(WorkingDirectory));
                }
            }
        }

        public bool HasLaunchOption
        {
            get
            {
                if (!IsProfileSelected)
                {
                    return false;
                }

                return SelectedDebugProfile.LaunchBrowser;
            }
            set
            {
                if (SelectedDebugProfile != null && SelectedDebugProfile.LaunchBrowser != value)
                {
                    SelectedDebugProfile.LaunchBrowser = value;
                    OnPropertyChanged(nameof(HasLaunchOption));
                }
            }
        }

        public bool IsExecutable
        {
            get
            {
                if (SelectedLaunchType == null)
                {
                    return false;
                }

                return SelectedLaunchType.Kind == ProfileKind.Executable;
            }
        }

        public bool IsProject
        {
            get
            {
                if (SelectedLaunchType == null)
                {
                    return false;
                }

                return SelectedLaunchType.Kind == ProfileKind.Project;
            }
        }

        public bool IsCommand
        {
            get
            {
                if (SelectedLaunchType == null)
                {
                    return false;
                }

                return SelectedLaunchType.Kind == ProfileKind.BuiltInCommand || SelectedLaunchType.Kind == ProfileKind.CustomizedCommand;
            }
        }

        public bool IsProfileSelected
        {
            get
            {
                return SelectedDebugProfile != null;
            }
        }

        public bool IsIISExpress
        {
            get
            {
                if (!IsProfileSelected)
                {
                    return false;
                }

                return SelectedDebugProfile.Kind == ProfileKind.IISExpress;
            }
        }

        public bool IsIISOrIISExpress
        {
            get
            {
                if (!IsProfileSelected)
                {
                    return false;
                }

                return SelectedDebugProfile.Kind == ProfileKind.IIS || SelectedDebugProfile.Kind == ProfileKind.IISExpress;
            }
        }

        public bool IsWebProfile
        {
            get
            {
                if (!IsProfileSelected)
                {
                    return false;
                }

#if ENABLE_SELFHOSTSERVER                
                SelectedDebugProfile.IsWebBasedProfile;
#else
                return IsIISOrIISExpress;
#endif
            }
        }

        public bool ShowVersionSelector
        {
            get
            {
                //return IsDnxProject && !IsExecutable;
                return false; //TODO: DebugProp 
            }
        }
        public bool IsCustomType
        {
            get
            {
                if (SelectedLaunchType == null || SelectedDebugProfile == null)
                {
                    return false;
                }

                return SelectedLaunchType.Kind == ProfileKind.CustomizedCommand || 
                       SelectedLaunchType.Kind == ProfileKind.Executable ||
                       SelectedLaunchType.Kind == ProfileKind.IIS ||
                       (SelectedDebugProfile.Kind == ProfileKind.IISExpress && !SelectedDebugProfile.IsDefaultIISExpressProfile) ||
                       (SelectedDebugProfile.Kind == ProfileKind.Project && !DebugProfile.IsSameProfileName(SelectedDebugProfile.Name, ""//UnconfiguredDotNetProject.ProjectName
                       ));
            }
        }

        private ObservableCollection<DebugProfile> debugProfiles;
        public ObservableCollection<DebugProfile> DebugProfiles
        {
            get
            {
                return debugProfiles;
            }
            set
            {
                var oldProfiles = debugProfiles;
                if (OnPropertyChanged(ref debugProfiles, value))
                { 
                    if (oldProfiles != null)
                    {
                        oldProfiles.CollectionChanged -= DebugProfiles_CollectionChanged;
                    }
                    if (debugProfiles != null)
                    {
                        debugProfiles.CollectionChanged += DebugProfiles_CollectionChanged;
                    }
                    OnPropertyChanged(nameof(HasProfiles));
                    OnPropertyChanged(nameof(NewProfileEnabled));
                }
            }
        }

        private void DebugProfiles_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasProfiles));
        }

        public bool HasProfiles
        {
            get
            {
                return debugProfiles != null && debugProfiles.Count > 0;
            }
        }

        private DebugProfile selectedDebugProfile;
        public DebugProfile SelectedDebugProfile
        {
            get
            {
                return selectedDebugProfile;
            }
            set
            {
                if (selectedDebugProfile != value)
                {
                    var oldProfile = selectedDebugProfile;
                    selectedDebugProfile = value;
                    NotifySelectedChanged(oldProfile);
                }
            }
        }

        public bool SSLEnabled
        {
            get
            {
                if(_currentIISSettings != null && SelectedDebugProfile != null)
                {
                    if(SelectedDebugProfile.Kind == ProfileKind.IISExpress && _currentIISSettings.IISExpressBindingData != null)
                    {
                        return _currentIISSettings.IISExpressBindingData.SSLPort != 0;
                    }
                    else if(SelectedDebugProfile.Kind == ProfileKind.IIS && _currentIISSettings.IISBindingData != null)
                    {
                        return _currentIISSettings.IISBindingData.SSLPort != 0;
                    }
                }
                return false;
            }
            set
            {
                // When transitioning to enabled we want to go get an ssl port. Of course when loading (ignore events is true) we don't 
                // want to do this.
                if(value != SSLEnabled && !IgnoreEvents)
                {
                    ServerBindingData binding = null;
                    if(_currentIISSettings != null)
                    {
                        if(SelectedDebugProfile.Kind == ProfileKind.IISExpress && _currentIISSettings.IISExpressBindingData != null)
                        {
                            binding = _currentIISSettings.IISExpressBindingData;
                        }
                        else if(SelectedDebugProfile.Kind == ProfileKind.IIS && _currentIISSettings.IISBindingData != null)
                        {
                            binding = _currentIISSettings.IISBindingData;
                        }
                    }
                    if(binding != null)
                    {
                        // When setting we need to configure the port
                        if(value == true)
                        {
                            // If we are already have a port (say the guy was enabling\disabing over and over), use that (nothing to do here)
                            if(string.IsNullOrWhiteSpace(SSLUrl))
                            {
                                ValidateApplicationUrl();

                                // No existing value. Go get one and set the url
                                // First we must validate the ApplicationUrl is valid. W/O it we don't know the host header
                                if(SelectedDebugProfile.Kind == ProfileKind.IISExpress)
                                {
                                    // Get the SSLPort provider
                                    var sslPortProvider = GetSSLPortProvider();
                                    if(sslPortProvider != null)
                                    {
                                        binding.SSLPort = sslPortProvider.GetAvailableSSLPort(ApplicationUrl);
                                    }
                                    else
                                    {
                                        // Just set it to a default iis express value
                                        binding.SSLPort = 44300; 
                                    }
                                }
                                else
                                {
                                    //For IIS use 443 as the binding
                                    binding.SSLPort = 443;
                                }
                            }
                            else
                            {
                                binding.SSLPort = new Uri(SSLUrl).Port;
                            }
                        }
                        else
                        {   
                            // Just clear the port. We don't clear the SSL url so that it persists (disabled) until we update.
                            binding.SSLPort = 0;
                        }
                    }
                }
                OnPropertyChanged(nameof(SSLEnabled));
                OnPropertyChanged(nameof(SSLUrl));
            }
        }

        /// <summary>
        /// This property is synthesized from the SSLPort changing.
        /// </summary>
        public string SSLUrl
        {
            get
            {
                int sslPort = 0;
                if(_currentIISSettings != null && SelectedDebugProfile != null)
                {
                    if(SelectedDebugProfile.Kind == ProfileKind.IISExpress && _currentIISSettings.IISExpressBindingData != null)
                    {
                        sslPort = _currentIISSettings.IISExpressBindingData.SSLPort;
                    }
                    else if(SelectedDebugProfile.Kind == ProfileKind.IIS && _currentIISSettings.IISBindingData != null)
                    {
                        sslPort = _currentIISSettings.IISBindingData.SSLPort;
                    }
                    
                    if(sslPort != 0)
                    {
                        try
                        {
                            // Application url could be bad so we need to protect ourself.
                            if(!string.IsNullOrWhiteSpace(ApplicationUrl))
                            {
                                return UriUtilities.MakeSecureUrl(ApplicationUrl, sslPort);
                            }
                        }
                        catch
                        {
                        }
                   }
                }
                return string.Empty;
            }
        }

        /// <summary>
        /// IIS\IISExpress specific property (shared between them) and stored in  _currentIISSetings 
        /// </summary>
        public bool AnonymousAuthenticationEnabled
        {
            get
            {
                if(_currentIISSettings == null)
                {
                    return false;
                }
                return _currentIISSettings.AnonymousAuthentication;
            }
            set
            {
                if(_currentIISSettings != null && _currentIISSettings.AnonymousAuthentication != value)
                {
                    _currentIISSettings.AnonymousAuthentication = value;
                    OnPropertyChanged(nameof(AnonymousAuthenticationEnabled));
                }
            }
        }

        /// <summary>
        /// IIS\IISExpress specific property (shared between them) and stored in  _currentIISSetings 
        /// </summary>
        public bool WindowsAuthenticationEnabled
        {
            get
            {
                if(_currentIISSettings == null)
                {
                    return false;
                }
                return _currentIISSettings.WindowsAuthentication;
            }
            set
            {
                if(_currentIISSettings != null && _currentIISSettings.WindowsAuthentication != value)
                {
                    _currentIISSettings.WindowsAuthentication = value;
                    OnPropertyChanged(nameof(WindowsAuthenticationEnabled));
                }
            }
        }

        private bool _removeEnvironmentVariablesRow;
        public bool RemoveEnvironmentVariablesRow
        {
            get
            {
                return _removeEnvironmentVariablesRow;
            }
            set
            {
                OnPropertyChanged(ref _removeEnvironmentVariablesRow, value, suppressInvalidation: true);
            }
        }

        private int _environmentVariablesRowSelectedIndex = -1;
        public int EnvironmentVariablesRowSelectedIndex
        {
            get { return _environmentVariablesRowSelectedIndex; }
            set
            {
                if (_environmentVariablesRowSelectedIndex != value)
                {
                    _environmentVariablesRowSelectedIndex = value;
                    if (_environmentVariablesRowSelectedIndex == -1)
                    {
                        //No selected item - Disable Remove button
                        RemoveEnvironmentVariablesRow = false;
                    }
                    else
                    {
                        RemoveEnvironmentVariablesRow = (EnvironmentVariablesValid) ? true : ((EnvironmentVariables[_environmentVariablesRowSelectedIndex] as NameValuePair).HasValidationError == true);
                    }

                    OnPropertyChanged(nameof(EnvironmentVariablesRowSelectedIndex), suppressInvalidation: true);
                }
            }
        }

        private bool _environmentVariablesValid = true;
        public bool EnvironmentVariablesValid
        {
            get
            {
                if (EnvironmentVariables == null) { return true; }
                else { return _environmentVariablesValid; }
            }
            set
            {
                if (_environmentVariablesValid != value)
                {
                    _environmentVariablesValid = value;
                    if (value == true && ClearEnvironmentVariablesGridError != null)
                    {
                        ClearEnvironmentVariablesGridError.Invoke(this, EventArgs.Empty);
                    }
                    OnPropertyChanged(nameof(EnvironmentVariablesValid), suppressInvalidation: true);
                }
            }
        }

        private ObservableList<NameValuePair> environmentVariables;
        public ObservableList<NameValuePair> EnvironmentVariables
        {
            get
            {
                return environmentVariables;
            }
            set
            {
                OnPropertyChanged(ref environmentVariables, value);
            }
        }

        protected virtual void NotifySelectedChanged(DebugProfile oldProfile)
        {
            // we need to keep the property page control from setting IsDirty when we are just switching between profiles.
            // we still need to notify the display of the changes though
            PushIgnoreEvents();
            try
            {
        
                // these have no backing store in the viewmodel, we need to send notifications when we change selected profiles
                // consider a better way of doing this
                OnPropertyChanged(nameof(SelectedDebugProfile));
                OnPropertyChanged(nameof(IsBuiltInProfile));
                OnPropertyChanged(nameof(IsIISExpress));
                OnPropertyChanged(nameof(IsIISOrIISExpress));
                OnPropertyChanged(nameof(IsWebProfile));
                OnPropertyChanged(nameof(CommandLineArguments));
                OnPropertyChanged(nameof(ExecutablePath));
                OnPropertyChanged(nameof(LaunchPage));
                OnPropertyChanged(nameof(HasLaunchOption));
                OnPropertyChanged(nameof(WorkingDirectory));
                OnPropertyChanged(nameof(SelectedCommandName));

                OnPropertyChanged(nameof(SSLEnabled));
                OnPropertyChanged(nameof(SSLUrl));
                OnPropertyChanged(nameof(ApplicationUrl));
                OnPropertyChanged(nameof(WindowsAuthenticationEnabled));
                OnPropertyChanged(nameof(AnonymousAuthenticationEnabled));


                SetLaunchType();

                OnPropertyChanged(nameof(IsCustomType));
                OnPropertyChanged(nameof(IsCommand));
                OnPropertyChanged(nameof(IsExecutable));
                OnPropertyChanged(nameof(IsProject));
                OnPropertyChanged(nameof(IsProfileSelected));
                OnPropertyChanged(nameof(DeleteProfileEnabled));
                OnPropertyChanged(nameof(ShowVersionSelector));

                if (oldProfile != null && !UseSpecificRuntime)
                {
                    oldProfile.SDKVersion = null;
                }

                UseSpecificRuntime = !string.IsNullOrEmpty(SelectedProfileDnxVersion);
                if (!IsProfileSelected)
                {
                    SelectedDnxVersion = null;
                }
                else
                {
                    SelectedDnxVersion = UseSpecificRuntime ? SelectedProfileDnxVersion : _defaultDNXVersion;
                }

                SetEnvironmentGrid(oldProfile);

                UpdateActiveProfile();
            }
            finally
            {
                PopIgnoreEvents();
            }
        }

        private void UpdateActiveProfile()
        {
            // need to set it dirty so Apply() actually saves the profile
            // Billhie: this causes hangs. Disabling for now
            //if (this.ParentControl != null)
            //{
            //    this.ParentControl.IsDirty = true;
            //    WaitForAsync<int>(this.ParentControl.Apply);
            //}
        }

        /// <summary>
        /// Functions which actually does the save of the settings. Persists the changes to the launch settings
        /// file and configures IIS if needed.
        /// </summary>
        public async virtual Task SaveLaunchSettings()
        {
            ILaunchSettingsProvider provider = GetDebugProfileProvider();
            if (EnvironmentVariables != null && EnvironmentVariables.Count > 0)
            {
                SelectedDebugProfile.MutableEnvironmentVariables = EnvironmentVariables.CreateDictionary();
            }
            else if (SelectedDebugProfile != null)
            {
                SelectedDebugProfile.MutableEnvironmentVariables = null;
            }

            SelectedProfileDnxVersion = UseSpecificRuntime ? SelectedDnxVersion : null;

            await provider.UpdateAndSaveSettingsAsync(new LaunchSettings(DebugProfiles, false, GetIISSettings(), SelectedDebugProfile != null ? SelectedDebugProfile.Name : null));

        }

        private void SetEnvironmentGrid(DebugProfile oldProfile)
        {
            if (EnvironmentVariables != null && oldProfile != null)
            {
                if (environmentVariables.Count > 0)
                {
                    oldProfile.MutableEnvironmentVariables = EnvironmentVariables.CreateDictionary();
                }
                else
                {
                    oldProfile.MutableEnvironmentVariables = null;
                }
                EnvironmentVariables.ValidationStatusChanged -= EnvironmentVariables_ValidationStatusChanged;
                EnvironmentVariables.CollectionChanged -= EnvironmentVariables_CollectionChanged;
                ((INotifyPropertyChanged)EnvironmentVariables).PropertyChanged -= DebugPageViewModel_EnvironmentVariables_PropertyChanged;
            }

            if (SelectedDebugProfile != null && SelectedDebugProfile.MutableEnvironmentVariables != null)
            {
                EnvironmentVariables = SelectedDebugProfile.MutableEnvironmentVariables.CreateList();
            }
            else
            {
                EnvironmentVariables = new ObservableList<NameValuePair>();
            }
            EnvironmentVariables.ValidationStatusChanged += EnvironmentVariables_ValidationStatusChanged;
            EnvironmentVariables.CollectionChanged += EnvironmentVariables_CollectionChanged;
            ((INotifyPropertyChanged)EnvironmentVariables).PropertyChanged += DebugPageViewModel_EnvironmentVariables_PropertyChanged;
        }

        private void EnvironmentVariables_ValidationStatusChanged(object sender, EventArgs e)
        {
            ValidationStatusChangedEventArgs args = e as ValidationStatusChangedEventArgs;
            EnvironmentVariablesValid = args.ValidationStatus;
        }

        /// <summary>
        /// Called whenever the debug targets change. Note that after a save this function will be
        /// called. It looks for changes and applies them to the UI as needed. Switching profiles
        /// will also cause this to change as the active profile is stored in profiles snaphost.
        /// </summary>
        internal virtual void InitializeDebugTargetsCore(ILaunchSettings profiles)
        {
            bool profilesChanged = true;
            bool IISSettingsChanged = true;

            // Since this get's reentered if the user saves or the user switches active profiles.
            if (DebugProfiles != null)
            {
                profilesChanged = profiles.ProfilesAreDifferent(DebugProfiles.Select(p => (IDebugProfile)p).ToList());
                IISSettingsChanged = profiles.IISSettingsAreDifferent(GetIISSettings());
                if (!profilesChanged && !IISSettingsChanged)
                {
                    return;
                }
            }
            
            try
            {
                // This should never change the dirty state
                PushIgnoreEvents();

                if(profilesChanged)
                {
                    // Remember the current selection
                    string curProfileName = SelectedDebugProfile == null ? null : SelectedDebugProfile.Name;

                    // Load debug profiles
                    var debugProfiles = new ObservableCollection<DebugProfile>();

                    foreach (var profile in profiles.Profiles)
                    {
                        // Don't show the dummy NoAction profile
                        if (profile.Kind != ProfileKind.NoAction)
                        {
                            var newProfile = new DebugProfile(profile);
                            debugProfiles.Add(newProfile);
                        }
                    }

                    CommandNames = new ObservableCollection<string>(debugProfiles.Where(p => p.Kind == ProfileKind.BuiltInCommand).Select(pr => pr.CommandName));

                    DebugProfiles = debugProfiles;

                    // If we have a selection, we want to leave it as is
                    if (curProfileName == null || profiles.Profiles.FirstOrDefault(p => { return DebugProfile.IsSameProfileName(p.Name, curProfileName); }) == null)
                    {
                        // Note that we have to be careful since the collection can be empty. 
                        if (!string.IsNullOrEmpty(profiles.ActiveProfileName))
                        {
                            SelectedDebugProfile = DebugProfiles.Where((p) => DebugProfile.IsSameProfileName(p.Name, profiles.ActiveProfileName)).Single();
                        }
                        else
                        {
                            if (debugProfiles.Count > 0)
                            {
                                SelectedDebugProfile = debugProfiles[0];
                            }
                            else
                            {
                                SetEnvironmentGrid(null);
                            }
                        }
                    }
                    else
                    {
                        SelectedDebugProfile = DebugProfiles.Where((p) => DebugProfile.IsSameProfileName(p.Name, curProfileName)).Single();
                    }
                }
                if(IISSettingsChanged)
                {
                    InitializeIISSettings(profiles.IISSettings);
                }
            }
            finally
            {
                PopIgnoreEvents();
            }
        }

        /// <summary>
        /// Initializes from the set of debug targets. It also hooks into debug provider so that it can update when the profile changes
        /// </summary>
        protected virtual void InitializeDebugTargets()
        {
            if(_debugProfileProviderLink == null)
            {
                var debugProfilesBlock = new ActionBlock<ILaunchSettings>(
                async (profiles) =>
                {
                    if (_useTaskFactory)
                    {
                       await _threadingService.SwitchToUIThread();
                    }
                    InitializeDebugTargetsCore(profiles);
                 });

                var profileProvider = GetDebugProfileProvider();
                _debugProfileProviderLink = profileProvider.SourceBlock.LinkTo(
                    debugProfilesBlock,
                    linkOptions: new DataflowLinkOptions { PropagateCompletion = true });
            }
        }

        public async override Task Initialize()
        {
            /* //TODO: DebugProp 
            // Need to set whether this is a web project or not since other parts of the code will use the cached value
            await IsWebProjectAsync();

            // Don't do the version dropdown for dotnet tooling
            if(IsDnxProject)
            {
                DnxVersions = new ObservableCollection<string>(GetAvailableVersions());
                // If there are no DnxVersions do nothing
                if(DnxVersions.Count != 0)
                {
                    _defaultDNXVersion = await GetDefaultPackageVersionAsync();
                }
            }
            */

            // Create the debug targets dropdown
            InitializeDebugTargets();
        }

        /// <summary>
        /// Called whenever new settings are retrieved. Note that the controls which are affected depend heavily on the
        /// currently selected profile.
        /// </summary>
        private void InitializeIISSettings(IIISSettings iisSettings)
        {
            if(iisSettings == null)
            {
                _currentIISSettings = null;
                return;
            }

            _currentIISSettings = IISSettingsData.FromIIISSettings(iisSettings);

            OnPropertyChanged(nameof(ApplicationUrl));
            OnPropertyChanged(nameof(SSLEnabled));
            OnPropertyChanged(nameof(SSLUrl));
            OnPropertyChanged(nameof(WindowsAuthenticationEnabled));
            OnPropertyChanged(nameof(AnonymousAuthenticationEnabled));
        }

        /// <summary>
        /// Called when then the user saves the form.
        /// </summary>
        public async override Task<int> Save()
        {
            /*
            // For web projects, we need to validate the settings -especially the appUrl from which everything else hangs.
            if(ContainsProfileKind(ProfileKind.IISExpress))
            {
                if(_currentIISSettings == null || _currentIISSettings.IISExpressBindingData == null || string.IsNullOrEmpty(_currentIISSettings.IISExpressBindingData.ApplicationUrl))
                {
                    throw new Exception(Resources.IISExpressMissingAppUrl);
                }
                try
                {
                     Uri appUri = new Uri(_currentIISSettings.IISExpressBindingData.ApplicationUrl, UriKind.Absolute);
                     if(appUri.Port < 1024 && !UnconfiguredDotNetProject.ServiceProvider.VSIsRunningElevated())
                     {
                        throw new Exception(Resources.AdminRequiredForPort);
                     }
                }
                catch (UriFormatException ex)
                {
                    throw new Exception(string.Format(Resources.InvalidIISExpressAppUrl, ex.Message));
                }
            }
            if(ContainsProfileKind(ProfileKind.IIS))
            {
                if(_currentIISSettings == null || _currentIISSettings.IISBindingData == null || string.IsNullOrEmpty(_currentIISSettings.IISBindingData.ApplicationUrl))
                {
                    throw new Exception(Resources.IISMissingAppUrl);
                }
                try
                {
                     Uri appUri = new Uri(_currentIISSettings.IISBindingData.ApplicationUrl, UriKind.Absolute);
                }
                catch (UriFormatException ex)
                {
                    throw new Exception(string.Format(Resources.InvalidIISAppUrl, ex.Message));
                }
            }

            // Persist the settings. The change in IIS Express settings will be tracked by the WebStateManager which will 
            // configure the IIS Express server as needed.
            await SaveLaunchSettings();

            if (!UseSpecificRuntime && IsProfileSelected)
            {
                SelectedDnxVersion = _defaultDNXVersion;
            }
            */ //TODO: DebugProp 
            return VSConstants.S_OK;
        }
        
        /// <summary>
        ///  Helper to determine if an IIS Express profile is defined
        /// </summary>
        private void ValidateApplicationUrl()
        {
            if(string.IsNullOrEmpty(ApplicationUrl))
            {
                throw new Exception(Resources.IISExpressMissingAppUrl);
            }
            try
            {
                    Uri appUri = new Uri(ApplicationUrl, UriKind.Absolute);
                /* //TODO: DebugProp 
                 * if(appUri.Port < 1024 && !UnconfiguredDotNetProject.ServiceProvider.VSIsRunningElevated())
                {
                throw new Exception(Resources.AdminRequiredForPort);
                }*/
            }
            catch (UriFormatException ex)
            {
                throw new Exception(string.Format(Resources.InvalidAppUrl, ex.Message));
            }
        }

        /// <summary>
        ///  Helper to determine if a particular profile type is defined
        /// </summary>
        private bool ContainsProfileKind(ProfileKind kind)
        {
            return DebugProfiles != null && DebugProfiles.FirstOrDefault(p => p.Kind == kind) != null;
        }
       
         /// <summary>
        /// Helper to get the IIS Settings
        /// </summary>
        private IISSettings GetIISSettings()
        {
            if(_currentIISSettings != null)
            {
                return new IISSettings(_currentIISSettings);
            }

            return null;
        }

        private ICommand _addEnironmentVariableRowCommand;
        public ICommand AddEnvironmentVariableRowCommand
        {
            get
            {
                return LazyInitializer.EnsureInitialized(ref _addEnironmentVariableRowCommand, () =>
                    new DelegateCommand((state) =>
                    {
                        NameValuePair newRow = new NameValuePair(Resources.EnvVariableNameWatermark, Resources.EnvVariableValueWatermark, EnvironmentVariables);
                        EnvironmentVariables.Add(newRow);
                        EnvironmentVariablesRowSelectedIndex = EnvironmentVariables.Count - 1;
                        //Raise event to focus on 
                        if (FocusEnvironmentVariablesGridRow != null)
                        {
                            FocusEnvironmentVariablesGridRow.Invoke(this, EventArgs.Empty);
                        }
                    }));
            }
        }

        private ICommand _removeEnvironmentVariableRowCommand;
        public ICommand RemoveEnvironmentVariableRowCommand
        {
            get
            {
                return LazyInitializer.EnsureInitialized(ref _removeEnvironmentVariableRowCommand, () =>
                    new DelegateCommand(state =>
                    {
                        int oldIndex = EnvironmentVariablesRowSelectedIndex;
                        EnvironmentVariables.RemoveAt(EnvironmentVariablesRowSelectedIndex);
                        EnvironmentVariablesValid = true;
                        EnvironmentVariablesRowSelectedIndex = (oldIndex == EnvironmentVariables.Count) ? oldIndex - 1 : oldIndex;
                    }));
            }
        }

        private ICommand _browseDirectoryCommand;
        public ICommand BrowseDirectoryCommand
        {
            get
            {
                return LazyInitializer.EnsureInitialized(ref _browseDirectoryCommand, () =>
                    new DelegateCommand(state =>
                    {
                        using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                        {
                            var folder = WorkingDirectory;
                            if (!String.IsNullOrEmpty(folder) && Directory.Exists(folder))
                            {
                                dialog.SelectedPath = folder;
                            }
                            var result = dialog.ShowDialog();
                            if (result == System.Windows.Forms.DialogResult.OK)
                            {
                                WorkingDirectory = dialog.SelectedPath.ToString();
                            }
                        }
                    }));
            }
        }

        private ICommand _browseExecutableCommand;
        public ICommand BrowseExecutableCommand
        {
            get
            {
                return LazyInitializer.EnsureInitialized(ref _browseExecutableCommand, () =>
                    new DelegateCommand(state =>
                    {
                        using (var dialog = new System.Windows.Forms.OpenFileDialog())
                        {
                            var file = ExecutablePath;
                            if (Path.IsPathRooted(file))
                            {
                                dialog.InitialDirectory = Path.GetDirectoryName(file);
                                dialog.FileName = file;
                            }
                            dialog.Multiselect = false;
                            dialog.Filter = ExecutableFilter;
                            var result = dialog.ShowDialog();
                            if (result == System.Windows.Forms.DialogResult.OK)
                            {
                                ExecutablePath = dialog.FileName.ToString();
                            }
                        }
                    }));
            }
        }
        private ICommand _copySSLUrlCommand;
        public ICommand CopySSLUrlCommand
        {
            get
            {
                return LazyInitializer.EnsureInitialized(ref _copySSLUrlCommand, () =>
                    new DelegateCommand((state) =>
                    {
                        Clipboard.SetText(SSLUrl);
                    }));
            }
        }

        public string CopyHyperlinkText
        {
            get
            {
                // Can't use x:Static in xaml since our resources are internal.
                return Resources.CopyHyperlinkText;
            }
        }

        public bool NewProfileEnabled
        {
            get
            {
                return debugProfiles != null;
            }
        }

        private ICommand _newProfileCommand;
        public ICommand NewProfileCommand
        {
            get
            {
                /*return LazyInitializer.EnsureInitialized(ref _newProfileCommand, () =>
                    new DelegateCommand(state =>
                    {
                        var dialog = new GetProfileNameDialog(UnconfiguredDotNetProject.ServiceProvider, GetNewProfileName(), IsNewProfileNameValid);
                        if (dialog.ShowModal() == true)
                        {
                            CreateProfile(dialog.ProfileName, ProfileKind.Executable);
                        }
                    }));
                */
                return null; //TODO: DebugProp 
            }
        }

        public bool DeleteProfileEnabled
        {
            get
            {
                if (!IsProfileSelected)
                {
                    return false;
                }

                return !IsBuiltInProfile && !SelectedDebugProfile.IsDefaultIISExpressProfile;
            }
        }

        private ICommand _deleteProfileCommand;
        public ICommand DeleteProfileCommand
        {
            get
            {
                return LazyInitializer.EnsureInitialized(ref _deleteProfileCommand, () =>
                    new DelegateCommand(state =>
                    {
                        var profileToRemove = SelectedDebugProfile;
                        SelectedDebugProfile = null;
                        DebugProfiles.Remove(profileToRemove);
                        SelectedDebugProfile = DebugProfiles.Count > 0 ? DebugProfiles[0] : null;
                    }));
            }
        }

        internal DebugProfile CreateProfile(string name, ProfileKind kind)
        {
            var profile = new DebugProfile() { Name = name, Kind = kind };
            DebugProfiles.Add(profile);

            // Fire a property changed so we can get the page to be dirty when we add a new profile
            OnPropertyChanged("_NewProfile");
            SelectedDebugProfile = profile;
            return profile;
        }

        internal bool IsNewProfileNameValid(string name)
        {
            return DebugProfiles.Where(
                profile => DebugProfile.IsSameProfileName(profile.Name, name)).Count() == 0;
        }

        internal string GetNewProfileName()
        {
            for(int i=1; i < int.MaxValue; i++)
            {
                string profileName = String.Format("{0}{1}", Resources.NewProfileSeedName, i.ToString());
                if (IsNewProfileNameValid(profileName))
                {
                    return profileName;
                }
            }

            return String.Empty;
        }

        private void SetLaunchType()
        {
            if (!IsProfileSelected)
            {
                launchTypes = new List<LaunchType>();
            }
            else if (SelectedDebugProfile.Kind == ProfileKind.CustomizedCommand || SelectedDebugProfile.Kind == ProfileKind.IIS || 
                     SelectedDebugProfile.Kind == ProfileKind.Executable || 
                     (SelectedDebugProfile.Kind == ProfileKind.IISExpress && !SelectedDebugProfile.IsDefaultIISExpressProfile))
            {
                // For customized commands, exe, IIS and non-built in IIS Express we allow the user to switch between them. Two cases, one where we have commands
                // and one where there are no commands defined in project.json
                /*if (CommandNames.Count > 0)
                {
                    launchTypes = IsWebProject ? LaunchType.GetWebCustomizedLaunchTypes(IsDnxProject).ToList<LaunchType>() : LaunchType.GetCustomizedLaunchTypes(IsDnxProject).ToList<LaunchType>();
                }
                else
                {
                    launchTypes = IsWebProject ? LaunchType.GetWebExecutableOnlyLaunchTypes(IsDnxProject).ToList<LaunchType>() : LaunchType.GetExecutableOnlyLaunchTypes(IsDnxProject).ToList<LaunchType>();
                }
                */
            }
            /*else
            {
                launchTypes = LaunchType.GetBuiltInLaunchTypes(IsDnxProject).ToList<LaunchType>();
            }*/
            
            OnPropertyChanged(nameof(LaunchTypes));

            // The selected launch type has to be tweaked for DotNet since in dotnet we don't want to support commands and yet user might have some commands 
            // defined from it being a DNX project. For that, we map the command launch types to the "Project" kind
            if(!IsProfileSelected)
            {
                 SelectedLaunchType = null;
            }
            else
            {
                var selKind = SelectedDebugProfile.Kind;
                /*if(!IsDnxProject && (selKind == ProfileKind.BuiltInCommand || selKind == ProfileKind.CustomizedCommand))
                {
                    selKind = ProfileKind.Project;
                }
                SelectedLaunchType = LaunchType.GetAllLaunchTypes(IsDnxProject).Where(lt => lt.Kind == selKind).SingleOrDefault(); 
                */
            }
        }

        private void EnvironmentVariables_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // cause the property page to be dirtied when a row is added or removed
            OnPropertyChanged("EnvironmentVariables_Contents");
        }

        private void DebugPageViewModel_EnvironmentVariables_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // cause the property page to be dirtied when a cell is updated
            OnPropertyChanged("EnvironmentVariables_Contents");
        }

        /// <summary>
        /// Helper to wait on async tasks
        /// </summary>
        protected T WaitForAsync<T>(Func<Task<T>> asyncFunc)
        {
            if (!_useTaskFactory)
            {
                // internal test usage
                Task<T> t = asyncFunc();
                return t.Result;
            }

            return _threadingService.ExecuteSynchronously<T>(asyncFunc);
        }

        /// <summary>
        /// Overridden to do cleanup
        /// </summary>
        public override void ViewModelDetached()
        {
            if(_debugProfileProviderLink != null)
            {
                _debugProfileProviderLink.Dispose();
                _debugProfileProviderLink = null;
            }
        }

        [ExcludeFromCodeCoverage]
        protected virtual async Task<string> GetDefaultPackageVersionAsync()
        {
            /* //TODO: DebugProp 
             * var sdkTooling = await UnconfiguredDotNetProject.GetDefaultTargetSdkToolingData(matchForProjectFrameworks: false, downloadIfNotExist: false);
             if(sdkTooling != null)
             {
                 // For dotnet we need to return the complete string which includes the type information
                 if(!sdkTooling.IsDotNetCli)
                 {
                     return DnxRuntimeName.GetPackageFromRuntimeFolder(sdkTooling.SdkPath).FullNameOrPrefix;
                 }
                 return sdkTooling.SdkVersion;
             }*/
            return string.Empty;
        }

        [ExcludeFromCodeCoverage]
        protected virtual IEnumerable<string> GetAvailableVersions()
        {
            //return TargetDnxVersionsFinder.GetAvailableDnxVersions(GetSubscriptions()); 
            return null; //TODO: DebugProp 
        }

        [ExcludeFromCodeCoverage]
        protected virtual ILaunchSettingsProvider GetDebugProfileProvider()
        {
            //return UnconfiguredDotNetProject.UnconfiguredProject.Services.ExportProvider.GetExportedValue<ILaunchSettingsProvider>();
            return null; //TODO: DebugProp 
        }

        [ExcludeFromCodeCoverage]
        protected virtual ISSLPortProvider GetSSLPortProvider()
        {
            /*try 
            {
                return UnconfiguredDotNetProject.UnconfiguredProject.Services.ExportProvider.GetExportedValue<ISSLPortProvider>();
            }
            catch
            {
            }*/
            return null;
        }

        public class LaunchType
        {
            public ProfileKind Kind { get; set; }
            public string Name { get; set; }

            public override bool Equals(object obj)
            {
                LaunchType oth = obj as LaunchType;
                if (oth != null)
                {
                    return Kind.Equals(oth.Kind);
                }

                return false;
            }

            public override int GetHashCode()
            {
                return Kind.GetHashCode();
            }

            public static readonly LaunchType CustomizedCommand = new LaunchType() { Kind = ProfileKind.CustomizedCommand, Name = Resources.ProfileKindCommandName };
            public static readonly LaunchType BuiltInCommand = new LaunchType() { Kind = ProfileKind.BuiltInCommand, Name = Resources.ProfileKindCommandName };
            public static readonly LaunchType Executable = new LaunchType() { Kind = ProfileKind.Executable, Name = Resources.ProfileKindExecutableName };
            public static readonly LaunchType Project = new LaunchType() { Kind = ProfileKind.Project, Name = Resources.ProfileKindProjectName };
            public static readonly LaunchType IISExpress = new LaunchType() { Kind = ProfileKind.IISExpress, Name = Resources.ProfileKindIISExpressName };

            // Helper accessors to deal with dnx/dotnet differences
            public static LaunchType[] GetAllLaunchTypes(bool isDNX)
            {
                return isDNX? DNXAllLaunchTypes : AllLaunchTypes;
            }

            public static LaunchType[] GetWebCustomizedLaunchTypes(bool isDNX)
            {
                return isDNX? DNXWebCustomizedLaunchTypes : WebCustomizedLaunchTypes;
            }
            public static LaunchType[] GetCustomizedLaunchTypes(bool isDNX)
            {
                return isDNX? DNXCustomizedLaunchTypes : CustomizedLaunchTypes;
            }

            public static LaunchType[] GetBuiltInLaunchTypes(bool isDNX)
            {
                return isDNX? DNXBuiltInLaunchTypes : BuiltInLaunchTypes;
            }

            public static LaunchType[] GetWebExecutableOnlyLaunchTypes(bool isDNX)
            {
                return isDNX? DNXWebExecutableOnlyLaunchType: WebExecutableOnlyLaunchType;
            }

            public static LaunchType[] GetExecutableOnlyLaunchTypes(bool isDNX)
            {
                return isDNX? DNXExecutableOnlyLaunchType: ExecutableOnlyLaunchType;
            }

// billhie: IIS is disabled until RC2 once we have sorted out the hosting story so we don't define an IIS launch type. 
#if IISSUPPORT
            public static readonly LaunchType IIS = new LaunchType() { Kind = ProfileKind.IIS, Name = Resources.ProfileKindIISName };
            public static readonly LaunchType[] AllLaunchTypes = new LaunchType[] { Executable, IISExpress, IIS, Project };
            public static readonly LaunchType[] WebCustomizedLaunchTypes = new LaunchType[] { Executable, IISExpress, IIS, Project };
            public static readonly LaunchType[] WebExecutableOnlyLaunchType = new LaunchType[] { Executable, IISExpress, IIS, Project };
#else
            private static readonly LaunchType[] AllLaunchTypes = new LaunchType[] { Executable, IISExpress, Project };
            private static readonly LaunchType[] WebCustomizedLaunchTypes = new LaunchType[] { Executable, IISExpress, Project };
            private static readonly LaunchType[] WebExecutableOnlyLaunchType = new LaunchType[] { Executable, IISExpress, Project };
#endif
            private static readonly LaunchType[] BuiltInLaunchTypes = new LaunchType[] { Executable, IISExpress, Project };
            private static readonly LaunchType[] CustomizedLaunchTypes = new LaunchType[] { Executable, Project };
            private static readonly LaunchType[] ExecutableOnlyLaunchType = new LaunchType[] { Executable, Project };

            // DNX still exposes commands, so we have a different set for those
            private static readonly LaunchType[] DNXAllLaunchTypes = new LaunchType[] { BuiltInCommand, CustomizedCommand, Executable, IISExpress};
            private static readonly LaunchType[] DNXWebCustomizedLaunchTypes = new LaunchType[] { CustomizedCommand, Executable, IISExpress};
            private static readonly LaunchType[] DNXWebExecutableOnlyLaunchType = new LaunchType[] { Executable, IISExpress, Project };
            private static readonly LaunchType[] DNXBuiltInLaunchTypes = new LaunchType[] { BuiltInCommand, Executable, IISExpress};
            private static readonly LaunchType[] DNXCustomizedLaunchTypes = new LaunchType[] { CustomizedCommand, Executable};
            private static readonly LaunchType[] DNXExecutableOnlyLaunchType = new LaunchType[] { Executable };
        }
    }
}
