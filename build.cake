//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#tool "nuget:?package=GitVersion.CommandLine"

//////////////////////////////////////////////////////////////////////
// ADDINS
//////////////////////////////////////////////////////////////////////
#addin "Cake.Docker"

//////////////////////////////////////////////////////////////////////
// NAMESPACES
//////////////////////////////////////////////////////////////////////
using System.Linq;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var isCIBuild = !BuildSystem.IsLocalBuild;
var registry = "my-registry:55000";

GitVersion gitVersionInfo;
string nugetVersion;
(string ProjectName, string ImageName, string ImagePath)[] solution;

Setup(context =>
{
    gitVersionInfo = GitVersion(new GitVersionSettings {
        OutputType = GitVersionOutput.Json
    });

    nugetVersion = gitVersionInfo.NuGetVersion;
    
    solution = new []
    {
        ("Generator API", $"generator-api:{nugetVersion}", "./Dockerfile")        
    };
    
    Information("Building generator Api v{0} with configuration {1}", nugetVersion, configuration);
});


Task("__DockerBuild")
    .Does(async () =>
    {
        var aggregate = solution.Select(project =>
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                var settings = new DockerImageBuildSettings
                { 
                    File = project.ImagePath,
                    BuildArg = new[] {
                        $"BUILDCONFIG={configuration}",
                        $"VERSION={nugetVersion}"
                    },
                    Tag = new[] {$"{registry}/{project.ImageName}"}
                };

                DockerBuild(settings, ".");

                return System.Threading.Tasks.Task.CompletedTask;
            }));

        await System.Threading.Tasks.Task.WhenAll(aggregate);
    });

Task("__DockerPush")
    .Does(async () =>
    {
        var aggregate = solution.Select(project =>
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                var settings = new ProcessSettings
                { 
                    Arguments = $"push {registry}/{project.ImageName}"
                };

                StartProcess("docker", settings);

                return System.Threading.Tasks.Task.CompletedTask;
            }));

        await System.Threading.Tasks.Task.WhenAll(aggregate);
    });

Task("Build")
    .IsDependentOn("__DockerBuild")
    .IsDependentOn("__DockerPush");

Task("Default")
    .IsDependentOn("Build");

RunTarget(target);