<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-cad">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
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
