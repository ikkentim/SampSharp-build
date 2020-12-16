# SampSharp-build
SampSharp shared build script code. Can possibly also be used with projects other than SampSharp.

Setup
=====
1. Stick the contents of the `root` directory into the root of your project
1. Configure `build.cake` in your project root
1. Add this repository as a submodule using `git submodule add https://github.com/ikkentim/SampSharp-build.git cake`
1. Add `tools/` to your `.gitignore`
1. Add the following environment variables to the AppVeyor settings for your repository:
  - **NUGET_KEY**: The NuGet key to publish with
  - **GITHUB_TOKEN**: The GitHub token to use to publish a release
1. Make sure your projects are at src/PROJECTNAME/PROJECTNAME.csproj
1. Install the dotnet-cake tool: `dotnet tool install Cake.Tool`
1. Add a GitHub Action to the project
1. Make sure your project is configured to build (in release mode) to `repo-root/bin`
1. Add the following to your csproj files:
```xml
    <!-- Add to the default ProjectGroup -->
    <Version>0.0.0-localbuild</Version>
    <AssemblyVersion>0.0.0</AssemblyVersion>
    <PackageReleaseNotes>placeholder</PackageReleaseNotes>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <AllowedOutputExtensionsInPackageBuildOutputFolder>$(AllowedOutputExtensionsInPackageBuildOutputFolder);.pdb</AllowedOutputExtensionsInPackageBuildOutputFolder>

  <!-- Add to Project -->
  <PropertyGroup Condition="'$(GITHUB_ACTIONS)' == 'true'">
     <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.SourceLink.GitHub" Version="1.0.0" PrivateAssets="All"/>
  </ItemGroup>

```

GitHub Action
=============
This template works as a good base, you possibly need to tweak it.

```yml
name: Cake
on:
  push:
    branches: [ master ]
    paths:
      - src/**
      - CHANGES.md

  pull_request:
    branches: [ master ]
    paths:
      - .github/workflows
      - src/**
      - CHANGES.md

  workflow_dispatch:

jobs:
  build:
    runs-on: windows-latest

    steps:
      - name: Checkout repo
        uses: actions/checkout@v2

      - name: Checkout submodules
        run: git submodule update --init --recursive

      - name: Run the Cake script
        uses: cake-build/cake-action@v1
        with:
          script-path: build.cake
          target: CI

      - name: Upload artifacts
        uses: actions/upload-artifact@v2
        with:
          name: nuget-packages
          path: |
            bin/Release/*.nupkg
            bin/**/*.dll
            bin/**/*.pdb
            bin/**/*.xml
```

Building
========
- Run `dotnet cake` to build.  

Creating New Releases
=====================
1. Update your CHANGES.md (see [versioning](#versioning)).
1. Create a git tag: `git tag <TAG>`. If you want to create a prerelease, use `<VERSION>-alpha` as tag. You can also number your prereleases like `<VERSION>-alpha2`.
1. Push your tags, `git push --tags` and the GitHub Action will take care of the rest.

Example of usage: [ikkentim/SampSharp](https://github.com/ikkentim/SampSharp)
