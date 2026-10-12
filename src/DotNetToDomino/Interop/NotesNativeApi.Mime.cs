using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    private const ushort MimeNotesTypeMimePart = 25;

    internal void CreateEmptyMimePart(nint note, string itemName)
    {
        EnsureInitialized(); using var thread = EnterThread(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        const ushort mimePartVersion = 2;
        const byte mimePartBody = 2;
        const int mimePartSize = 20;
        var mimePart = new byte[mimePartSize];
        mimePart[0] = (byte)mimePartVersion;
        mimePart[1] = (byte)(mimePartVersion >> 8);
        mimePart[6] = mimePartBody;
        using var name = ToLmbcs(itemName);
        var value = Marshal.AllocHGlobal(mimePart.Length);
        try
        {
            Marshal.Copy(mimePart, 0, value, mimePart.Length);
            CheckStatus(Resolve<Mime_NSFItemAppendDelegate>("NSFItemAppend")(
                note, 0, name.Pointer, checked((ushort)name.Length), MimeNotesTypeMimePart,
                value, checked((uint)mimePart.Length)), "NSFItemAppend(empty MIME entity)");
        }
        finally { Marshal.FreeHGlobal(value); }
    }

    internal bool IsMimeItem(nint note, string itemName)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);
        if (!HasItem(note, itemName)) return false;
        return GetItemMetadata(note, itemName).DataType == MimeNotesTypeMimePart;
    }

    internal byte[] ReadMimeStream(nint note, string itemName)
    {
        EnsureInitialized(); using var thread = EnterThread(); using var name = ToLmbcs(itemName);
        const uint openRead = 0x00000001u, includeHeaders = 0x00000010u;
        var status = Resolve<MIMEStreamOpenDelegate>("MIMEStreamOpen")(note, name.Pointer, checked((ushort)name.Length), openRead | includeHeaders, out var stream);
        if ((status & ErrMask) == ErrMimeNoData) return [];
        CheckStatus(status, "MIMEStreamOpen(read)");
        try
        {
            using var output = new MemoryStream(); const int capacity = 65536; var buffer = Marshal.AllocHGlobal(capacity);
            try
            {
                while (true)
                {
                    var rc = Resolve<MIMEStreamReadDelegate>("MIMEStreamRead")(buffer, out var read, capacity, stream);
                    if (rc == 2) throw new NotesException("MIMEStreamRead failed.");
                    if (read != 0) { var bytes = new byte[read]; Marshal.Copy(buffer, bytes, 0, checked((int)read)); output.Write(bytes); }
                    if (rc == 1) break;
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }
            return output.ToArray();
        }
        finally { Resolve<MIMEStreamCloseDelegate>("MIMEStreamClose")(stream); }
    }

    internal void WriteMimeStream(nint db, nint note, string itemName, byte[] data)
    {
        EnsureInitialized(); using var thread = EnterThread(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); ArgumentNullException.ThrowIfNull(data);
        const uint openWrite = 0x00000002u;
        var temporaryNote = CreateNote(db);
        try
        {
            using var name = ToLmbcs(itemName);
            CheckStatus(Resolve<MIMEStreamOpenDelegate>("MIMEStreamOpen")(temporaryNote, name.Pointer, checked((ushort)name.Length), openWrite, out var stream), "MIMEStreamOpen(write temp)");
            try
            {
                for (var offset = 0; offset < data.Length;)
                {
                    var count = Math.Min(60000, data.Length - offset);
                    var buffer = Marshal.AllocHGlobal(count);
                    try
                    {
                        Marshal.Copy(data, offset, buffer, count);
                        if (Resolve<MIMEStreamWriteDelegate>("MIMEStreamWrite")(buffer, checked((uint)count), stream) == 2) throw new NotesException("MIMEStreamWrite failed.");
                    }
                    finally { Marshal.FreeHGlobal(buffer); }
                    offset += count;
                }
                CheckStatus(Resolve<MIMEStreamItemizeDelegate>("MIMEStreamItemize")(temporaryNote, name.Pointer, checked((ushort)name.Length), 0x00000006, stream), "MIMEStreamItemize(full temp)");
            }
            finally { Resolve<MIMEStreamCloseDelegate>("MIMEStreamClose")(stream); }
            while (HasItem(note, itemName)) DeleteItem(note, itemName);
            CopyMimeItemsByName(temporaryNote, note, itemName);
            CopyMimeItemsByName(temporaryNote, note, "$FILE");
        }
        finally { CloseNote(temporaryNote); }
    }

    private void CopyMimeItemsByName(nint source, nint destination, string itemName)
    {
        using var name = ToLmbcs(itemName);
        var status = Resolve<NSFItemInfoDelegate>("NSFItemInfo")(source, name.Pointer, checked((ushort)name.Length), out var block, out _, out _, out _);
        if ((status & ErrMask) == ErrItemNotFound) return;
        CheckStatus(status, "NSFItemInfo(MIME copy)");
        while (true)
        {
            CheckStatus(Resolve<Mime_NSFItemCopy>("NSFItemCopy")(checked((uint)destination), block), "NSFItemCopy(MIME)");
            status = Resolve<Attachment_NSFItemInfoNext>("NSFItemInfoNext")(source, block, name.Pointer, checked((ushort)name.Length), out var next, out _, out _, out _);
            if ((status & ErrMask) == ErrItemNotFound) return;
            CheckStatus(status, "NSFItemInfoNext(MIME copy)");
            block = next;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Mime_NSFItemCopy(uint destination, NotesBlockId itemBlock);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Mime_NSFItemAppendDelegate(nint note, ushort itemFlags, nint itemName, ushort itemNameLength, ushort itemType, nint itemValue, uint itemValueLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort MIMEStreamOpenDelegate(nint note, nint itemName, ushort itemNameLength, uint flags, out nint stream);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int MIMEStreamReadDelegate(nint data, out uint dataLength, uint maxDataLength, nint stream);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int MIMEStreamWriteDelegate(nint data, uint dataLength, nint stream);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort MIMEStreamItemizeDelegate(nint note, nint itemName, ushort itemNameLength, uint flags, nint stream);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void MIMEStreamCloseDelegate(nint stream);
}
