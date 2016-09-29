//--------------------------------------------------------------------------------------------
// ILaunchSettingsProvider
//
// Interface definition used by LaunchSettingsProvider.
//
// Copyright(c) 2014 Microsoft Corporation
//--------------------------------------------------------------------------------------------
namespace Microsoft.VisualStudio.ProjectSystem.DotNet
{
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;

    internal interface ILaunchSettingsProvider
    {
        IReceivableSourceBlock<ILaunchSettings> SourceBlock { get; }
        ILaunchSettings CurrentSnapshot { get; }
        string DebugSettingsFile { get; }
        IDebugProfile ActiveProfile { get; }
            
        // Replaces the current set of profiles with the contents of profiles. If changes were
        // made, the file will be checked out and updated. If the active profile is different, the
        // active profile property in the krpoj.user file is updated.
        Task UpdateAndSaveSettingsAsync(ILaunchSettings profiles);

        // Blocks until at least one snapshot has been generated.
        Task<ILaunchSettings> WaitForFirstSnapshot(CancellationToken token, int timeout);
    }
}

