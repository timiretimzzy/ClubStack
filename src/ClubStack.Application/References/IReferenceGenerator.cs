namespace ClubStack.Application.References;

/// <summary>
/// Generates deterministic, concurrency-safe, human-readable business references
/// (e.g. MEM-000001). Generation is scoped per prefix so different modules never
/// share a counter.
/// </summary>
public interface IReferenceGenerator
{
    /// <summary>
    /// Allocates the next reference for <paramref name="prefix"/> and returns its
    /// formatted value (e.g. <c>"PO-000047"</c>).
    /// </summary>
    Task<string> GenerateAsync(string prefix, int width = 6, CancellationToken cancellationToken = default);
}