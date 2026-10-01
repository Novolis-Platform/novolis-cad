<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-cad/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-cad/) · [Source](https://github.com/Novolis-Platform/novolis-cad)
<!-- novolis-pkg-brand:end -->

# Novolis.Cad.Evaluation

Avalonia-free staged evaluation over `CadDocument`: solids → MeshFromSolid → modifiers → instances / preview bags, plus `.cadphys` export and document bounds.

## Install

```bash
dotnet add package Novolis.Cad.Evaluation
```

## API

| API | Purpose |
|-----|---------|
| `CadModelEvaluator` | Staged `Evaluate(CadDocument)` with `CadEvaluationCache` |
| `CadPhysExporter` | Analytic solids → `CadPhysDocument` / `.cadphys.json` |
| `EntityBounds.Compute` | World AABB center/radius for framing |

Tessellation kernels live in `Novolis.Cad.SceneBridge.Tessellation`.
