using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    private const ushort RichTextTypeMimePart = 0x0019;

    internal void CreateRichTextItem(nint note, string name)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var itemName = ToLmbcs(name);
        CheckStatus(Resolve<RichText_CompoundTextCreate>("CompoundTextCreate")(unchecked((uint)note), itemName.Pointer, out var compound), "CompoundTextCreate");
        var closeStatus = Resolve<RichText_CompoundTextClose>("CompoundTextClose")(compound, 0, 0, 0, 0);
        if (closeStatus != 0)
        {
            try { Resolve<RichText_CompoundTextDiscard>("CompoundTextDiscard")(compound); } catch { }
            CheckStatus(closeStatus, "CompoundTextClose");
        }
    }

    internal void AppendRichText(nint note, string name, string value)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var itemName = ToLmbcs(name);
        CheckStatus(Resolve<RichText_CompoundTextCreate>("CompoundTextCreate")(unchecked((uint)note), itemName.Pointer, out var compound), "CompoundTextCreate");
        var closed = false;
        try
        {
            using var text = ToLmbcs(value ?? string.Empty);
            using var delimiter = ToLmbcs("\r\n");
            CheckStatus(Resolve<RichText_CompoundTextAddTextExt>("CompoundTextAddTextExt")(
                compound, 0xFFFFFFFFu, 0, text.Pointer, checked((uint)text.Length), delimiter.Pointer, 0x00000002u, 0), "CompoundTextAddTextExt");
            CheckStatus(Resolve<RichText_CompoundTextClose>("CompoundTextClose")(compound, 0, 0, 0, 0), "CompoundTextClose");
            closed = true;
        }
        finally
        {
            if (!closed)
            {
                try { Resolve<RichText_CompoundTextDiscard>("CompoundTextDiscard")(compound); } catch { }
            }
        }
    }

    internal void AppendRichTextDocLink(nint note, string name, nint targetDatabase, string targetUnid, string comment)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var itemName = ToLmbcs(name);
        using var commentText = ToLmbcs(comment ?? string.Empty);
        var target = ParseUnid(targetUnid);
        var replica = GetDatabaseReplicaTimeDate(targetDatabase);
        CheckStatus(Resolve<RichText_CompoundTextCreate>("CompoundTextCreate")(unchecked((uint)note), itemName.Pointer, out var compound), "CompoundTextCreate(doclink)");
        var closed = false;
        try
        {
            CheckStatus(Resolve<RichText_CompoundTextAddDocLink>("CompoundTextAddDocLink")(compound, replica, default, target, commentText.Pointer, 0), "CompoundTextAddDocLink");
            CheckStatus(Resolve<RichText_CompoundTextClose>("CompoundTextClose")(compound, 0, 0, 0, 0), "CompoundTextClose(doclink)");
            closed = true;
        }
        finally { if (!closed) try { Resolve<RichText_CompoundTextDiscard>("CompoundTextDiscard")(compound); } catch { } }
    }

    internal void AppendRichTextTable(nint note, string name, int rows, int columns)
    {
        EnsureInitialized();
        if (rows is < 1 or > 255) throw new ArgumentOutOfRangeException(nameof(rows));
        if (columns is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(columns));
        using var thread = EnterThread();
        using var itemName = ToLmbcs(name);
        CheckStatus(Resolve<RichText_CompoundTextCreate>("CompoundTextCreate")(unchecked((uint)note), itemName.Pointer, out var compound), "CompoundTextCreate(table)");
        var closed = false;
        try
        {
            // Tables must be surrounded by paragraphs in a composite item.
            CheckStatus(Resolve<RichText_CompoundTextAddParagraphExt>("CompoundTextAddParagraphExt")(compound, 0xFFFFFFFFu, 0, 0, 0, 0), "CompoundTextAddParagraphExt(table-before)");
            AppendTableRecords(compound, new[] { BuildTableBegin() });
            for (var row = 0; row < rows; row++)
                for (var column = 0; column < columns; column++)
                {
                    AppendTableRecords(compound, new[] { BuildTableCell(row, column) });
                    CheckStatus(Resolve<RichText_CompoundTextAddParagraphExt>("CompoundTextAddParagraphExt")(
                        compound, 0xFFFFFFFFu, 0, 0, 0, 0), "CompoundTextAddParagraphExt(table-cell)");
                }
            AppendTableRecords(compound, new[] { BuildTableEnd() });
            CheckStatus(Resolve<RichText_CompoundTextAddParagraphExt>("CompoundTextAddParagraphExt")(compound, 0xFFFFFFFFu, 0, 0, 0, 0), "CompoundTextAddParagraphExt(table-after)");
            CheckStatus(Resolve<RichText_CompoundTextClose>("CompoundTextClose")(compound, 0, 0, 0, 0), "CompoundTextClose(table)");
            closed = true;
        }
        finally { if (!closed) try { Resolve<RichText_CompoundTextDiscard>("CompoundTextDiscard")(compound); } catch { } }
    }

    private void AppendTableRecords(uint compound, IReadOnlyList<byte[]> records)
    {
        var total = records.Sum(r => r.Length + (r.Length & 1));
        var buffer = Marshal.AllocHGlobal(total);
        try
        {
            var offset = 0;
            foreach (var record in records)
            {
                Marshal.Copy(record, 0, buffer + offset, record.Length);
                offset += record.Length;
                if ((record.Length & 1) != 0) Marshal.WriteByte(buffer, offset++, 0);
            }
            CheckStatus(Resolve<Mutation_CompoundTextAddCDRecords>("CompoundTextAddCDRecords")(compound, buffer, checked((uint)total)), "CompoundTextAddCDRecords(table)");
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }


    private static byte[] BuildTableBegin() { var data = new byte[16]; PutU16(data, 0, 163); PutU16(data, 2, 16); return data; }
    private static byte[] BuildTableCell(int row, int column)
    {
        var data = new byte[20]; PutU16(data, 0, 164); PutU16(data, 2, 20); data[4] = checked((byte)row); data[5] = checked((byte)column); data[15] = 1; data[16] = 1; PutU16(data, 18, 0); return data;
    }
    private static byte[] BuildTableEnd() { var data = new byte[8]; PutU16(data, 0, 165); PutU16(data, 2, 8); return data; }

    private static void PutU16(byte[] data, int offset, ushort value) { data[offset] = (byte)value; data[offset + 1] = (byte)(value >> 8); }

    internal void ConvertMimePartsToComposite(nint note)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        foreach (var name in GetAllItemNames(note))
        {
            if (!HasItem(note, name)) continue;
            if (GetItemMetadata(note, name).DataType != RichTextTypeMimePart) continue;
            CheckStatus(Resolve<RichText_MIMEConvertMIMEPartsCC>("MIMEConvertMIMEPartsCC")(unchecked((uint)note), 0, 0), "MIMEConvertMIMEPartsCC");
            return;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort RichText_CompoundTextCreate(uint note, nint itemName, out uint compound);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort RichText_CompoundTextClose(uint compound, nint returnBuffer, nint returnBufferSize, nint returnFile, ushort returnFileNameSize);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void RichText_CompoundTextDiscard(uint compound);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort RichText_CompoundTextAddTextExt(uint compound, uint styleId, uint fontId, nint text, uint textLength, nint lineDelimiter, uint flags, nint nlsInfo);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort RichText_CompoundTextAddDocLink(uint compound, NotesTimeDate replica, NotesUnid view, NotesUnid note, nint comment, uint reserved);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort RichText_CompoundTextAddParagraphExt(uint compound, uint styleId, uint fontId, nint text, uint textLength, nint nlsInfo);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort RichText_MIMEConvertMIMEPartsCC(uint note, int canonical, nint conversionControls);
}
