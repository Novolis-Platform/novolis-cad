# Release

Packages publish to GitHub Packages on merge to `main` via the org `dotnet-merge-publish` workflow. Versioning follows `build/version.json` (`2026.1.*`). Consumers PackageReference `2026.1.*` from nuget.org + GitHub Packages only.
