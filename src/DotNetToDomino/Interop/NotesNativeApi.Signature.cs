using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    internal void SignNote(nint note)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus(Resolve<Signature_NSFNoteSign>("NSFNoteSign")(note), "NSFNoteSign");
    }

    internal (string Signer, string Verifier) GetNoteSignatureInfo(nint note)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        if (!HasItem(note, "$Signature")) return (string.Empty, string.Empty);
        const int capacity = 2048;
        var signer = Marshal.AllocHGlobal(capacity);
        var certifier = Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(signer, capacity);
            Zero(certifier, capacity);
            var status = Resolve<Signature_NSFNoteVerifySignature>("NSFNoteVerifySignature")(note, 0, 0, signer, certifier);
            if (status != 0) return (string.Empty, string.Empty);
            return (FromLmbcsZeroTerminated(signer, capacity - 1), FromLmbcsZeroTerminated(certifier, capacity - 1));
        }
        finally { Marshal.FreeHGlobal(certifier); Marshal.FreeHGlobal(signer); }
    }

    internal bool IsNoteEncrypted(nint note)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus((ushort)Resolve<Signature_NSFNoteIsSignedOrSealed>("NSFNoteIsSignedOrSealed")(note, out _, out var sealedNote), "NSFNoteIsSignedOrSealed");
        return sealedNote != 0;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Signature_NSFNoteSign(nint note);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Signature_NSFNoteVerifySignature(nint note, nint name, nint when, nint signer, nint certifier);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Signature_NSFNoteIsSignedOrSealed(nint note, out byte signed, out byte sealedNote);
}
