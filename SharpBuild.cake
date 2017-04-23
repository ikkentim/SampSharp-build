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
      
    private ProjectFile GetProjectFile(string project)
    {
        return new ProjectFile(Context, project);
    }
  
    private void BuildVersion()
    {
        if(_version != null) return;
        
        if(IsAppVeyorTag)
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
    
    private ReleaseNotes GetReleaseNotes(string project)
    {
        var path = string.Format("./CHANGES-{0}.md", project);

        return Context.FileExists(path)
            ? Context.ParseReleaseNotes(path)
            : Context.ParseReleaseNotes("./CHANGES.md");
    }
    
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
    
    private void RestoreProjectFile(string project)
    {
        GetProjectFile(project).Restore();
    }
    
    public void Clean()
    {
        Context.CleanDirectory(BuildPath);
    }
    
    public void Build()
    {
        var settings = new DotNetCoreBuildSettings {
            OutputDirectory = BuildPath,
            Configuration = Configuration,
        };

        foreach(var projectFile in ProjectFiles) {
            Context.DotNetCoreBuild(projectFile.Path.FullPath, settings);
        }
    }
    
    public void Restore()
    {
        var settings = new DotNetCoreRestoreSettings {
            Sources = NuGetSources
        };

        foreach(var projectFile in ProjectFiles) {
            Context.DotNetCoreRestore(projectFile.Path.FullPath, settings);
        }
    }

    public void Pack()
    {
        Context.Information("Packing with version " + VersionFull);

        var settings = new DotNetCorePackSettings {
            VersionSuffix = VersionSuffix,
            Configuration = Configuration,
            OutputDirectory = BuildPath,
        };

        foreach(var project in Projects) {
            PrepareProjectFile(project);
            Context.DotNetCorePack(GetProjectFile(project).Path.FullPath, settings);
            RestoreProjectFile(project);
        }
    }

    public void PublishNuGet()
    {
        var settings = new NuGetPushSettings {
            Source = NuGetSource,
            ApiKey = NuGetKey
        };

        foreach(var project in Projects)
        {
            foreach(var package in Context.GetFiles(BuildPath + "/" + project + ".*.nupkg"))
            {
                Context.NuGetPush(package, settings);
            }
        }
    }

    public void PublishGitHub()
    {
        var notesPath = BuildPath + "/releasenotes-" + VersionFull + ".txt";

        string notes = "";
        
        foreach(var project in Projects)
        {
            notes += "# " + project + "\n";
            notes += string.Join("\n", GetReleaseNotes(project).Notes);
            notes += "\n\n";
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

    public void Publish()
    {
        PublishNuGet();
        PublishGitHub();
    }
}
