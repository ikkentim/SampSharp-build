public class ProjectFile
{
    public ProjectFile(ICakeContext context, string name)
    {
        Context = context;
        Name = name;
    }
    
    private ICakeContext Context { get; set; }

    public string Name { get; private set; }

    public FilePath Path
    {
        get { return string.Format("./src/{0}/{0}.csproj", Name); }
    }

    public FilePath BackupPath
    {
        get { return Path.AppendExtension(".projfile.bak"); }
    }

    public bool BackupExists
    {
        get { return Context.FileExists(BackupPath); }
    }

    public void Restore()
    {
        if(!BackupExists) return;

        Context.Information("Restoring " + Name);
        Context.DeleteFile(Path);
        Context.MoveFile(BackupPath, Path);
    }

    public void Backup()
    {
        if(BackupExists) Restore();
        
        Context.Information("Backing up " + Name);

        Context.CopyFile(Path, BackupPath);
    }

    public void Replace(string pattern, string replacement)
    {
        var abs = Context.MakeAbsolute(Path).FullPath;
        var content = System.IO.File.ReadAllText(abs);

        var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.Multiline);
        content = regex.Replace(content, replacement);

        System.IO.File.WriteAllText(abs, content);
    }
}