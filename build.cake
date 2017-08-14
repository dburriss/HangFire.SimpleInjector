#tool "nuget:?package=xunit.runner.console"

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument<string>("target", "Default");
var configuration = Argument<string>("configuration", "Release");
var verbosity = Argument<string>("verbosity");

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////

var solutions = GetFiles("./**/*.sln");
var proj = Directory("./src/HangFire.SimpleInjector/HangFire.SimpleInjector.csproj");
var solutionPaths = solutions.Select(solution => solution.GetDirectory());

var unitTestsProjGlob = "./**/*.Tests.csproj";

var packageDir = Directory("./artifacts/nuget/" + configuration);
var publishDir = Directory("./artifacts/build/" + configuration);

var dotNetCoreVerbosity = Cake.Common.Tools.DotNetCore.DotNetCoreVerbosity.Detailed;
if (!Enum.TryParse(verbosity, true, out dotNetCoreVerbosity))
{
    Warning("Verbosity could not be parsed into type 'Cake.Common.Tools.DotNetCore.DotNetCoreVerbosity'. Defaulting to {0}", dotNetCoreVerbosity); 
}

///////////////////////////////////////////////////////////////////////////////
// COMMON FUNCTION DEFINITIONS
///////////////////////////////////////////////////////////////////////////////

void Test(string testDllGlob)
{
    var testAssemblies = GetFiles(testDllGlob);

    foreach(var testProject in testAssemblies)
    {
        Verbose("Testing '{0}'...", testProject.FullPath);
        var settings = new DotNetCoreTestSettings
        {
            Configuration = configuration,
            Verbosity = dotNetCoreVerbosity
        };

        DotNetCoreTest(testProject.FullPath, settings);
    }
}

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(ctx =>
{
    // Executed BEFORE the first task.
    EnsureDirectoryExists(packageDir);
    EnsureDirectoryExists(publishDir);
    Verbose("Running tasks...");
});

Teardown(ctx =>
{
    // Executed AFTER the last task.
    Verbose("Finished running tasks.");
});

///////////////////////////////////////////////////////////////////////////////
// TASK DEFINITIONS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Description("Cleans all directories that are used during the build process.")
    .Does(() =>
{
    foreach(var path in solutionPaths)
    {
        Verbose("Cleaning '{0}'...", path);
        CleanDirectories(path + "/**/bin/" + configuration);
        CleanDirectories(path + "/**/obj");
        CleanDirectory(packageDir);
        CleanDirectory(publishDir);
        Verbose("'{0}' cleaned.", path);
    }
});

Task("Restore")
    .Description("Restores all the NuGet packages that are used by the specified solution.")
    .Does(() =>
{
    string rawPackageSources = EnvironmentVariable("PackageSources");
    Verbose("Customising NuGet sources. Going to use [{0}]", rawPackageSources);

    IList<string> packageSources = null;
    if (!string.IsNullOrEmpty(rawPackageSources))
    {
        packageSources = rawPackageSources.Split(';');
    }
 
    var restoreSettings = new DotNetCoreRestoreSettings { 
        Sources = packageSources,
        Verbosity = dotNetCoreVerbosity
    };

    foreach(var solution in solutions)
    {
        Verbose("Restoring NuGet packages for '{0}'...", solution);
        DotNetCoreRestore(solution.FullPath, restoreSettings);
        Verbose("NuGet packages restored for '{0}'.", solution);
    }
});

Task("Build")
    .Description("Builds all the different parts of the project.")
    .Does(() =>
{
    foreach(var solution in solutions)
    {
        Verbose("Building '{0}'...", solution);
        var msBuildSettings = new DotNetCoreMSBuildSettings {
            TreatAllWarningsAs = MSBuildTreatAllWarningsAs.Error,
            Verbosity = dotNetCoreVerbosity
        };
        var settings = new DotNetCoreBuildSettings {
            Configuration = configuration,
            MSBuildSettings = msBuildSettings
        };

        DotNetCoreBuild(solution.FullPath, settings);
        Verbose("'{0}' has been built.", solution);
    }
});

Task("Test")
    .Description("Runs all your unit tests, using xUnit.")
    .Does(() => { Test(unitTestsProjGlob); });

Task("Package")
    .Description("Publishes the nupkg 'artifacts/nuget/<release>'.")
    .Does(() => 
{
    Verbose("Packaging to '{0}'...", packageDir);
    var settings = new DotNetCorePackSettings
    {
        Configuration = configuration,
        OutputDirectory = packageDir
    };
    DotNetCorePack(proj, settings);
    Verbose("Packaged to '{0}'.", packageDir);
});

Task("Publish")
    .Description("Publishes all the dlls to 'artifacts/build/<release>'.")
    .Does(() => 
{
        Verbose("Publishing everything to '{0}'...", publishDir);
        var settings = new DotNetCorePublishSettings
        {
            Configuration = configuration,
            OutputDirectory = publishDir
        };
        DotNetCorePublish(proj, settings);
        Verbose("Everything has been published to '{0}'.", publishDir);
});



///////////////////////////////////////////////////////////////////////////////
// COMBINATIONS - let's make life easier...
///////////////////////////////////////////////////////////////////////////////

Task("Rebuild+Test")
    .Description("First runs Build, then Test targets.")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .Does(() => { Information("Ran Build+Test target"); });

Task("Rebuild")
    .Description("Runs a full Clean+Restore+Build build.")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .IsDependentOn("Build")
    .Does(() => { Information("Rebuilt everything"); });

///////////////////////////////////////////////////////////////////////////////
// DEFAULT TARGET
///////////////////////////////////////////////////////////////////////////////

Task("Default")
    .Description("This is the default task which will run if no specific target is passed in.")
    .IsDependentOn("Rebuild+Test")
    .IsDependentOn("Package")
    .Does(() => { Warning("No 'Target' was passed in, so we ran the 'Rebuild+Test' and 'Package' operation."); });

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);