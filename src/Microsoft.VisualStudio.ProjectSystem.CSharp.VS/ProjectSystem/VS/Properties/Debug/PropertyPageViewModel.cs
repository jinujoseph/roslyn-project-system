//--------------------------------------------------------------------------------------------
// PropertyPageViewModel
//
// Base ViewModel for all our property pages
//
// Copyright(c) 2014 Microsoft Corporation
//--------------------------------------------------------------------------------------------

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    using Microsoft.VisualStudio.ProjectSystem.CSharp.VS;
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    //using Microsoft.VisualStudio.ProjectSystem.DotNet.PropertyProviders;

    internal abstract class PropertyPageViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public IPropertyProvider[] ConfiguredProperties { get; set; }
        public IPropertyProvider UnconfiguredProperties { get;  set; }
        public UnconfiguredProject UnconfiguredDotNetProject { get; set; }
        public PropertyPageControl ParentControl { get; set; }

        /// <summary>
        /// Since calls to ignore events can be nested, a downstream call could change the outer 
        /// value.  To guard against this, IgnoreEvents returns true if the count is > 0 and there is no setter. 
        /// PushIgnoreEvents\PopIgnoreEvents  are used instead to control the count.
        /// </summary>
        private int _ignoreEventsNestingCount = 0;
        public bool IgnoreEvents { get { return _ignoreEventsNestingCount > 0;}}
        public void PushIgnoreEvents()
        {
            _ignoreEventsNestingCount++;
        }

        public void PopIgnoreEvents()
        {
            Debug.Assert(_ignoreEventsNestingCount > 0);
            if (_ignoreEventsNestingCount > 0)
            {
                _ignoreEventsNestingCount--;
            }
        }

        public abstract Task Initialize();
        public abstract Task<int> Save();

        /* //TODO: DebugProp 
        [ExcludeFromCodeCoverage]
        public virtual IProjectSubscriptions GetSubscriptions()
        {
            return UnconfiguredDotNetProject.UnconfiguredProject.Services.ExportProvider.GetExportedValue<IProjectSubscriptions>();
        }*/

        protected virtual void OnPropertyChanged(string propertyName, bool suppressInvalidation = false)
        {
            // For some properties we don't want to invalidate the property page
            if (suppressInvalidation)
            {
                PushIgnoreEvents();
            }
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
            if(suppressInvalidation)
            {
                PopIgnoreEvents();
            }
        }

        protected virtual bool OnPropertyChanged<T>(ref T propertyRef, T value, bool suppressInvalidation, [CallerMemberName] string propertyName = null)
        {
            if (!Object.Equals(propertyRef, value))
            {
                propertyRef = value;
                OnPropertyChanged(propertyName, suppressInvalidation);
                return true;
            }
            return false;
        }

        protected virtual bool OnPropertyChanged<T>(ref T propertyRef, T value, [CallerMemberName] string propertyName=null)
        {
            return OnPropertyChanged(ref propertyRef, value, suppressInvalidation: false, propertyName: propertyName);
        }

        /* //TODO: DebugProp
        /// <summary>
        /// Helper to determine if this is a web project or not. We cache it so we can get it in a non
        /// async way from the UI.
        /// </summary>
        public bool IsWebProject { get; private set; }
        public virtual async Task<bool> IsWebProjectAsync()
        {
            bool isWebProject;
            bool.TryParse(await UnconfiguredProperties.GetEvaluatedPropertyValueAsync(ConfigurationGeneral.IsWebProjectProperty), out isWebProject);
            IsWebProject = isWebProject;
            return isWebProject;
        }

        public virtual bool IsDnxProject 
        {
            get
            {
                var sdkToolingData = UnconfiguredDotNetProject.CurrentSdkToolingData;
                return sdkToolingData == null ? false : !sdkToolingData.IsDotNetCli;
            }
        }*/

        protected void SetBooleanProperty(ref bool property, string value, bool defaultValue, bool invert = false)
        {
            if (!String.IsNullOrEmpty(value))
            {
                property = bool.Parse(value);
                if (invert)
                {
                    property = !property;
                }
            }
            else
            {
                property = defaultValue;
            }
        }

        /// <summary>
        /// Override to do cleanup
        /// </summary>
        public virtual void ViewModelDetached()
        {

        }
    }
}

