using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
class LibBuild : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<LibBuild>(x => x.Build);

    [Parameter] string MyGetSource;
    [Parameter] string MyGetApiKey;


    [Parameter]
    public bool ForProd { get; set; }

    public string Configuration => ForProd ? "Release" : "Debug";

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion] readonly GitVersion GitVersion;

    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            EnsureCleanDirectory(ArtifactsDirectory);
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Build => _ => _
        .DependsOn(Restore)
        .Before(Test)
        .Executes(() =>
        {

            Logger.Normal("Version: " + GitVersion.FullSemVer);
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .SetVersion(GitVersion.FullSemVer)
                .EnableNoRestore());
        });


    Target Test => _ => _
        .Before(Pack)
        .Executes(() =>
        {
            DotNetTest(s => s
               .SetProjectFile(Solution)
               .SetConfiguration(Configuration)
               .EnableNoBuild()
               .EnableNoRestore());
        });

    Target Pack => _ => _
        .Executes(() =>
        {
            DotNetPack(s => s
               .SetConfiguration(Configuration)
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

    Target Push => _ => _
         .DependsOn(Pack)
         .Requires(() => MyGetSource)
         .Requires(() => MyGetApiKey)
         .Executes(() =>
         {
             GlobFiles(ArtifactsDirectory, "*nupkg").NotEmpty()
                .ForEach(x =>
                {
                    DotNetNuGetPush(s => s
                        .SetTargetPath(x)
                        .SetSource(MyGetSource)
                        .SetApiKey(MyGetApiKey));
                });
         });

}
