using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using P7SUniversalViewer.Core;
using Xunit;

namespace P7SUniversalViewer.Tests;
public class VerificationTests
{
    public static byte[] Signed(byte[] payload, bool detached = false, int signers = 1, bool expired = false)
    {
        var cms = new SignedCms(new ContentInfo(payload), detached);
        for (int i = 0; i < signers; i++) {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest($"CN=Demonstration signer {i + 1}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(expired ? -1 : 10));
            var signer = new CmsSigner(cert); signer.SignedAttributes.Add(new Pkcs9SigningTime(DateTime.UtcNow)); cms.ComputeSignature(signer);
        }
        return cms.Encode();
    }
    [Fact] public void AttachedIntegrityDoesNotClaimTrust() { var result = new CmsInspector().Inspect(Signed("Demo content"u8.ToArray())); Assert.Equal(Integrity.Valid, result.Integrity); Assert.Equal(CertificateTrust.Untrusted, result.Signers.Single().Trust); Assert.Equal(Revocation.NotChecked, result.Signers[0].Revocation); }
    [Fact] public void AlteredPayloadNeverShowsValidAnywhere() {
        byte[] encoded = Signed("Original demo content"u8.ToArray()); int index = encoded.AsSpan().IndexOf("Original demo content"u8); Assert.True(index >= 0); encoded[index] ^= 1;
        var result = new CmsInspector().Inspect(encoded); Assert.Equal(Integrity.Invalid, result.Integrity); Assert.Equal("INVALID SIGNATURE", result.IntegrityLabel); Assert.Equal(Integrity.Invalid, result.Signers[0].Integrity); Assert.EndsWith("INVALID SIGNATURE", result.Signers[0].Summary);
    }
    [Fact] public void MultipleSignersCheckedIndividually() { var r = new CmsInspector().Inspect(Signed("content"u8.ToArray(), signers: 3)); Assert.Equal(3, r.Signers.Count); Assert.All(r.Signers, s => Assert.Equal(Integrity.Valid, s.Integrity)); }
    [Fact] public void DetachedRequiresOriginalAndRejectsWrongBytes() { var cms = Signed("original"u8.ToArray(), true); var inspector = new CmsInspector(); Assert.Equal(ContainerState.DetachedContentRequired, inspector.Inspect(cms).State); Assert.Equal(Integrity.NotChecked, inspector.Inspect(cms).Signers[0].Integrity); Assert.Equal(Integrity.Valid, inspector.Inspect(cms, "original"u8.ToArray()).Integrity); Assert.Equal(Integrity.Invalid, inspector.Inspect(cms, "modified"u8.ToArray()).Integrity); }
    [Fact] public void EmptyAttachedContentIsNotDetached() { var cms = Signed([1]); var root = new AsnReader(cms, AsnEncodingRules.BER).ReadSequence(); var oid = root.ReadObjectIdentifier(); var data = root.ReadSequence(new Asn1Tag(TagClass.ContextSpecific,0,true)).ReadSequence(); var writer = new AsnWriter(AsnEncodingRules.DER); writer.PushSequence(); writer.WriteObjectIdentifier(oid); var tag = new Asn1Tag(TagClass.ContextSpecific,0,true); writer.PushSequence(tag); writer.PushSequence(); writer.WriteEncodedValue(data.ReadEncodedValue().Span); writer.WriteEncodedValue(data.ReadEncodedValue().Span); data.ReadEncodedValue(); writer.PushSequence(); writer.WriteObjectIdentifier("1.2.840.113549.1.7.1"); writer.PushSequence(tag); writer.WriteOctetString([]); writer.PopSequence(tag); writer.PopSequence(); while(data.HasData) writer.WriteEncodedValue(data.ReadEncodedValue().Span); writer.PopSequence(); writer.PopSequence(tag); writer.PopSequence(); var result = new CmsInspector().Inspect(writer.Encode()); Assert.False(result.Detached); Assert.Equal(ContainerState.Ready,result.State); Assert.Empty(result.Content!); }
    [Fact] public void ExpiredCertificateIsSeparateFromIntegrity() { var r = new CmsInspector().Inspect(Signed("content"u8.ToArray(), expired: true)); Assert.Equal(Integrity.Valid, r.Integrity); Assert.Equal(CertificateDates.Expired, r.Signers[0].Dates); }
    [Theory][InlineData("")][InlineData("not a CMS")][InlineData("\0\u0001\u0002")] public void BadContainersAreControlledErrors(string input) { var r = new CmsInspector().Inspect(Encoding.UTF8.GetBytes(input)); Assert.Equal(ContainerState.Error, r.State); Assert.Equal(Integrity.NotChecked, r.Integrity); }
    [Fact] public void PayloadBoundaryIsEnforced() { var inspector = new CmsInspector(new(MaxPayloadBytes: 8)); Assert.Equal(Integrity.Valid, inspector.Inspect(Signed(new byte[8])).Integrity); Assert.Equal(ContainerState.Error, inspector.Inspect(Signed(new byte[9])).State); }
    [Fact] public void InputBoundaryIsEnforced() { Assert.Equal(ContainerState.Error, new CmsInspector(new(MaxInputBytes: 8)).Inspect(new byte[9]).State); }
    [Fact] public void SignerLimitIsEnforced() { Assert.Equal(ContainerState.Error, new CmsInspector(new(MaxSigners: 1)).Inspect(Signed([1], signers: 2)).State); }
    [Fact] public void NestingLimitIsEnforced() { Assert.Equal(ContainerState.Error, new CmsInspector(new(MaxAsnDepth: 2)).Inspect(Signed([1])).State); }
    [Fact] public void ExtractionIsByteForByte() { var bytes = Encoding.UTF8.GetBytes("Document de test — România 🙂\r\n"); Assert.Equal(bytes, new CmsInspector().Inspect(Signed(bytes)).Content); }
}
