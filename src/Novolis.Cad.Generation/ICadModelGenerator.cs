namespace Novolis.Cad.Generation;

/// <summary>Domain model generator. Headless-safe; no UI or camera dependencies.</summary>
public interface ICadModelGenerator
{
    string Id { get; }

    string DisplayName { get; }

    ValueTask<CadGenerationResult> GenerateAsync(
        CadGenerationRequest request,
        CancellationToken cancellationToken = default);
}
