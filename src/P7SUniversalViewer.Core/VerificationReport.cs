using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace P7SUniversalViewer.Core;

public sealed record VerificationReport(DateTimeOffset CheckedAtUtc, string Container, string? Original,
    ContainerState State, bool Detached, Integrity Integrity, string? DocumentSha256,
    IReadOnlyList<SignerResult> Signers, string Guidance)
{
    [JsonIgnore]
    public string Summary => State switch
    {
        ContainerState.DetachedContentRequired => "Original required",
        ContainerState.Ready => Integrity.ToString(),
        _ => State.ToString()
    };

    public static VerificationReport Create(string container, string? original, Inspection inspection) =>
        new(DateTimeOffset.UtcNow, Path.GetFileName(container),
            inspection.Detached ? Path.GetFileName(original) : null,
            inspection.State, inspection.Detached, inspection.Integrity,
            inspection.Content is null ? null : Convert.ToHexString(SHA256.HashData(inspection.Content)),
            inspection.Signers, inspection.NextStep);

    public static byte[] Export(IReadOnlyList<VerificationReport> results) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            SchemaVersion = 1,
            Limitations = "Offline inspection. Revocation and timestamp authority tokens are not checked. " +
                "The report records what this application observed and does not establish legal validity.",
            Results = results
        }, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });

    public static string? SuggestOriginal(string container)
    {
        var candidate = Path.ChangeExtension(container, null);
        return candidate is not null &&
            !string.Equals(candidate, container, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(candidate) ? candidate : null;
    }
}
