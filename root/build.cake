#load "./cake/SharpBuild.cake"

var build = new SharpBuild(Context, "<project-name>");

Task("Clean")
    .Does(() => build.Clean());
   
Task("Restore")
    .Does(() => build.Restore());
 
Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .Does(() => build.Build());
    
Task("Pack")
    .IsDependentOn("Build")
    .Does(() => build.Pack());

Task("Publish")
    .WithCriteria(() => build.IsCiWithTag)
    .IsDependentOn("Pack")
    .Does(() => build.Publish());

Task("Default")
    .IsDependentOn("Build");

Task("CI")
    .IsDependentOn("Publish");

RunTarget(Argument("target", "Default"));
