//--------------------------------------------------------------------------------------------
// DebugPropertyPage
//
// PropertyPage implementation for debug designer tab. It is split into two classes one which 
// describes the property and is exported so the PropertyPageProvider  can return the information
// to CPS, and the actual property page class. 
//
// Copyright(c) 2014 Microsoft Corporation
//--------------------------------------------------------------------------------------------

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    using Microsoft.VisualStudio.ProjectSystem.VS.Extensibility;
    using Microsoft.VisualStudio.Shell;
    using System;
    using System.ComponentModel.Composition;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.InteropServices;

#if VS15
    using Microsoft.VisualStudio.ProjectSystem.VS.Properties;
#elif VS14
    using Microsoft.VisualStudio.ProjectSystem.Utilities;
    using Microsoft.VisualStudio.ProjectSystem.VS;
#endif

    [Export(typeof(IPageMetadata))]
    [AppliesTo(ProjectCapability.CSharp)]
    internal partial class DebugPropertyPageMetaData : IPageMetadata
    {
        bool IPageMetadata.HasConfigurationCondition { get {return false;} }
        string IPageMetadata.Name { get {return DebugPropertyPage.PageName;} }
        Guid IPageMetadata.PageGuid { get {return typeof(DebugPropertyPage).GUID;}}
        int IPageMetadata.PageOrder { get {return 30;} }
    }

    [Guid("0273C280-1882-4ED0-9308-52914672E3AA")]
    [ExcludeFromCodeCoverage]
    [AppliesTo(ProjectCapability.CSharp)]
    internal partial class DebugPropertyPage : WpfBasedPropertyPage
    {

        internal static readonly string PageName = "Debug Page"; // Resources.DebugPropertyPageTitle;//TODO: DebugProp 
        
        public DebugPropertyPage()
        { 
        }

        protected override PropertyPageViewModel CreatePropertyPageViewModel()
        {
            return new DebugPageViewModel();
        }

        protected override PropertyPageControl CreatePropertyPageControl()
        {
            return new DebugPageControl();
        }

        protected override string PropertyPageName
        {
            get
            {
                return PageName;
            }
        }

        /// <summary>
        ///   Looks up a localized string similar to Arguments to be passed to the application.
        /// </summary>
        public static string ApplicationArgumentsWatermark
        {
            get
            {
                return  Resources.ApplicationArgumentsWatermark;
            }
        }
        


        public static string ApplicationUrlWatermark
        {
            get
            {
                return Resources.ApplicationUrlWatermark;
            }
        }

        /// <summary>
        ///   Looks up a localized string similar to Arguments to be passed to the application.
        /// </summary>
        public static string EnvVariableValueWatermark
        {
            get
            {
                return Resources.EnvVariableValueWatermark;
            }
        }

        /// <summary>
        ///   Looks up a localized string similar to Path to the executable to run.
        /// </summary>
        public static string ExecutablePathWatermark
        {
            get
            {
                return Resources.ExecutablePathWatermark;
            }
        }

        /// <summary>
        ///   Looks up a localized string similar to Absolute or relative URL.
        /// </summary>
        public static string LaunchUrlWatermark
        {
            get
            {
                return Resources.LaunchUrlWatermark;
            }
        }

        /// <summary>
        ///   Looks up a localized string similar to Absolute path to working directory.
        /// </summary>
        public static string WorkingDirectoryWatermark
        {
            get
            {
                return Resources.WorkingDirectoryWatermark;
            }
        }
    }
}

