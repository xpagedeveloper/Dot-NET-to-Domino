using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    private const ushort RichTextTypeMimePart = 0x0019;
    // FONTIDFIELDS: Swiss face, plain attributes, black, 10 points.
    private const uint RichTextDefaultFontId = 0x0A000001;

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
                compound, 0xFFFFFFFFu, RichTextDefaultFontId, text.Pointer, checked((uint)text.Length), delimiter.Pointer, 0x00000002u, 0), "CompoundTextAddTextExt");
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

    internal void AppendRichTextItem(nint destinationNote, string destinationName, nint sourceNote, string sourceName)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var destination = ToLmbcs(destinationName);
        using var source = ToLmbcs(sourceName);
        CheckStatus(Resolve<RichText_CompoundTextCreate>("CompoundTextCreate")(unchecked((uint)destinationNote), destination.Pointer, out var compound), "CompoundTextCreate(append item)");
        var closed = false;
        try
        {
            CheckStatus(Resolve<RichText_CompoundTextAssimilateItem>("CompoundTextAssimilateItem")(
                compound, sourceNote, source.Pointer, 0), "CompoundTextAssimilateItem");
            CheckStatus(Resolve<RichText_CompoundTextClose>("CompoundTextClose")(compound, 0, 0, 0, 0), "CompoundTextClose(append item)");
            closed = true;
        }
        finally { if (!closed) try { Resolve<RichText_CompoundTextDiscard>("CompoundTextDiscard")(compound); } catch { } }
    }

    internal void AppendRichTextDocLink(nint note, string name, nint targetDatabase, string? targetUnid, string comment, string? viewUnid = null)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var itemName = ToLmbcs(name);
        using var commentText = ToLmbcs(comment ?? string.Empty);
        var target = targetUnid is null ? default : ParseUnid(targetUnid);
        var view = viewUnid is null ? default : ParseUnid(viewUnid);
        var replica = GetDatabaseReplicaTimeDate(targetDatabase);
        CheckStatus(Resolve<RichText_CompoundTextCreate>("CompoundTextCreate")(unchecked((uint)note), itemName.Pointer, out var compound), "CompoundTextCreate(doclink)");
        var closed = false;
        try
        {
            CheckStatus(Resolve<RichText_CompoundTextAddDocLink>("CompoundTextAddDocLink")(compound, replica, view, target, commentText.Pointer, 0), "CompoundTextAddDocLink");
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
            CheckStatus(Resolve<RichText_CompoundTextAddParagraphExt>("CompoundTextAddParagraphExt")(compound, 0xFFFFFFFFu, RichTextDefaultFontId, 0, 0, 0), "CompoundTextAddParagraphExt(table-before)");
            AppendRichTextCdRecords(compound, new[] { BuildTableBegin() });
            for (var row = 0; row < rows; row++)
                for (var column = 0; column < columns; column++)
                {
                    AppendRichTextCdRecords(compound, new[] { BuildTableCell(row, column) });
                    CheckStatus(Resolve<RichText_CompoundTextAddParagraphExt>("CompoundTextAddParagraphExt")(
                        compound, 0xFFFFFFFFu, RichTextDefaultFontId, 0, 0, 0), "CompoundTextAddParagraphExt(table-cell)");
                }
            AppendRichTextCdRecords(compound, new[] { BuildTableEnd() });
            CheckStatus(Resolve<RichText_CompoundTextAddParagraphExt>("CompoundTextAddParagraphExt")(compound, 0xFFFFFFFFu, RichTextDefaultFontId, 0, 0, 0), "CompoundTextAddParagraphExt(table-after)");
            CheckStatus(Resolve<RichText_CompoundTextClose>("CompoundTextClose")(compound, 0, 0, 0, 0), "CompoundTextClose(table)");
            closed = true;
        }
        finally { if (!closed) try { Resolve<RichText_CompoundTextDiscard>("CompoundTextDiscard")(compound); } catch { } }
    }

    internal void AppendRichTextSection(nint note, string name, string title, bool expanded)
    {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(title);
        using var thread = EnterThread();
        using var itemName = ToLmbcs(name);
        using var titleText = ToLmbcs(title + "\0");
        CheckStatus(Resolve<RichText_CompoundTextCreate>("CompoundTextCreate")(unchecked((uint)note), itemName.Pointer, out var compound), "CompoundTextCreate(section)");
        var closed = false;
        try
        {
            CheckStatus(Resolve<RichText_CompoundTextAddParagraphExt>("CompoundTextAddParagraphExt")(compound, 0xFFFFFFFFu, RichTextDefaultFontId, 0, 0, 0), "CompoundTextAddParagraphExt(section-before)");
            var bar = new byte[12 + titleText.Length];
            bar[0] = 172; bar[1] = 0xFF; PutU16(bar, 2, checked((ushort)bar.Length));
            if (expanded) PutU32(bar, 4, 0x00000002u);
            Marshal.Copy(titleText.Pointer, bar, 12, titleText.Length);
            AppendRichTextCdRecords(compound, new[] { bar });
            CheckStatus(Resolve<RichText_CompoundTextAddParagraphExt>("CompoundTextAddParagraphExt")(compound, 0xFFFFFFFFu, RichTextDefaultFontId, 0, 0, 0), "CompoundTextAddParagraphExt(section-content)");
            AppendRichTextCdRecords(compound, new[] { BuildSectionEnd() });
            CheckStatus(Resolve<RichText_CompoundTextAddParagraphExt>("CompoundTextAddParagraphExt")(compound, 0xFFFFFFFFu, RichTextDefaultFontId, 0, 0, 0), "CompoundTextAddParagraphExt(section-after)");
            CheckStatus(Resolve<RichText_CompoundTextClose>("CompoundTextClose")(compound, 0, 0, 0, 0), "CompoundTextClose(section)");
            closed = true;
        }
        finally { if (!closed) try { Resolve<RichText_CompoundTextDiscard>("CompoundTextDiscard")(compound); } catch { } }
    }

    internal void BeginRichTextSection(nint note, string name, string title, bool expanded)
        => AppendRichTextSectionBoundary(note, name, title, expanded, begin: true);

    internal void EndRichTextSection(nint note, string name)
        => AppendRichTextSectionBoundary(note, name, string.Empty, expanded: false, begin: false);

    private void AppendRichTextSectionBoundary(nint note, string name, string title, bool expanded, bool begin)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var itemName = ToLmbcs(name);
        using var titleText = ToLmbcs(title + "\0");
        CheckStatus(Resolve<RichText_CompoundTextCreate>("CompoundTextCreate")(unchecked((uint)note), itemName.Pointer, out var compound), "CompoundTextCreate(section boundary)");
        var closed = false;
        try
        {
            if (begin)
            {
                CheckStatus(Resolve<RichText_CompoundTextAddParagraphExt>("CompoundTextAddParagraphExt")(compound, 0xFFFFFFFFu, RichTextDefaultFontId, 0, 0, 0), "CompoundTextAddParagraphExt(section-before)");
                var bar = new byte[12 + titleText.Length];
                bar[0] = 172; bar[1] = 0xFF; PutU16(bar, 2, checked((ushort)bar.Length));
                if (expanded) PutU32(bar, 4, 0x00000002u);
                Marshal.Copy(titleText.Pointer, bar, 12, titleText.Length);
                AppendRichTextCdRecords(compound, new[] { bar });
                CheckStatus(Resolve<RichText_CompoundTextAddParagraphExt>("CompoundTextAddParagraphExt")(compound, 0xFFFFFFFFu, RichTextDefaultFontId, 0, 0, 0), "CompoundTextAddParagraphExt(section-content)");
            }
            else
            {
                AppendRichTextCdRecords(compound, new[] { BuildSectionEnd() });
                CheckStatus(Resolve<RichText_CompoundTextAddParagraphExt>("CompoundTextAddParagraphExt")(compound, 0xFFFFFFFFu, RichTextDefaultFontId, 0, 0, 0), "CompoundTextAddParagraphExt(section-after)");
            }
            CheckStatus(Resolve<RichText_CompoundTextClose>("CompoundTextClose")(compound, 0, 0, 0, 0), "CompoundTextClose(section boundary)");
            closed = true;
        }
        finally { if (!closed) try { Resolve<RichText_CompoundTextDiscard>("CompoundTextDiscard")(compound); } catch { } }
    }

    private void AppendRichTextCdRecords(uint compound, IReadOnlyList<byte[]> records)
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
            CheckStatus(Resolve<Mutation_CompoundTextAddCDRecords>("CompoundTextAddCDRecords")(compound, buffer, checked((uint)total)), "CompoundTextAddCDRecords(rich text)");
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }


    private static byte[] BuildTableBegin()
    {
        var data = new byte[14]; data[0] = 163; data[1] = 14;
        PutU16(data, 4, 90); PutU16(data, 6, 90); PutU16(data, 8, 90); PutU16(data, 10, 90);
        return data;
    }
    private static byte[] BuildTableCell(int row, int column)
    {
        var data = new byte[18]; data[0] = 164; data[1] = 18;
        data[2] = checked((byte)row); data[3] = checked((byte)column);
        PutU16(data, 4, (ushort)(120 + column * 480)); PutU16(data, 6, (ushort)(420 + column * 480));
        data[11] = 0x02; data[14] = 1; data[15] = 1;
        return data;
    }
    private static byte[] BuildTableEnd() { var data = new byte[6]; data[0] = 165; data[1] = 6; return data; }
    private static byte[] BuildSectionEnd() { var data = new byte[4]; data[0] = 174; data[1] = 0xFF; PutU16(data, 2, 4); return data; }

    private static void PutU16(byte[] data, int offset, ushort value) { data[offset] = (byte)value; data[offset + 1] = (byte)(value >> 8); }
    private static void PutU32(byte[] data, int offset, uint value) { data[offset] = (byte)value; data[offset + 1] = (byte)(value >> 8); data[offset + 2] = (byte)(value >> 16); data[offset + 3] = (byte)(value >> 24); }

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
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort RichText_CompoundTextAssimilateItem(uint compound, nint note, nint itemName, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort RichText_CompoundTextAddParagraphExt(uint compound, uint styleId, uint fontId, nint text, uint textLength, nint nlsInfo);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort RichText_MIMEConvertMIMEPartsCC(uint note, int canonical, nint conversionControls);
}
