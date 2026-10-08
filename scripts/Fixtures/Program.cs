using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

var directory = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/fixtures"); Directory.CreateDirectory(directory);
byte[] Create(byte[] payload, bool detached = false, int count = 1) {
    var cms = new SignedCms(new ContentInfo(payload), detached);
    for (int i = 0; i < count; i++) {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN=ALMARFELD demonstration signer {i + 1}",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(30));
        var signer = new CmsSigner(certificate); signer.SignedAttributes.Add(new Pkcs9SigningTime(DateTime.UtcNow)); cms.ComputeSignature(signer);
    }
    return cms.Encode();
}
var text = Encoding.UTF8.GetBytes("ALMARFELD · Demonstration document\n\nPROJECT HANDOVER\nReference: DEMO-2026-0042\nStatus: Ready for review\n\nThis synthetic document demonstrates attached digital signatures.\nIt contains no customer information.\n\nReview checklist\n  • Inspect every signer\n  • Check integrity separately from certificate trust\n  • Extract the original document without changing its bytes\n\nRomanian text: Semnătură digitală — verificare locală.\n\nThe demonstration certificate is self-signed and untrusted.\nNo legal validity is implied.\n");
File.WriteAllBytes(Path.Combine(directory,"handover.p7s"),Create(text));
var altered = Create(text); int position = altered.AsSpan().IndexOf(Encoding.UTF8.GetBytes("PROJECT HANDOVER")); altered[position] ^= 1;
File.WriteAllBytes(Path.Combine(directory,"altered.p7s"),altered);
File.WriteAllBytes(Path.Combine(directory,"multiple-signers.p7s"),Create(text,count:3));
File.WriteAllBytes(Path.Combine(directory,"detached.p7s"),Create(text,true));
File.WriteAllBytes(Path.Combine(directory,"original.txt"),text);
File.WriteAllText(Path.Combine(directory,"wrong-original.txt"),"Wrong content");
File.WriteAllBytes(Path.Combine(directory,"unsupported.p7s"),Create([0,1,2,255,0,16]));
File.WriteAllBytes(Path.Combine(directory,"empty.p7s"),[]);
File.WriteAllText(Path.Combine(directory,"malformed.p7s"),"Not a CMS container");
File.WriteAllBytes(Path.Combine(directory,"ședință-document.p7s"),Create(text));
if (args.Length > 1) File.WriteAllBytes(Path.Combine(directory,"image.p7s"),Create(File.ReadAllBytes(args[1])));
var pdf = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int> {0};
var commands = "BT /F1 20 Tf 48 740 Td (ALMARFELD Demonstration) Tj 0 -40 Td /F1 12 Tf (Synthetic signed PDF - no customer data) Tj 0 -30 Td (Integrity and certificate trust are separate checks.) Tj ET";
string[] objects = ["<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", $"<< /Length {commands.Length} >>\nstream\n{commands}\nendstream"];
for (int i = 0; i < objects.Length; i++) { offsets.Add(pdf.Length); pdf.Append($"{i+1} 0 obj\n{objects[i]}\nendobj\n"); }
var xref = pdf.Length; pdf.Append($"xref\n0 {offsets.Count}\n0000000000 65535 f \n"); foreach (var offset in offsets.Skip(1)) pdf.Append($"{offset:D10} 00000 n \n"); pdf.Append($"trailer << /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
File.WriteAllBytes(Path.Combine(directory,"document.pdf.p7s"),Create(Encoding.ASCII.GetBytes(pdf.ToString())));
Console.WriteLine("Synthetic fixtures created. No private keys are persisted.");
