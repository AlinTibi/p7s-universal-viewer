using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace P7SUniversalViewer.Core;

public enum Integrity { NotChecked, Valid, Invalid, Unsupported }
public enum CertificateTrust { Unknown, Trusted, Untrusted }
public enum CertificateDates { Unknown, Valid, Expired, NotYetValid }
public enum Revocation { NotChecked, Good, Revoked, Unknown }
public enum ContainerState { Ready, DetachedContentRequired, Unsupported, Error }
public sealed record SignerResult(string Name, string Issuer, string SerialNumber, string Thumbprint,
    string SignatureAlgorithm, string DigestAlgorithm, DateTimeOffset? SigningTime,
    DateTimeOffset? NotBefore, DateTimeOffset? NotAfter, Integrity Integrity,
    CertificateTrust Trust, CertificateDates Dates, Revocation Revocation, string Detail)
{
    public string IntegrityLabel => Integrity switch { Integrity.Valid => "VALID SIGNATURE", Integrity.Invalid => "INVALID SIGNATURE", Integrity.Unsupported => "UNSUPPORTED", _ => "NOT VERIFIED" };
    public string Summary => $"{Name} — {IntegrityLabel}";
    public string CertificateDetail => $"Issuer: {Issuer}\nSerial: {SerialNumber}\nThumbprint: {Thumbprint}\nSignature: {SignatureAlgorithm}\nDigest: {DigestAlgorithm}\nNot before: {NotBefore:u}\nNot after: {NotAfter:u}\nSigning time (untrusted assertion): {(SigningTime is null ? "Not present" : SigningTime.Value.ToString("u"))}\nRevocation: {Revocation}\n{Detail}";
}
public sealed record Inspection(ContainerState State, bool Detached, byte[]? Content,
    IReadOnlyList<SignerResult> Signers, string Message)
{
    public Integrity Integrity => State != ContainerState.Ready || Signers.Count == 0 ? Integrity.NotChecked :
        Signers.Any(s => s.Integrity == Integrity.Invalid) ? Integrity.Invalid :
        Signers.All(s => s.Integrity == Integrity.Valid) ? Integrity.Valid : Integrity.Unsupported;
    public string IntegrityLabel => State switch {
        ContainerState.DetachedContentRequired => "DETACHED CONTENT REQUIRED", ContainerState.Error => "ERROR",
        ContainerState.Unsupported => "UNSUPPORTED", _ => Integrity switch { Integrity.Valid => "VALID SIGNATURE", Integrity.Invalid => "INVALID SIGNATURE", _ => "NOT VERIFIED" } };
    public string TrustLabel => Signers.Count == 0 ? "Certificate trust: UNKNOWN" :
        Signers.All(s => s.Trust == CertificateTrust.Trusted) ? "Certificate trust: TRUSTED" : "Certificate trust: UNTRUSTED / UNKNOWN";
}
public sealed record ResourceLimits(long MaxInputBytes = 64 * 1024 * 1024, int MaxPayloadBytes = 48 * 1024 * 1024,
    int MaxSigners = 32, int MaxCertificates = 128, int MaxAsnDepth = 32, int MaxAsnItems = 100000);

public sealed class CmsInspector(ResourceLimits? limits = null)
{
    public ResourceLimits Limits { get; } = limits ?? new();
    public async Task<Inspection> InspectFileAsync(string path, string? original = null, CancellationToken cancellation = default)
    {
        try { return await Task.Run(async () => Inspect(await ReadBoundedAsync(path, Limits.MaxInputBytes, cancellation),
            original is null ? null : await ReadBoundedAsync(original, Limits.MaxPayloadBytes, cancellation)), cancellation); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException) { return Error(e.Message); }
    }
    public static async Task<byte[]> ReadBoundedAsync(string path, long maximum, CancellationToken cancellation = default)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        if (input.Length > maximum || input.Length > int.MaxValue) throw new InvalidDataException($"File exceeds the {maximum / 1024 / 1024} MiB limit.");
        var bytes = new byte[(int)input.Length];
        await input.ReadExactlyAsync(bytes, cancellation);
        if (input.ReadByte() != -1) throw new InvalidDataException("File changed while reading.");
        return bytes;
    }
    public Inspection Inspect(byte[] encoded, byte[]? original = null)
    {
        try {
            if (encoded.Length == 0) return Error("The file is empty.");
            if (encoded.LongLength > Limits.MaxInputBytes || original?.Length > Limits.MaxPayloadBytes) return Error("Resource limit exceeded.");
            ValidateStructure(encoded);
            bool detached = IsDetached(encoded);
            var cms = detached && original is not null ? new SignedCms(new ContentInfo(original), true) : new SignedCms();
            cms.Decode(encoded);
            if (cms.SignerInfos.Count == 0) return new(ContainerState.Unsupported, detached, null, [], "CMS has no signers.");
            if (cms.SignerInfos.Count > Limits.MaxSigners || cms.Certificates.Count > Limits.MaxCertificates) return Error("Too many signers or certificates.");
            if (cms.ContentInfo.Content.Length > Limits.MaxPayloadBytes) return Error("Extracted content exceeds the size limit.");
            bool needsContent = detached && original is null;
            var results = cms.SignerInfos.Cast<SignerInfo>().Select(s => Check(s, cms.Certificates, needsContent)).ToArray();
            return new(needsContent ? ContainerState.DetachedContentRequired : ContainerState.Ready, detached,
                needsContent ? null : cms.ContentInfo.Content, results,
                needsContent ? "Detached signature detected. Select the original file to verify this signature." :
                "Integrity and certificate trust are separate checks. Revocation is not checked; no network lookup is performed.");
        }
        catch (Exception e) when (e is CryptographicException or AsnContentException or InvalidDataException or ArgumentException) { return Error("Cannot inspect this CMS container: " + e.Message); }
    }
    private void ValidateStructure(ReadOnlyMemory<byte> bytes)
    {
        int count = 0;
        void Visit(AsnReader reader, int depth) {
            if (depth > Limits.MaxAsnDepth) throw new InvalidDataException("ASN.1 nesting limit exceeded.");
            while (reader.HasData) {
                if (++count > Limits.MaxAsnItems) throw new InvalidDataException("ASN.1 item limit exceeded.");
                var tag = reader.PeekTag();
                var encoded = reader.ReadEncodedValue();
                if (tag.IsConstructed) {
                    AsnDecoder.ReadEncodedValue(encoded.Span, AsnEncodingRules.BER, out int offset, out int length, out _);
                    Visit(new AsnReader(encoded.Slice(offset, length), AsnEncodingRules.BER), depth + 1);
                }
            }
        }
        Visit(new AsnReader(bytes, AsnEncodingRules.BER), 0);
    }
    private static bool IsDetached(byte[] bytes)
    {
        var root = new AsnReader(bytes, AsnEncodingRules.BER);
        var contentInfo = root.ReadSequence(); root.ThrowIfNotEmpty();
        if (contentInfo.ReadObjectIdentifier() != "1.2.840.113549.1.7.2") throw new InvalidDataException("Only CMS SignedData is supported.");
        var wrapper = contentInfo.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true)); contentInfo.ThrowIfNotEmpty();
        var signed = wrapper.ReadSequence(); wrapper.ThrowIfNotEmpty();
        signed.ReadInteger(); signed.ReadSetOf();
        var encapsulated = signed.ReadSequence(); encapsulated.ReadObjectIdentifier();
        return !encapsulated.HasData;
    }
    private static SignerResult Check(SignerInfo signer, X509Certificate2Collection certificates, bool needsContent)
    {
        var integrity = Integrity.NotChecked; string detail = "";
        if (!needsContent) {
            try { signer.CheckSignature(true); integrity = Integrity.Valid; }
            catch (CryptographicException e) { integrity = Integrity.Invalid; detail = e.Message; }
        }
        var cert = signer.Certificate; var trust = CertificateTrust.Unknown; var dates = CertificateDates.Unknown;
        if (cert is not null) {
            var now = DateTime.UtcNow;
            dates = now < cert.NotBefore.ToUniversalTime() ? CertificateDates.NotYetValid : now > cert.NotAfter.ToUniversalTime() ? CertificateDates.Expired : CertificateDates.Valid;
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.DisableCertificateDownloads = true;
            chain.ChainPolicy.ExtraStore.AddRange(certificates);
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreNotTimeValid;
            trust = chain.Build(cert) ? CertificateTrust.Trusted : CertificateTrust.Untrusted;
            detail += "\n" + string.Join("; ", chain.ChainStatus.Select(s => s.Status.ToString()));
        }
        DateTimeOffset? time = null;
        foreach (CryptographicAttributeObject attribute in signer.SignedAttributes) {
            if (attribute.Oid.Value == "1.2.840.113549.1.9.5" && attribute.Values.Count == 1) {
                try { time = new Pkcs9SigningTime(attribute.Values[0].RawData).SigningTime.ToUniversalTime(); } catch (CryptographicException) { detail += "\nMalformed signing-time assertion."; }
            }
        }
        if (signer.UnsignedAttributes.Cast<CryptographicAttributeObject>().Any(a => a.Oid.Value == "1.2.840.113549.1.9.16.2.14"))
            detail += "\nTimestamp token present; timestamp authority and token are NOT verified.";
        return new(cert?.GetNameInfo(X509NameType.SimpleName, false) ?? signer.SignerIdentifier.Value?.ToString() ?? "Unknown signer",
            cert?.Issuer ?? "Unknown", cert?.SerialNumber ?? "Unknown", cert?.Thumbprint ?? "Unknown",
            signer.SignatureAlgorithm.FriendlyName ?? signer.SignatureAlgorithm.Value ?? "Unknown",
            signer.DigestAlgorithm.FriendlyName ?? signer.DigestAlgorithm.Value ?? "Unknown", time,
            cert is null ? null : new DateTimeOffset(cert.NotBefore), cert is null ? null : new DateTimeOffset(cert.NotAfter),
            integrity, trust, dates, Revocation.NotChecked, detail.Trim());
    }
    private static Inspection Error(string message) => new(ContainerState.Error, false, null, [], message);
}
