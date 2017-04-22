# SampSharp-build
SampSharp shared build script code. Can possibly also be used with projects other than SampSharp.

Setup
=====
1. Stick the contents of the `root` directory into the root of your project.
2. Configure `build.cake` in your project root.
3. Add this repository as a submodule using `git submodule add https://github.com/ikkentim/SampSharp-build.git cake` (make sure the `cake` directory does not exist yet! If it does, delete it, and commit the deletion first.)
4. Add `tools/` to your `.gitignore`.
5. Setup AppVeyor for your repository.
6. Add the following environment variables to the AppVeyor settings for your repository:
  - **LAGET_KEY**: The NuGet key to publish with
  - **GITHUB_USERNAME**: The GitHub username to create new releases with. \*
  - **GITHUB_PASSWORD**: The GitHub password to create new releases with. \*

_\* personally, I use an account I made specially for automated works for security reasons (Don't forget to give it the rights to the repo ;) )._

Building
========
- On Windows: run `./build.bat` to build.  
- On Linux/MacOS: run `./build.sh` to build.

Creating New Releases
=====================
1. Make sure you've got a `<Version>`, `<AssemblyVersion>`, `<FileVersion>` and a `<PackageReleaseNotes>` tag in your project file.
2. Update your CHANGES.md (see [versioning](#versioning)).
3. Create a git tag: `git tag <TAG>`. If you want to create a prerelease, use `<VERSION>-alpha` as tag. You can also number your prereleases like `<VERSION>-alpha2`.
4. Push your tags, `git push --tags` and AppVeyor will take care of the rest.

Example of usage: [ikkentim/SampSharp](https://github.com/ikkentim/SampSharp)
