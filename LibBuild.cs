using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
class LibBuild : NukeBuild
{
    [GitRepository]
    readonly GitRepository GitRepository;

    [GitVersion]
    readonly Nuke.Common.Tools.GitVersion.GitVersion GitVersion;

    [Solution]
    readonly Solution Solution;

    [Parameter]
    string ApiKey;

    [Parameter]
    string Path;

    [Parameter]
    public bool ForProd { get; set; }

    public string Configuration => ForProd ? "Release" : "Debug";

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    Target Clean => _ => _.Before(Restore).Executes(() => EnsureCleanDirectory(ArtifactsDirectory));

    Target Restore => _ => _.Executes(() => DotNetRestore(s => s.SetProjectFile(Solution)));

    Target Build => _ => _.DependsOn(Restore)
                          .Before(Test)
                          .Executes(() =>
                          {
                              Logger.Normal("Version: " + GitVersion.FullSemVer);
                              DotNetBuild(s => s.SetProjectFile(Solution)
                                                .SetConfiguration(Configuration)
                                                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                                                .SetFileVersion(GitVersion.AssemblySemVer)
                                                .SetInformationalVersion(GitVersion.InformationalVersion)
                                                .SetVersion(GitVersion.FullSemVer)
                                                .EnableNoRestore());
                          });

    Target Test => _ => _.Before(Pack)
                         .Executes(() =>
                         {
                             DotNetTest(s => s.SetProjectFile(Solution)
                                              .SetConfiguration(Configuration)
                                              .EnableNoBuild()
                                              .EnableNoRestore());
                         });

    Target Pack => _ => _.Executes(() =>
    {
        DotNetPack(s => s.SetConfiguration(Configuration)
                         .SetOutputDirectory(ArtifactsDirectory)
                         .EnableIncludeSymbols()
                         .EnableIncludeSource()
                         .SetAssemblyVersion(GitVersion.AssemblySemVer)
                         .SetFileVersion(GitVersion.AssemblySemVer)
                         .SetInformationalVersion(GitVersion.InformationalVersion)
                         .SetVersion(GitVersion.FullSemVer)
                         .SetSymbolPackageFormat(DotNetSymbolPackageFormat.snupkg)
                         .EnableNoBuild()
                         .EnableNoRestore());
    });

    Target Push => _ => _.DependsOn(Pack)
                         .Requires(() => Path)
                         .Requires(() => ApiKey)
                         .Executes(() =>
                         {
                             void Push(string x)
                             {
                                 DotNetNuGetPush(s => s.SetTargetPath(x).SetSource(Path).SetApiKey(ApiKey));
                             }

                             GlobFiles(ArtifactsDirectory, "*.nupkg").NotEmpty().ForEach(Push);
                             GlobFiles(ArtifactsDirectory, "*.snupkg").NotEmpty().ForEach(Push);
                         });

    public static int Main()
    {
        return Execute<LibBuild>(x => x.Build);
    }
}