using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed record NotesViewColumnData(
    int Position, string ItemName, string Title, string Formula, ushort Flags1, ushort DisplayWidth,
    byte FontFace, byte FontAttributes, byte FontColor, byte FontPointSize,
    ushort Flags2, byte NumberDigits, byte NumberFormat, byte NumberAttributes,
    byte DateFormat, byte TimeFormat, byte TimeZoneFormat, byte TimeDateFormat,
    ushort FormatDataType, ushort ListSep);

internal sealed partial class NotesNativeApi
{
    internal string? GetDesignNoteText(nint database, uint noteId, string itemName)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var status = Resolve<Delta_NSFNoteOpen>("NSFNoteOpen")(database, noteId, 0, out var note);
        if ((status & 0x3FFF) == 0x0227) return null;
        CheckStatus(status, "NSFNoteOpen(design note)");
        try { return GetItemText(note, itemName); }
        finally { CloseNote(note); }
    }

    internal NotesViewColumnData[] GetViewColumns(nint database, string viewName)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
        using var name = ToLmbcs(viewName.Trim());
        CheckStatus(Resolve<Delta_NIFFindDesignNote>("NIFFindDesignNote")(database, name.Pointer, 0x0008, out var viewNoteId), "NIFFindDesignNote(view)");
        var note = TryOpenNote(database, viewNoteId);
        if (note == 0) return [];
        try
        {
            var info = GetItemMetadata(note, "$VIEWFORMAT");
            if (info.DataType != 0x0005) throw new NotesException("NotesView $VIEWFORMAT has an unexpected data type.");
            return ParseViewColumns(CopyItemValueWithoutType(info));
        }
        finally { CloseNote(note); }
    }

    internal void AppendRichTextStyled(nint note, string name, string value, NotesRichTextStyleState style)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var itemName = ToLmbcs(name);
        CheckStatus(Resolve<RichText_CompoundTextCreate>("CompoundTextCreate")(unchecked((uint)note), itemName.Pointer, out var compound), "CompoundTextCreate");
        var closed = false;
        try
        {
            using var text = ToLmbcs(value);
            using var delimiter = ToLmbcs("\r\n");
            const uint styleSameAsPrevious = 0xFFFFFFFFu;
            const uint preserveLines = 0x00000002u;
            var fontId = BuildRichTextFontId(style);
            CheckStatus(Resolve<RichText_CompoundTextAddTextExt>("CompoundTextAddTextExt")(
                compound, styleSameAsPrevious, fontId, text.Pointer, checked((uint)text.Length), delimiter.Pointer, preserveLines, 0),
                "CompoundTextAddTextExt(styled)");
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

    internal string DecodeRichTextText(byte[] data, int offset, int length)
    {
        EnsureInitialized();
        if (length <= 0) return string.Empty;
        if (offset < 0 || offset > data.Length || length > data.Length - offset) throw new ArgumentOutOfRangeException(nameof(offset));
        var pointer = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.Copy(data, offset, pointer, length);
            return FromLmbcs(pointer, length);
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    internal string DecompileRichTextFormula(byte[] data, int offset, int length) => DecompileFormula(data, offset, length, "rich text");

    internal string FormatRichTextTimeDate(byte[] data, int offset)
    {
        if (offset < 0 || offset + 8 > data.Length) return string.Empty;
        var first = ReadUInt32(data, offset);
        var second = ReadUInt32(data, offset + 4);
        return second.ToString("X8", System.Globalization.CultureInfo.InvariantCulture) + first.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
    }

    internal string FormatRichTextUnid(byte[] data, int offset)
    {
        if (offset < 0 || offset + 16 > data.Length) return string.Empty;
        return FormatRichTextTimeDate(data, offset) + FormatRichTextTimeDate(data, offset + 8);
    }

    private NotesViewColumnData[] ParseViewColumns(byte[] data)
    {
        const int tableFormatSize = 10;
        const int columnFormatSize = 32;
        const ushort columnSignature = 17238;
        if (data.Length < tableFormatSize) throw new NotesException("Invalid Notes view format data.");
        var count = ReadUInt16(data, 2);
        if (count == 0) return [];
        var descriptorBytes = checked(count * columnFormatSize);
        if (data.Length < tableFormatSize + descriptorBytes) throw new NotesException("Truncated Notes view column format data.");

        var descriptors = new (ushort Flags1, ushort Item, ushort Title, ushort Formula, ushort Constant, ushort Width,
            byte Face, byte Attr, byte Color, byte Point, ushort Flags2, byte Digits, byte NumFmt, byte NumAttr,
            byte DateFmt, byte TimeFmt, byte ZoneFmt, byte TimeDateFmt, ushort DataType, ushort ListSep)[count];
        for (var i = 0; i < count; i++)
        {
            var offset = tableFormatSize + i * columnFormatSize;
            if (ReadUInt16(data, offset) != columnSignature) throw new NotesException("Invalid Notes view column format signature.");
            descriptors[i] = (
                ReadUInt16(data, offset + 2), ReadUInt16(data, offset + 4), ReadUInt16(data, offset + 6), ReadUInt16(data, offset + 8),
                ReadUInt16(data, offset + 10), ReadUInt16(data, offset + 12), data[offset + 14], data[offset + 15], data[offset + 16], data[offset + 17],
                ReadUInt16(data, offset + 18), data[offset + 20], data[offset + 21], data[offset + 22], data[offset + 24], data[offset + 25],
                data[offset + 26], data[offset + 27], ReadUInt16(data, offset + 28), ReadUInt16(data, offset + 30));
        }

        var cursor = tableFormatSize + descriptorBytes;
        var result = new NotesViewColumnData[count];
        for (var i = 0; i < count; i++)
        {
            var d = descriptors[i];
            var packedSize = checked((int)d.Item + d.Title + d.Formula + d.Constant);
            if (cursor + packedSize > data.Length) throw new NotesException("Truncated Notes view column data.");
            var itemName = DecodeRichTextText(data, cursor, d.Item); cursor += d.Item;
            var title = DecodeRichTextText(data, cursor, d.Title); cursor += d.Title;
            var formula = d.Formula == 0 ? string.Empty : DecompileFormula(data, cursor, d.Formula, "view column"); cursor += d.Formula;
            cursor += d.Constant;
            result[i] = new NotesViewColumnData(i + 1, itemName, title, formula, d.Flags1, d.Width, d.Face, d.Attr, d.Color, d.Point,
                d.Flags2, d.Digits, d.NumFmt, d.NumAttr, d.DateFmt, d.TimeFmt, d.ZoneFmt, d.TimeDateFmt, d.DataType, d.ListSep);
        }
        return result;
    }

    private string DecompileFormula(byte[] data, int offset, int length, string context)
    {
        if (length <= 0) return string.Empty;
        var formula = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.Copy(data, offset, formula, length);
            CheckStatus(Resolve<Delta_NSFFormulaDecompile>("NSFFormulaDecompile")(formula, 0, out var textHandle, out var textLength), $"NSFFormulaDecompile({context})");
            if (textHandle == 0 || textLength == 0)
            {
                if (textHandle != 0) Resolve<Delta_OSMemFree>("OSMemFree")(textHandle);
                return string.Empty;
            }
            try
            {
                var text = Resolve<Delta_OSLockObject>("OSLockObject")(textHandle);
                if (text == 0) throw new NotesException("Unable to lock decompiled formula text.");
                try { return FromLmbcs(text, textLength); }
                finally { Resolve<Delta_OSUnlockObject>("OSUnlockObject")(textHandle); }
            }
            finally { Resolve<Delta_OSMemFree>("OSMemFree")(textHandle); }
        }
        finally { Marshal.FreeHGlobal(formula); }
    }

    private static uint BuildRichTextFontId(NotesRichTextStyleState style)
    {
        const int noChange = 255;
        var face = style.NotesFont == noChange ? (int)(RichTextDefaultFontId & 0xff) : Math.Clamp(style.NotesFont, 0, 255);
        var color = style.NotesColor == noChange ? 0 : Math.Clamp(style.NotesColor, 0, 255);
        var point = style.FontSize == noChange ? 10 : Math.Clamp(style.FontSize, 1, 250);
        var attributes = 0;
        if (style.Bold != noChange && style.Bold != 0) attributes |= 0x01;
        if (style.Italic != noChange && style.Italic != 0) attributes |= 0x02;
        if (style.Underline != noChange && style.Underline != 0) attributes |= 0x04;
        if (style.Strikethrough != noChange && style.Strikethrough != 0) attributes |= 0x08;
        if (style.Effects == 1) attributes |= 0x10;
        else if (style.Effects == 2) attributes |= 0x20;
        else if (style.Effects == 3) attributes |= 0x80;
        else if (style.Effects == 4) attributes |= 0x90;
        else if (style.Effects == 5) attributes |= 0xA0;
        return (uint)(face | (attributes << 8) | (color << 16) | (point << 24));
    }

    private static ushort ReadUInt16(byte[] data, int offset) => (ushort)(data[offset] | (data[offset + 1] << 8));
    private static uint ReadUInt32(byte[] data, int offset) => (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Delta_NSFNoteOpen(nint database, uint noteId, ushort flags, out nint note);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Delta_NIFFindDesignNote(nint database, nint name, ushort noteClass, out uint noteId);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Delta_NSFFormulaDecompile(nint formula, int selectionFormula, out nint formulaText, out ushort formulaTextLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Delta_OSLockObject(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Delta_OSUnlockObject(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Delta_OSMemFree(nint handle);
}
