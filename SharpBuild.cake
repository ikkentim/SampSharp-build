#tool nuget:?package=gitreleasemanager&version=0.11.0

#load "./ProjectFile.cake"

public class SharpBuild
{
    private string _configuration = "Release";
    private string _version;
    private string _versionFull;
    private string _versionSuffix;

    public SharpBuild(ICakeContext context, params string[] projects)
    {
        Context = context;

        BuildPathRelease = "./bin/Release";
        BuildPathDebug = "./bin/Debug";
        NuGetKey = Context.EnvironmentVariable("NUGET_KEY");
        GitHubToken = Context.EnvironmentVariable("GITHUB_TOKEN");
        NuGetSource = "https://api.nuget.org/v3/index.json";
        NuGetSources = new [] {
            "https://api.nuget.org/v3/index.json",
        };
        Configuration = Context.Argument("configuration", "Release");
        Projects = projects;

        var repo = Context.EnvironmentVariable("GITHUB_REPOSITORY");

        if(!string.IsNullOrEmpty(repo))
        {
            var repoSplit = repo.Split("/");
            GitHubOwner = repoSplit[0];
            GitHubRepository = repoSplit[1];
        }
    }

    protected ICakeContext Context { get; private set; }
    public string[] Projects { get; private set; }
    public string BuildPathDebug { get; set; }
    public string BuildPathRelease { get; set; }

    public string NuGetKey { get; set; }
    public string NuGetSource { get; set; }
    public string[] NuGetSources { get; set; }
    public string GitHubToken { get; set; }

    public string GitHubOwner { get; set; }
    public string GitHubRepository { get; set; }

    public IEnumerable<ProjectFile> ProjectFiles =>  Projects.Select(GetProjectFile); 

    public string BuildPath => IsRelease ? BuildPathRelease : BuildPathDebug;

    public string Configuration
    { 
        get { return _configuration; }
        set {  _configuration = value.ToLower() == "release" ? "Release" : "Debug"; } 
    }
    
    public bool IsRelease =>  Configuration == "Release";

    public string TagName => Context.EnvironmentVariable("GITHUB_REF"); // TODO contents?

    public bool IsCi => Context.BuildSystem().GitHubActions.IsRunningOnGitHubActions;

    public bool IsCiWithTag => IsCi && false; // TODO: WIP

    public string Version
    {
        get
        {
            BuildVersion();
            return _version;
        }
    }
    
    public string VersionFull
    {
        get
        {
            BuildVersion();
            return _versionFull;
        }
    }
    public string VersionSuffix
    {
        get
        {
            BuildVersion();
            return _versionSuffix;
        }
    }
    
    /// Gets the project file for the specified project.
    private ProjectFile GetProjectFile(string project)
    {
        return new ProjectFile(Context, project);
    }

    /// Builds the version strings, Version, VersionFull and VersionSuffix
    private void BuildVersion()
    {
        if (_version != null) return;
        
        if (IsCiWithTag)
        {
            Context.Information($"TagName: {TagName}");
            _versionFull = TagName;
            var parts = _versionFull.Split(new []{'-'}, 2);
            _version = parts[0];
            _versionSuffix = parts.Length > 1 ? parts[1] : null;
        }
        else if (IsCi)
        {
            _version = "0.0.0";
            _versionSuffix = "ci";
            _versionFull = "0.0.0-ci";
        }
        else
        {
            _version = "0.0.0";
            _versionSuffix = "localbuild";
            _versionFull = "0.0.0-localbuild";
        }
    }

    /// Gets the most recent release notes for the specified project.
    private ReleaseNotes GetReleaseNotes(string project)
    {
        string path;
        if (Projects.Length > 1) {
            path = string.Format("./CHANGES-{0}.md", project);
        }
        else {
            path = "./CHANGES.md";
        }

        var notes = Context.FileExists(path)
            ? Context.ParseReleaseNotes(path)
            : null;

        return notes;
    }

    /// Gets a value indicating whether the release notes for the specified
    /// project has notes for the version specified in "VersionFull".
    private bool HasMatchingReleaseNotesVersion(string project) {
        var notes = GetReleaseNotes(project);

        return VersionFull == GetVersionString(notes);
    }

    /// Returns a string format of the last version in the specified release notes
    private string GetVersionString(ReleaseNotes notes){
        return notes.RawVersionLine.Trim().TrimStart('#').Trim().ToLower();
    }

    /// Backs up and replaces the project (.csproj) file with a file containing the version and release notes of the project.
    private void PrepareProjectFile(string project)
    {
        var file = GetProjectFile(project);
        file.Backup();
        
        var notes = GetReleaseNotes(project);
        var notesText = string.Join("\n", notes.Notes);
        var projectVersion = GetVersionString(notes);
        notesText = notesText.Replace("<", "&lt;");

        file.Replace("<AssemblyVersion>(.*)</AssemblyVersion>", "<AssemblyVersion>" + projectVersion + "</AssemblyVersion>");
        file.Replace("<FileVersion>(.*)</FileVersion>", "<FileVersion>" + projectVersion + "</FileVersion>");
        file.Replace("<Version>(.*)</Version>", "<Version>" + projectVersion + "</Version>");
        file.Replace("<PackageReleaseNotes>(.*)</PackageReleaseNotes>", "<PackageReleaseNotes>" + notesText + "</PackageReleaseNotes>");
    }

    /// Restores a backed up project (.csproj) file.
    private void RestoreProjectFile(string project)
    {
        GetProjectFile(project).Restore();
    }

    /// (TARGET) Cleans the build directory.
    public void Clean()
    {
        Context.CleanDirectory(BuildPath);
    }

    /// (TARGET) Builds the projects.
    public void Build()
    {
        var settings = new DotNetCoreBuildSettings {
            OutputDirectory = BuildPath,
            Configuration = Configuration,
        };

        foreach (var projectFile in ProjectFiles) {
            Context.DotNetCoreBuild(projectFile.Path.FullPath, settings);
        }
    }

    /// (TARGET) Restores dependencies of the DotNet Core projects
    public void Restore()
    {
        var settings = new DotNetCoreRestoreSettings {
            Sources = NuGetSources
        };

        foreach (var projectFile in ProjectFiles) {
            Context.DotNetCoreRestore(projectFile.Path.FullPath, settings);
        }
    }

    /// (TARGET) Packs the NuGet packages.
    public void Pack()
    {
        Context.Information("Packing with version " + VersionFull);

        var settings = new DotNetCorePackSettings {
            VersionSuffix = VersionSuffix,
            Configuration = Configuration,
            OutputDirectory = BuildPath,
        };

        // Prepare all projects before packing so project references have correct version information.
        foreach (var project in Projects) {
            PrepareProjectFile(project);
        }
        foreach (var project in Projects) {
            Context.DotNetCorePack(GetProjectFile(project).Path.FullPath, settings);
        }
        foreach (var project in Projects) {
            RestoreProjectFile(project);
        }
    }

    /// Publishes the packages on GitHub if release notes have been written for the tag version.
    public void PublishNuGet()
    {
        var settings = new NuGetPushSettings {
            Source = NuGetSource,
            ApiKey = NuGetKey
        };

        foreach (var project in Projects)
        {
            // Only push the package if release notes have been written.
            if(HasMatchingReleaseNotesVersion(project))
            {
                foreach (var package in Context.GetFiles(BuildPath + "/" + project + ".*.nupkg"))
                {
                    Context.NuGetPush(package, settings);
                }
            }
        }
    }

    /// Creates a release on GitHub on the master branch the package along with
    /// the release notes. Nothing will be done if no GitHub user or pass is set.
    public void PublishGitHub()
    {
        if (string.IsNullOrEmpty(GitHubToken) ||
            string.IsNullOrEmpty(GitHubOwner) ||
            string.IsNullOrEmpty(GitHubRepository))
        {
            Context.Warning("Cannot publish to GitHub, configuration options are missing.");
            return;
        }

        // Construct release notes for all project
        var notesPath = BuildPath + "/releasenotes-" + VersionFull + ".txt";
        string notes = string.Empty;
        
        foreach (var project in Projects)
        {
            if(HasMatchingReleaseNotesVersion(project))
            {
                // Prefix notes with project name if there are multiple projects.
                if(Projects.Length > 1)
                {
                    notes += "## " + project + "\n";
                }
                
                var releaseNotes = GetReleaseNotes(project);
                notes += string.Join("\n", releaseNotes.Notes);
                notes += "\n\n";
            }
        }

        System.IO.File.WriteAllText(notesPath, notes);

        var settings = new GitReleaseManagerCreateSettings {
            InputFilePath     = Context.MakeAbsolute((FilePath)notesPath).FullPath,
            Name              = VersionFull,
            Prerelease        = Version != VersionFull,
            TargetCommitish   = "master",
        };

        Context.GitReleaseManagerCreate(GitHubToken, GitHubOwner, GitHubRepository, settings);
        Context.GitReleaseManagerPublish(GitHubToken, GitHubOwner, GitHubRepository, VersionFull);
    }

    /// (TARGET) Publishes the packages to NuGet and GitHub.
    public void Publish()
    {
        PublishNuGet();
        PublishGitHub();
    }
}
