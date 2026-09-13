using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    private const ushort MailNoteAnyRecipient = 0x0001;
    private const ushort MailNoteNotesEncryptMime = 0x0040;
    private const ushort MailNoteMimeBody = 0x0080;
    private const ushort MailSendSign = 0x0002;
    private const ushort MailSendSeal = 0x0004;

    internal void SendNote(nint note, string[]? recipients)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus(Resolve<NSFNoteCopySendDelegate>("NSFNoteCopy")(note, out var copy), "NSFNoteCopy(send)");
        try
        {
            if (recipients is not null)
                SetMailTextList(copy, "SendTo", recipients);

            if (!HasItem(copy, "SendTo") && !HasItem(copy, "CopyTo") && !HasItem(copy, "BlindCopyTo"))
                throw new NotesException(5, "NotesDocument.Send requires SendTo, CopyTo, BlindCopyTo, or an explicit recipients argument.");

            ushort mailFlags = 0;
            _ = Resolve<NSFNoteIsSignedOrSealedSendDelegate>("NSFNoteIsSignedOrSealed")(copy, out var signed, out var sealedValue);
            if (signed != 0) mailFlags |= MailSendSign;
            if (sealedValue != 0) mailFlags |= MailSendSeal;

            ushort mailNoteFlags = MailNoteAnyRecipient;
            if (HasItem(copy, "$NoteHasNativeMIME"))
                mailNoteFlags |= MailNoteMimeBody | MailNoteNotesEncryptMime;

            CheckStatus(Resolve<MailNoteJitEx2SendDelegate>("MailNoteJitEx2")(
                0, copy, mailFlags, 0, 0, mailNoteFlags, 0, 0), "MailNoteJitEx2");
        }
        finally
        {
            CloseNote(copy);
        }
    }

    private void SetMailTextList(nint note, string itemName, IReadOnlyList<string> values)
    {
        var cleaned = values.Select(value => value?.Trim() ?? string.Empty).Where(value => value.Length != 0).ToArray();
        if (cleaned.Length == 0)
            throw new NotesException(5, "NotesDocument.Send recipients cannot be empty.");

        using var name = ToLmbcs(itemName);
        using var first = ToLmbcs(cleaned[0]);
        CheckStatus(Resolve<NSFItemCreateTextListSendDelegate>("NSFItemCreateTextList")(
            note, name.Pointer, first.Pointer, checked((ushort)Math.Min(first.Length, ushort.MaxValue))), "NSFItemCreateTextList(" + itemName + ")");

        for (var i = 1; i < cleaned.Length; i++)
        {
            using var value = ToLmbcs(cleaned[i]);
            CheckStatus(Resolve<NSFItemAppendTextListSendDelegate>("NSFItemAppendTextList")(
                note, name.Pointer, value.Pointer, checked((ushort)Math.Min(value.Length, ushort.MaxValue)), 1), "NSFItemAppendTextList(" + itemName + ")");
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort NSFNoteCopySendDelegate(nint source, out nint destination);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int NSFNoteIsSignedOrSealedSendDelegate(nint note, out byte signed, out byte sealedValue);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort NSFItemCreateTextListSendDelegate(nint note, nint itemName, nint text, ushort textLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort NSFItemAppendTextListSendDelegate(nint note, nint itemName, nint text, ushort textLength, int allowDuplicates);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort MailNoteJitEx2SendDelegate(nint runContext, nint note, ushort mailFlags, nint recipients, ushort jitFlag, ushort mailNoteFlags, nint callback, nint callbackContext);
}
