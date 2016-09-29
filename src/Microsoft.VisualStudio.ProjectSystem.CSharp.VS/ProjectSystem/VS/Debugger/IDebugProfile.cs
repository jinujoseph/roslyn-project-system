//--------------------------------------------------------------------------------------------
// IDebugProfile
//
// Interface definition for a profile
//
// Copyright(c) 2014 Microsoft Corporation
//--------------------------------------------------------------------------------------------
namespace Microsoft.VisualStudio.ProjectSystem.DotNet
{
    using System.Collections.Immutable;

    public enum ProfileKind
    {
        IISExpress,
        BuiltInCommand,
        CustomizedCommand,
        Executable,
        IIS,
        Project,            // Run the project executable
        NoAction            // This is the profile used when there is no profiles. It is a dummy placeholder
    }

    public interface IDebugProfile
    {
        string Name { get; }
        ProfileKind Kind{ get; }
        string ExecutablePath { get; }
        string CommandName { get; }
        string CommandLineArgs{ get; }
        string WorkingDirectory{ get; }
        bool LaunchBrowser { get; }
        string LaunchUrl { get; }
        string ApplicationUrl { get; }
        ImmutableDictionary<string, string> EnvironmentVariables { get; }
        string SDKVersion{ get; }
        bool IsDefaultIISExpressProfile { get; }
        bool ProfileShouldBePersisted();
        bool IsWebBasedProfile { get; }
        bool IsWebServerCmdProfile { get; }
    }
}
