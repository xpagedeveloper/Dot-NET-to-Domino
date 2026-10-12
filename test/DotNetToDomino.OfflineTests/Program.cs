using System.Reflection;
using DotNetToDomino;
using DotNetToDomino.Interop;

var tests = new List<(string Name, Action Test)>();
void Test(string name, Action test) => tests.Add((name, test));
void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
void Throws<T>(Action action) where T : Exception
{
    try { action(); throw new InvalidOperationException($"Expected {typeof(T).Name}."); }
    catch (T) { }
}

Test("record order and identity", () =>
{
    var records = new[]
    {
        new NotesRichTextRecordData(0, 0, 0x0001, new byte[] { 1, 0 }, "", 0, ""),
        new NotesRichTextRecordData(0, 1, 0x0002, new byte[] { 2, 0 }, "", 0, ""),
        new NotesRichTextRecordData(1, 0, 0x0003, new byte[] { 3, 0 }, "", 0, "")
    };
    var result = NotesRichTextCdTransform.Preserve(records);
    Assert(result.Count == 3 && result[2].SegmentIndex == 1, "Record order was not preserved.");
    Throws<NotesException>(() => NotesRichTextCdTransform.Transform(records.Reverse().ToArray(), static r => NotesRichTextRecordRewrite.Preserve(r)));
});

Test("segment grouping and padding", () =>
{
    var records = new[]
    {
        new NotesRichTextRecordRewrite(0, 0, 0x0001, new byte[] { 1, 2, 3 }),
        new NotesRichTextRecordRewrite(0, 1, 0x0002, new byte[] { 2, 3 }),
        new NotesRichTextRecordRewrite(1, 0, 0x0003, new byte[] { 3, 4, 5 })
    };
    var segments = NotesRichTextCdTransform.GroupCanonicalSegments(records);
    Assert(segments.Count == 2 && segments[0].Length == 6 && segments[1].Length == 4, "Canonical segment grouping/padding failed.");
});

Test("canonical headers and corrupt data", () =>
{
    var valid = new[] { new NotesRichTextRecordRewrite(0, 0, 0x1201, new byte[] { 1, 0, 7 }) };
    NotesRichTextCdTransform.ValidateForPersistence(valid);
    Throws<NotesException>(() => NotesRichTextCdTransform.ValidateForPersistence([new NotesRichTextRecordRewrite(0, 0, 0x1201, [2, 0])]));
    Throws<NotesException>(() => NotesRichTextCdTransform.ValidateForPersistence([new NotesRichTextRecordRewrite(-1, 0, 1, [1, 0])]));
    Throws<NotesException>(() => NotesRichTextCdTransform.ValidateForPersistence([new NotesRichTextRecordRewrite(0, 0, 0xFF01, [1, 0])]));
});

Test("backup and restore clones data", () =>
{
    var source = new[] { new NotesRichTextRecordData(0, 0, 1, new byte[] { 1, 0, 9 }, "", 0, "") };
    var copy = NotesRichTextCdTransform.Preserve(source);
    source[0].Data[2] = 77;
    Assert(copy[0].Data[2] == 9, "Preserved record was not cloned.");
});

Test("MIME encoding fixtures", () =>
{
    var type = typeof(NotesMIMEEntity);
    var encode = type.GetMethod("EncodeContentBytes", BindingFlags.NonPublic | BindingFlags.Static)!;
    var decode = type.GetMethod("DecodeQuotedPrintable", BindingFlags.NonPublic | BindingFlags.Static)!;
    foreach (var value in new[] { 1724, 1725, 1728 }) Assert(((byte[])encode.Invoke(null, [new byte[] { 0, 1, 255 }, value])!).Length == 3, $"Encoding {value} changed raw bytes.");
    var base64 = (byte[])encode.Invoke(null, [new byte[] { 1, 2, 3 }, 1727])!;
    Assert(Convert.FromBase64String(System.Text.Encoding.ASCII.GetString(base64)).SequenceEqual(new byte[] { 1, 2, 3 }), "Base64 fixture failed.");
    var qp = (byte[])encode.Invoke(null, [new byte[] { 0, 61, 255 }, 1726])!;
    Assert(System.Text.Encoding.ASCII.GetString(qp) == "=00=3D=FF", "Quoted-printable fixture failed.");
    var decoded = (byte[])decode.Invoke(null, [qp])!;
    Assert(decoded.SequenceEqual(new byte[] { 0, 61, 255 }), "Quoted-printable decode failed.");
});

Test("nested multipart fixture", () =>
{
    const string boundary = "offline-boundary";
    var fixture = $"--{boundary}\r\nContent-Type: text/plain\r\n\r\nfirst\r\n--{boundary}\r\nContent-Type: multipart/alternative\r\n\r\nsecond\r\n--{boundary}--\r\n";
    var parts = fixture.Split("--" + boundary, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    Assert(parts.Count(part => part.Contains("Content-Type:", StringComparison.OrdinalIgnoreCase)) == 2, "Nested multipart fixture is malformed.");
});

Test("attachment fixture validation", () =>
{
    var file = Path.Combine(Path.GetTempPath(), "DotNetToDomino-offline-attachment.bin");
    try { File.WriteAllBytes(file, [0, 1, 2, 255]); Assert(new FileInfo(file).Length == 4, "Attachment fixture size mismatch."); Assert(Path.GetFileName(file).Length > 0, "Attachment fixture name missing."); }
    finally { if (File.Exists(file)) File.Delete(file); }
});

var failures = new List<string>();
foreach (var (name, test) in tests)
    try { test(); Console.WriteLine($"PASS {name}"); } catch (Exception ex) { failures.Add($"{name}: {ex.Message}"); Console.Error.WriteLine($"FAIL {failures[^1]}"); }
Console.WriteLine($"Offline tests: {tests.Count - failures.Count}/{tests.Count} passed.");
Environment.ExitCode = failures.Count == 0 ? 0 : 1;
