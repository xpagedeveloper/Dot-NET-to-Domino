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
        const uint openRead = 0x00000001u, includeHeaders = 0x00000004u;
        CheckStatus(Resolve<MIMEStreamOpenDelegate>("MIMEStreamOpen")(note, name.Pointer, checked((ushort)name.Length), openRead | includeHeaders, out var stream), "MIMEStreamOpen(read)");
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

    internal void WriteMimeStream(nint note, string itemName, byte[] data)
    {
        EnsureInitialized(); using var thread = EnterThread(); ArgumentException.ThrowIfNullOrWhiteSpace(itemName); ArgumentNullException.ThrowIfNull(data);
        const uint openWrite = 0x00000002u;
        CheckStatus(Resolve<MIMEStreamOpenDelegate>("MIMEStreamOpen")(note, 0, 0, openWrite, out var stream), "MIMEStreamOpen(write)");
        try
        {
            var buffer = Marshal.AllocHGlobal(Math.Max(1, data.Length));
            try
            {
                if (data.Length != 0) Marshal.Copy(data, 0, buffer, data.Length);
                if (Resolve<MIMEStreamWriteDelegate>("MIMEStreamWrite")(buffer, checked((uint)data.Length), stream) != 0) throw new NotesException("MIMEStreamWrite failed.");
                using var name = ToLmbcs(itemName);
                CheckStatus(Resolve<MIMEStreamItemizeDelegate>("MIMEStreamItemize")(note, name.Pointer, checked((ushort)name.Length), 0, stream), "MIMEStreamItemize");
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        finally { Resolve<MIMEStreamCloseDelegate>("MIMEStreamClose")(stream); }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Mime_NSFItemAppendDelegate(nint note, ushort itemFlags, nint itemName, ushort itemNameLength, ushort itemType, nint itemValue, uint itemValueLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort MIMEStreamOpenDelegate(nint note, nint itemName, ushort itemNameLength, uint flags, out nint stream);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int MIMEStreamReadDelegate(nint data, out uint dataLength, uint maxDataLength, nint stream);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int MIMEStreamWriteDelegate(nint data, uint dataLength, nint stream);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort MIMEStreamItemizeDelegate(nint note, nint itemName, ushort itemNameLength, uint flags, nint stream);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void MIMEStreamCloseDelegate(nint stream);
}
