using P7SUniversalViewer.Core;
using Xunit;

namespace P7SUniversalViewer.Tests;
public class SafetyTests
{
    [Theory][InlineData("../../outside.pdf")][InlineData("C:\\private\\file.p7s")][InlineData("..\\evil.txt")][InlineData("CON.p7s")][InlineData("bad:filename.p7s")]
    public void FilenamesSanitized(string input) { var name = ContentSafety.SafeFileName(input, ".pdf"); Assert.DoesNotContain("/", name); Assert.DoesNotContain("\\", name); Assert.DoesNotContain(":", name); Assert.NotEqual("CON.pdf", name); }
    [Theory][InlineData("../outside")][InlineData("..\\outside")][InlineData("C:\\outside")][InlineData("file:stream")]
    public void TraversalRefused(string name) { Assert.Throws<InvalidDataException>(() => ContentSafety.ContainedPath(Path.GetTempPath(), name)); }
    [Fact] public async Task SaveIsExactAndNeverOverwrites() { var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); try { await ContentSafety.SaveNewAsync(dir, "content.txt", [1,2,3]); Assert.Equal(new byte[] {1,2,3}, File.ReadAllBytes(Path.Combine(dir,"content.txt"))); await Assert.ThrowsAsync<IOException>(() => ContentSafety.SaveNewAsync(dir,"content.txt",[9])); Assert.Equal(new byte[] {1,2,3}, File.ReadAllBytes(Path.Combine(dir,"content.txt"))); } finally { Directory.Delete(dir,true); } }
    [Fact] public void TempFilesCleanedOnClearAndClose() { var root = Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")); var temp = new PreviewFiles(root); var first = temp.Write([1],".pdf"); temp.Clear(); Assert.False(File.Exists(first)); var second = temp.Write([2],".txt"); temp.Dispose(); Assert.False(File.Exists(second)); Assert.False(Directory.Exists(temp.SessionDirectory)); Directory.Delete(root); }
    [Fact] public void KeepOptionPreservesOnlyOwnedSession() { var root = Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")); var temp = new PreviewFiles(root) { Keep = true }; var file = temp.Write([1],".pdf"); temp.Clear(); temp.Dispose(); Assert.True(File.Exists(file)); PreviewFiles.CleanupStale(root); Assert.True(File.Exists(file)); Directory.Delete(root,true); }
    [Fact] public void StaleCleanupNeverRemovesUnmarkedFiles() { var root = Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); var other = Path.Combine(root,Guid.NewGuid().ToString("N")); Directory.CreateDirectory(other); File.WriteAllText(Path.Combine(other,"private.txt"),"untouched"); Directory.SetLastWriteTimeUtc(other,DateTime.UtcNow.AddDays(-2)); PreviewFiles.CleanupStale(root); Assert.True(File.Exists(Path.Combine(other,"private.txt"))); Directory.Delete(root,true); }
    [Fact] public void StaleOwnedSessionIsCleaned() { var root = Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")); var temp = new PreviewFiles(root) { Keep = true }; temp.Write([1],".pdf"); temp.Dispose(); File.Delete(Path.Combine(temp.SessionDirectory,".keep")); Directory.SetLastWriteTimeUtc(temp.SessionDirectory,DateTime.UtcNow.AddDays(-2)); PreviewFiles.CleanupStale(root); Assert.False(Directory.Exists(temp.SessionDirectory)); Directory.Delete(root); }
    [Fact] public void ArchivesAreNeverDecompressed() { Assert.Equal(PreviewKind.Unsupported,ContentSafety.Detect("PK\u0003\u0004bomb"u8).Preview); }
    [Fact] public void BinaryIsNotPretendedToBeText() { Assert.Equal(PreviewKind.Unsupported,ContentSafety.Detect(new byte[] {0,1,2,255}).Preview); }
}
