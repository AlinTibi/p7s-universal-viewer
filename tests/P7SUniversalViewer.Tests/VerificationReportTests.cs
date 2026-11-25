using System.Security.Cryptography;
using System.Text.Json;
using P7SUniversalViewer.Core;
using Xunit;

namespace P7SUniversalViewer.Tests;

public class VerificationReportTests
{
    [Fact]
    public void DetachedReportDistinguishesMissingCorrectAndChangedOriginals()
    {
        byte[] original = "Invoice 000123"u8.ToArray();
        byte[] changed = "Invoice 000321"u8.ToArray();
        var cms = VerificationTests.Signed(original, detached: true);
        var inspector = new CmsInspector();
        var missing = VerificationReport.Create("invoice.pdf.p7s", null, inspector.Inspect(cms));
        var valid = VerificationReport.Create("invoice.pdf.p7s", "invoice.pdf", inspector.Inspect(cms, original));
        var invalid = VerificationReport.Create("invoice.pdf.p7s", "invoice.pdf", inspector.Inspect(cms, changed));

        Assert.Equal(ContainerState.DetachedContentRequired, missing.State);
        Assert.Null(missing.DocumentSha256);
        Assert.Equal(Integrity.Valid, valid.Integrity);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(original)), valid.DocumentSha256);
        Assert.Equal(Integrity.Invalid, invalid.Integrity);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(changed)), invalid.DocumentSha256);

        using var json = JsonDocument.Parse(VerificationReport.Export([missing, valid, invalid]));
        var results = json.RootElement.GetProperty("Results");
        Assert.Equal(3, results.GetArrayLength());
        var signer = results[1].GetProperty("Signers")[0];
        Assert.Equal("NotChecked", signer.GetProperty("Revocation").GetString());
        Assert.Equal("Untrusted", signer.GetProperty("Trust").GetString());
        Assert.Equal(64, signer.GetProperty("Chain")[0].GetProperty("Sha256").GetString()!.Length);
        Assert.False(results[1].TryGetProperty("Content", out _));
        Assert.Equal("invoice.pdf", results[1].GetProperty("Original").GetString());
    }

    [Fact]
    public void SuggestsOnlyAnExistingSiblingAfterRemovingTheSignatureExtension()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var original = Path.Combine(directory.FullName, "invoice.pdf");
            var container = original + ".p7s";
            Assert.Null(VerificationReport.SuggestOriginal(container));
            File.WriteAllBytes(original, [1]);
            Assert.Equal(original, VerificationReport.SuggestOriginal(container));
            Assert.Null(VerificationReport.SuggestOriginal(Path.Combine(directory.FullName, "invoice")));
        }
        finally
        {
            File.Delete(Path.Combine(directory.FullName, "invoice.pdf"));
            directory.Delete();
        }
    }
}
