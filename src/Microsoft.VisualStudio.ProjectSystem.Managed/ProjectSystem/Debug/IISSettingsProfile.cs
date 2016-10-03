//--------------------------------------------------------------------------------------------
// IISSettings
//
// Represents the iis settings section in LaunchSettings.json
//
// Copyright(c) 2015 Microsoft Corporation
//--------------------------------------------------------------------------------------------
namespace Microsoft.VisualStudio.ProjectSystem.Debug
{
    using System;
    using Newtonsoft.Json;
 
    // Each server (iis and iis express) contains a serverbinding data object if present.
    [JsonObject(MemberSerialization.OptIn)]
    internal class ServerBindingData : IServerBinding
    {
        [JsonProperty(PropertyName="applicationUrl")]
        public string ApplicationUrl { get; set; }

        [JsonProperty(PropertyName="sslPort")]
        public int SSLPort { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal class IISSettingsData
    {
        public const bool DefaultAnonymousAuth = true;
        public const bool DefaultWindowsAuth = false;

        [JsonProperty(PropertyName="windowsAuthentication")]
        public bool WindowsAuthentication { get; set; } = DefaultWindowsAuth;

        [JsonProperty(PropertyName="anonymousAuthentication")]
        public bool AnonymousAuthentication { get; set; } = DefaultAnonymousAuth;

        [JsonProperty(PropertyName="iis")]
        public ServerBindingData IISBindingData { get; set; }


        [JsonProperty(PropertyName="iisExpress")]
        public ServerBindingData IISExpressBindingData { get; set; }

        /// <summary>
        /// Helper to convert an IIISSettings back to its serializable form.
        /// </summary>
        public static IISSettingsData FromIIISSettings(IIISSettings settings)
        {
            IISSettingsData data = new IISSettingsData();
            data.WindowsAuthentication = settings.WindowsAuthentication;
            data.AnonymousAuthentication= settings.AnonymousAuthentication;
            if(settings.IISBinding != null)
            {
                data.IISBindingData = new ServerBindingData() {ApplicationUrl = settings.IISBinding.ApplicationUrl, SSLPort = settings.IISBinding.SSLPort};
            }
            if(settings.IISExpressBinding != null)
            {
                data.IISExpressBindingData = new ServerBindingData() {ApplicationUrl = settings.IISExpressBinding.ApplicationUrl, SSLPort = settings.IISExpressBinding.SSLPort};
            }
            return data;
        }
    }


    internal class IISSettingsProfile : IIISSettings
    {
        IISSettingsData SettingsData { get; set; }
        public IISSettingsProfile(IISSettingsData data)
        {
            SettingsData = data;
        }

        public IISSettingsProfile(IIISSettings data)
        {
            SettingsData = IISSettingsData.FromIIISSettings(data);
        }
        
        public bool WindowsAuthentication 
        { 
            get
            {
                return SettingsData.WindowsAuthentication;
            }
        }
        
        public bool AnonymousAuthentication
        { 
            get
            {
                return SettingsData.AnonymousAuthentication;
            }
        }
        
        public IServerBinding IISBinding 
        { 
            get
            {
                return SettingsData.IISBindingData;
            }
        }

        public IServerBinding IISExpressBinding
        { 
            get
            {
                return SettingsData.IISExpressBindingData;
            }
        }

        /// <summary>
        /// Helper to determine whether the settings contain any real values
        /// </summary>
        public static bool IsEmptySettings(IIISSettings settings)
        {
            return settings.AnonymousAuthentication == IISSettingsData.DefaultAnonymousAuth &&
                   settings.WindowsAuthentication == IISSettingsData.DefaultWindowsAuth &&
                   settings.IISBinding == null &&
                   settings.IISExpressBinding == null;
        }

        /// <summary>
        /// Helper to determine if two settings are different
        /// </summary>
        public static bool SettingsDiffer(IIISSettings settings1, IIISSettings settings2)
        {
            if(settings1.AnonymousAuthentication != settings2.AnonymousAuthentication ||
               settings1.WindowsAuthentication != settings2.WindowsAuthentication)
            {
                return true;
            }

            // One null, one not null
            if(settings1.IISBinding == null ^ settings2.IISBinding == null ||
               settings1.IISExpressBinding == null ^ settings2.IISExpressBinding == null)
            {
                return true;
            }

            if(settings1.IISBinding != null && settings2.IISBinding != null)
            {
                if(!string.Equals(settings1.IISBinding.ApplicationUrl, settings2.IISBinding.ApplicationUrl, StringComparison.Ordinal) ||
                    settings1.IISBinding.SSLPort != settings2.IISBinding.SSLPort)
                {
                    return true;
                }
            }

            if(settings1.IISExpressBinding != null && settings2.IISExpressBinding != null)
            {
                if(!string.Equals(settings1.IISExpressBinding.ApplicationUrl, settings2.IISExpressBinding.ApplicationUrl, StringComparison.Ordinal) ||
                    settings1.IISExpressBinding.SSLPort != settings2.IISExpressBinding.SSLPort)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
