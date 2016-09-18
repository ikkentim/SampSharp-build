# SampSharp-build
SampSharp shared build script code. Can possibly also be used with projects other than SampSharp.

Setup
=====
1. Stick the contents of the `root` directory into the root of your project.
2. Configure `build.cake` in your project root.
3. Create your `.nuspec` files in the `nuspec` folder in your project root.
4. Add this repository as a submodule using `git submodule add https://github.com/ikkentim/SampSharp-build.git cake` (make sure the `cake` directory does not exist yet! If it does, delete it, and commit the deletion first.)
5. Add `tools/` to your `.gitignore`.
6. Setup AppVeyor for your repository.
7. Add the following environment variables to the AppVeyor settings for your repository:
  - **LAGET_KEY**: The NuGet key to publish with
  - **GITHUB_USERNAME**: The GitHub username to create new releases with. \*
  - **GITHUB_PASSWORD**: The GitHub password to create new releases with. \*

_\* personally, I use an account I made specially for automated works for security reasons (Don't forget to give it the rights to the repo ;) )._

Versioning
==========
This build script automatically generates version numbers for you, but you need to set it up correctly.

- Before building, make sure CHANGES.md in your project root is up to date. Add new versions at the top.
- Make the versioning attributes in you `AssemblyInfo.cs` look like this:

```
[assembly: AssemblyVersion("0.0.*")]
[assembly: NeutralResourcesLanguage("en")]
```

These assembly infos will be automatically updated when using the build script. If a tagged version is built on AppVeyor the tag is used as the version, make sure each tag starts with a semantic version, e.g. `1.0.0` or `0.1.0-alpha1`. Something like `v1.0.0` will not work! If something other than a tagged version is build, the build script will use the most recent `CHANGES.md` entry and use it's title as the major.minor version. The patch.build version is constructed by the current date. I've used the same version incremental algorithm as [AssemblyVersionAttribute](https://msdn.microsoft.com/en-us/library/system.reflection.assemblyversionattribute.aspx).

Building
========
- On Windows: run `./build.bat` to build.  
- On Linux/MacOS: run `./build.sh` to build.

Creating New Releases
=====================
1. Build with the `-Target GenerateTag` flag. e.g. `./build.bat -Target GenerateTag`
2. In the results, find the suggested tag/version
3. Update your CHANGES.md (see [versioning](#versioning)).
4. Create a git tag: `git tag <TAG>` using the suggested tag. If you want to create a prerelease, use `<VERSION>-alpha` as tag. You can also number your prereleases like `<VERSION>-alpha2`.
5. Push your tags, `git push --tags` and AppVeyor will take care of the rest.

Example of usage: [ikkentim/SampSharp](https://github.com/ikkentim/SampSharp)
