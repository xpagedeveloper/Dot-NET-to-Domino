using System.Reflection;
using System.Runtime.InteropServices;
using DotNetToDomino;

// Test-only native calls: the public NuGet document surface has no Sign/Encrypt methods.
internal static class NotesItemCryptoTests
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NoteCall(uint note);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Update(uint note, ushort flags);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Verify(uint note, nint name, nint when, nint signer, nint certifier);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Encrypt(uint note, ushort flags, out uint encrypted);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Decrypt(uint note, nint id, uint flags, nint cipher, uint reserved, nint reservedPointer);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int State(uint note, out byte signed, out byte sealedNote);

    internal static void Run(NotesSession session, string server, string? path, Action<string, Action> check)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        uint Handle(NotesDocument document) => checked((uint)(nint)(typeof(NotesDocument).GetProperty("Handle", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(document) ?? throw new InvalidOperationException("Document handle unavailable.")));
        void Status(ushort status, string operation) { if (status != 0) throw new InvalidOperationException($"{operation} failed: 0x{status:X4}."); }
        foreach (var encryption in new[] { false, true })
            check("item surface regression native " + (encryption ? "signed encrypted save reopen" : "signature save reopen tamper"), () =>
            {
                var runtime = Environment.GetEnvironmentVariable("NOTES_RUNTIME") ?? throw new InvalidOperationException("Notes runtime missing.");
                var library = NativeLibrary.Load(Path.Combine(runtime, "nnotes.dll"));
                try
                {
                    T Export<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));
                    using var database = session.GetDatabase(server, path, false) ?? throw new InvalidOperationException("Database missing.");
                    using var doc = database.CreateDocument();
                    using var item = doc.ReplaceItemValue("Secret", "Åäö signed secret");
                    if (doc.IsSigned || doc.Signer.Length != 0 || doc.Verifier.Length != 0)
                        throw new InvalidOperationException("Unsigned document reported a signature.");
                    item.IsSigned = true;
                    item.IsEncrypted = encryption;
                    doc.Save();
                    var unid = doc.UniversalID;
                    try
                    {
                        doc.Sign();
                        if (!doc.IsSigned || !string.Equals(doc.Signer, session.UserName, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Public Sign/Signer failed.");
                        var expectedVerifier = doc.Verifier;
                        Status(Export<Verify>("NSFNoteVerifySignature")(Handle(doc), 0, 0, 0, 0), "Verify signature");
                        if (encryption)
                        {
                            // ENCRYPT_WITH_USER_PUBLIC_KEY=0x0001; uses the authenticated ID only.
                            Status(Export<Encrypt>("NSFNoteCopyAndEncrypt")(Handle(doc), 1, out var encrypted), "Encrypt");
                            try { Status(Export<Update>("NSFNoteUpdate")(encrypted, 0), "Save encrypted note"); }
                            finally { Status(Export<NoteCall>("NSFNoteClose")(encrypted), "Close encrypted note"); }
                        }
                        else doc.Save();
                        using var reopened = database.GetDocumentByUNID(unid) ?? throw new InvalidOperationException("Saved note missing.");
                        var handle = Handle(reopened);
                        _ = Export<State>("NSFNoteIsSignedOrSealed")(handle, out var signed, out var sealedNote);
                        // The signature itself is sealed until decryption.
                        if ((!encryption && signed == 0) || (encryption && sealedNote == 0)) throw new InvalidOperationException($"Saved note lost its cryptographic state: signed={signed}, sealed={sealedNote}.");
                        if (encryption) Status(Export<Decrypt>("NSFNoteCipherDecrypt")(handle, 0, 0, 0, 0, 0), "Decrypt");
                        Status(Export<Verify>("NSFNoteVerifySignature")(handle, 0, 0, 0, 0), "Verify saved signature");
                        if (reopened.Signer != doc.Signer || reopened.Verifier != expectedVerifier)
                            throw new InvalidOperationException("Public signer/verifier changed after persistence.");
                        using var saved = reopened.GetFirstItem("Secret") ?? throw new InvalidOperationException("Secret missing.");
                        if (saved.Text != "Åäö signed secret") throw new InvalidOperationException("Cryptographic round trip corrupted value.");
                        if (!encryption)
                        {
                            saved.Values = ["tampered value"];
                            reopened.Save();
                            using var tampered = database.GetDocumentByUNID(unid) ?? throw new InvalidOperationException("Modified note missing.");
                            if (Export<Verify>("NSFNoteVerifySignature")(Handle(tampered), 0, 0, 0, 0) == 0) throw new InvalidOperationException("Modified signed data verified successfully.");
                            if (tampered.Signer.Length != 0 || tampered.Verifier.Length != 0)
                                throw new InvalidOperationException("Invalid signature returned trusted signer/verifier.");
                        }
                    }
                    finally
                    {
                        using var cleanup = database.GetDocumentByUNID(unid);
                        cleanup?.Remove(true);
                    }
                }
                finally { NativeLibrary.Free(library); }
            });
    }
}
