#tool "nuget:?package=gitreleasemanager"

#load "./ProjectFile.cake"

public class SharpBuild
{
    private string _configuration = "Release";
    private string _version;
    private string _versionFull;
    private string _versionSuffix;

    protected ICakeContext Context { get; private set; }
    public string[] Projects { get; private set; }
    public string BuildPathDebug { get; set; }
    public string BuildPathRelease { get; set; }

    public string NuGetKey { get; set; }
    public string NuGetSource { get; set; }
    public string[] NuGetSources { get; set; }
    public string GitHubReleaseUser { get; set; }
    public string GitHubReleasePass { get; set; }
    public string GitHubRepoUser { get; set; }
    public string GitHubRepoName { get; set; }

    public IEnumerable<ProjectFile> ProjectFiles
    {
        get { return Projects.Select(GetProjectFile); }
    }

    public string BuildPath
    {
        get { return IsRelease ? BuildPathRelease : BuildPathDebug; }
    }

    public string Configuration
    { 
        get { return _configuration; }
        set {  _configuration = value.ToLower() == "release" ? "Release" : "Debug"; } 
    }
    
    public bool IsRelease
    {
        get
        {
            return Configuration == "Release";
        }
    }
    
    public bool IsRunningOnAppVeyor
    {
        get
        {
            return IsRelease && Context.BuildSystem().AppVeyor.IsRunningOnAppVeyor;
        }
    }
    
    public bool IsAppVeyorTag
    {
        get
        {
            return IsRunningOnAppVeyor && Context.BuildSystem().AppVeyor.Environment.Repository.Tag.IsTag;
        }
    }
    
    public string AppVeyorTagName
    {
        get
        {
            return Context.BuildSystem().AppVeyor.Environment.Repository.Tag.Name;
        }
    }
    
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
    
    public SharpBuild(ICakeContext context, string repoUser, string repoName, params string[] projects) : this(context, projects)
    {
        GitHubRepoUser = repoUser;
        GitHubRepoName = repoName;
    }

    public SharpBuild(ICakeContext context, params string[] projects)
    {
        Context = context;

        BuildPathRelease = "./bin/Release";
        BuildPathDebug = "./bin/Debug";
        NuGetKey = Context.EnvironmentVariable("LAGET_KEY");
        NuGetSource = "http://nuget.timpotze.nl/upload";
        GitHubReleaseUser = Context.EnvironmentVariable("GITHUB_USERNAME");
        GitHubReleasePass = Context.EnvironmentVariable("GITHUB_PASSWORD");
        NuGetSources = new [] {
            "https://api.nuget.org/v3/index.json",
            "http://nuget.timpotze.nl/api/v2/"
        };
        Configuration = Context.Argument("configuration", "Release");

        Projects = projects;
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
        
        if (IsAppVeyorTag)
        {
            _versionFull = AppVeyorTagName;
            var parts = _versionFull.Split(new []{'-'}, 2);
            _version = parts[0];
            _versionSuffix = parts.Length > 1 ? parts[1] : null;
        }
        else if (IsRunningOnAppVeyor)
        {
            _version = "0.0.0";
            _versionSuffix = "appveyorbuild";
            _versionFull = "0.0.0-appveyorbuild";
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

        return VersionFull.Trim().ToLower() == notes.RawVersionLine.Trim().TrimStart('#').Trim().ToLower();
    }

    /// Backs up and replaces the project (.csproj) file with a file containing the version and release notes of the project.
    private void PrepareProjectFile(string project)
    {
        var file = GetProjectFile(project);
        file.Backup();
        
        var notes = string.Join("\n", GetReleaseNotes(project).Notes);
        notes = notes.Replace("<", "&lt;");

        file.Replace("<AssemblyVersion>(.*)</AssemblyVersion>", "<AssemblyVersion>" + Version + "</AssemblyVersion>");
        file.Replace("<FileVersion>(.*)</FileVersion>", "<FileVersion>" + Version + "</FileVersion>");
        file.Replace("<Version>(.*)</Version>", "<Version>" + VersionFull + "</Version>");
        file.Replace("<PackageReleaseNotes>(.*)</PackageReleaseNotes>", "<PackageReleaseNotes>" + notes + "</PackageReleaseNotes>");
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
        if (string.IsNullOrEmpty(GitHubReleaseUser) ||
            string.IsNullOrEmpty(GitHubReleasePass) ||
            string.IsNullOrEmpty(GitHubRepoUser) ||
            string.IsNullOrEmpty(GitHubRepoName))
        {
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

        Context.GitReleaseManagerCreate(GitHubReleaseUser, GitHubReleasePass, GitHubRepoUser, GitHubRepoName, settings);
        Context.GitReleaseManagerPublish(GitHubReleaseUser, GitHubReleasePass, GitHubRepoUser, GitHubRepoName, VersionFull);
    }

    /// (TARGET) Publishes the packages to NuGet and GitHub.
    public void Publish()
    {
        PublishNuGet();
        PublishGitHub();
    }
}
