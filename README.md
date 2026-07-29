# novolis-cad

Avalonia-free CAD interchange packages for Novolis.

| Package | Role |
|---------|------|
| **Novolis.Cad.Primitives** | `.cadjson` / `.cadphys` DTOs, workspace enums, vec helpers |

Schemas: [novolis-governance](https://github.com/Novolis-Platform/novolis-governance) (`schemas/cad`). UI editor: [Novolis.Avalonia.Cad](https://github.com/Novolis-Platform/novolis-avalonia).

## Build

```powershell
dotnet build Novolis.Cad.slnx
dotnet test Novolis.Cad.slnx
```

Cross-repo local iteration: open `Novolis.Platform.slnx` (ProjectReference mode). Do not use local NuGet folder feeds.
