using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed class NotesRichTextRecordData
{
    internal NotesRichTextRecordData(int segmentIndex, int recordIndex, ushort signature, byte[] data, string text, int elementType, string linkedObjectName)
    {
        SegmentIndex = segmentIndex;
        RecordIndex = recordIndex;
        Signature = signature;
        Data = data;
        Text = text;
        ElementType = elementType;
        LinkedObjectName = linkedObjectName;
    }

    internal int SegmentIndex { get; }
    internal int RecordIndex { get; }
    internal ushort Signature { get; }
    internal byte[] Data { get; }
    internal string Text { get; }
    internal int ElementType { get; }
    internal string LinkedObjectName { get; }
}

internal readonly record struct NotesRichTextStyleState(
    int Bold,
    int Effects,
    int FontSize,
    int Italic,
    int NotesColor,
    int NotesFont,
    int PassThruHtml,
    int Strikethrough,
    int Underline);

internal readonly record struct NotesCompoundStyleState(
    short JustifyMode,
    short LineSpacing,
    short ParagraphSpacingBefore,
    short ParagraphSpacingAfter,
    short LeftMargin,
    short RightMargin,
    short FirstLineLeftMargin,
    short Flags,
    short[] Tabs);

[StructLayout(LayoutKind.Sequential)]
internal struct NotesCompoundStyleNative
{
    public short JustifyMode;
    public short LineSpacing;
    public short ParagraphSpacingBefore;
    public short ParagraphSpacingAfter;
    public short LeftMargin;
    public short RightMargin;
    public short FirstLineLeftMargin;
    public short Tabs;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
    public short[] Tab;
    public short Flags;
}

internal sealed partial class NotesNativeApi
{
    private const ushort RichTextSigParagraph = 129;
    private const ushort RichTextSigText = unchecked((ushort)-123);
    private const ushort RichTextSigLink2 = unchecked((ushort)-111);
    private const ushort RichTextSigLinkExport2 = unchecked((ushort)-110);
    private const ushort RichTextSigTableBegin = 163;
    private const ushort RichTextSigTableCell = 164;
    private const ushort RichTextSigTableEnd = 165;
    private const ushort RichTextSigNestedTableBegin = 207;
    private const ushort RichTextSigNestedTableCell = 208;
    private const ushort RichTextSigNestedTableEnd = 209;
    private const ushort RichTextSigBar = unchecked((ushort)-84);

    internal NotesCompoundStyleState GetDefaultRichTextParagraphStyle()
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var style = new NotesCompoundStyleNative { Tab = new short[20] };
        Resolve<RichText_CompoundTextInitStyle>("CompoundTextInitStyle")(ref style);
        var tabCount = Math.Clamp((int)unchecked((ushort)style.Tabs), 0, 20);
        var tabs = new short[tabCount];
        if (tabCount > 0) Array.Copy(style.Tab, tabs, tabCount);
        return new NotesCompoundStyleState(
            style.JustifyMode,
            style.LineSpacing,
            style.ParagraphSpacingBefore,
            style.ParagraphSpacingAfter,
            style.LeftMargin,
            style.RightMargin,
            style.FirstLineLeftMargin,
            style.Flags,
            tabs);
    }

    internal void AppendRichTextParagraphStyle(nint note, string name, NotesCompoundStyleState state)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var itemName = ToLmbcs(name);
        CheckStatus(Resolve<RichText_CompoundTextCreate>("CompoundTextCreate")(unchecked((uint)note), itemName.Pointer, out var compound), "CompoundTextCreate(paragraph style)");
        var closed = false;
        try
        {
            var native = new NotesCompoundStyleNative
            {
                JustifyMode = state.JustifyMode,
                LineSpacing = state.LineSpacing,
                ParagraphSpacingBefore = state.ParagraphSpacingBefore,
                ParagraphSpacingAfter = state.ParagraphSpacingAfter,
                LeftMargin = state.LeftMargin,
                RightMargin = state.RightMargin,
                FirstLineLeftMargin = state.FirstLineLeftMargin,
                Tabs = checked((short)Math.Min(state.Tabs.Length, 20)),
                Tab = new short[20],
                Flags = state.Flags
            };
            Array.Copy(state.Tabs, native.Tab, Math.Min(state.Tabs.Length, native.Tab.Length));
            using var styleName = ToLmbcs(string.Empty);
            CheckStatus(Resolve<RichText_CompoundTextDefineStyle>("CompoundTextDefineStyle")(
                compound, styleName.Pointer, ref native, out var styleId), "CompoundTextDefineStyle");
            CheckStatus(Resolve<RichText_CompoundTextAddParagraphExt>("CompoundTextAddParagraphExt")(
                compound, styleId, 0, 0, 0, 0), "CompoundTextAddParagraphExt(paragraph style)");
            CheckStatus(Resolve<RichText_CompoundTextClose>("CompoundTextClose")(compound, 0, 0, 0, 0), "CompoundTextClose(paragraph style)");
            closed = true;
        }
        finally
        {
            if (!closed) try { Resolve<RichText_CompoundTextDiscard>("CompoundTextDiscard")(compound); } catch { }
        }
    }

    internal IReadOnlyList<NotesRichTextRecordData> GetRichTextRecords(nint note, string itemName)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        itemName = itemName.Trim();
        if (itemName.Length == 0) return Array.Empty<NotesRichTextRecordData>();

        var result = new List<NotesRichTextRecordData>();
        using var itemNameText = ToLmbcs(itemName);
        var status = Resolve<Attachment_NSFItemInfo>("NSFItemInfo")(
            note, itemNameText.Pointer, checked((ushort)itemNameText.Length),
            out var currentItem, out var dataType, out var valueBlock, out var valueLength);
        if (status != 0) return result;

        var segmentIndex = 0;
        while (true)
        {
            if (dataType == NotesTypeComposite)
                AppendRichTextRecords(valueBlock, valueLength, segmentIndex, result);

            status = Resolve<Attachment_NSFItemInfoNext>("NSFItemInfoNext")(
                note, currentItem, itemNameText.Pointer, checked((ushort)itemNameText.Length),
                out var nextItem, out dataType, out valueBlock, out valueLength);
            if (status != 0) break;
            currentItem = nextItem;
            segmentIndex++;
        }
        return result;
    }

    private void AppendRichTextRecords(NotesBlockId valueBlock, uint valueLength, int segmentIndex, ICollection<NotesRichTextRecordData> destination)
    {
        var recordIndex = 0;
        Attachment_EnumCompositeAction callback = (record, signature, recordLength, _) =>
        {
            if (record == 0 || recordLength == 0 || recordLength > int.MaxValue) return 0;
            var length = checked((int)recordLength);
            var bytes = new byte[length];
            Marshal.Copy(record, bytes, 0, length);
            var text = ReadRichTextRecordText(record, signature, length);
            var elementType = MapRichTextElementType(record, signature, length);
            var linkedObjectName = ReadRichTextLinkedObjectName(record, elementType, length);
            destination.Add(new NotesRichTextRecordData(segmentIndex, recordIndex++, signature, bytes, text, elementType, linkedObjectName));
            return 0;
        };
        _ = Resolve<Attachment_EnumCompositeBuffer>("EnumCompositeBuffer")(valueBlock, valueLength, callback, 0);
        GC.KeepAlive(callback);
    }

    private string ReadRichTextRecordText(nint record, ushort signature, int recordLength)
    {
        if (signature != RichTextSigText || recordLength <= 8) return string.Empty;
        return FromLmbcs(nint.Add(record, 8), recordLength - 8).Replace('\0', '\n');
    }

    private string ReadRichTextLinkedObjectName(nint record, int elementType, int recordLength)
    {
        if (elementType != 8 || record == 0 || recordLength < 12) return string.Empty;
        var dataLength = unchecked((ushort)Marshal.ReadInt16(record, 10));
        if (dataLength == 0 || dataLength > recordLength - 12) return string.Empty;
        var data = nint.Add(record, 12);
        var nameLength = ZeroTerminatedLength(data, dataLength);
        return nameLength <= 0 ? string.Empty : FromLmbcs(data, nameLength);
    }

    private int MapRichTextElementType(nint record, ushort signature, int recordLength)
    {
        if (signature == RichTextSigTableBegin || signature == RichTextSigNestedTableBegin) return 1;
        if (signature == RichTextSigText) return 3;
        if (signature == RichTextSigParagraph) return 4;
        if (signature == RichTextSigLink2 || signature == RichTextSigLinkExport2) return 5;
        if (signature == RichTextSigBar) return 6;
        if (signature == RichTextSigTableCell || signature == RichTextSigNestedTableCell) return 7;
        if (signature == RichTextSigTableEnd || signature == RichTextSigNestedTableEnd) return 9;
        if (recordLength >= 12 && IsHotspotBeginSignature(signature))
        {
            var hotspotType = unchecked((ushort)Marshal.ReadInt16(record, 4));
            if (hotspotType == HotspotTypeFile) return 8;
        }
        return 0;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void RichText_CompoundTextInitStyle(ref NotesCompoundStyleNative style);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort RichText_CompoundTextDefineStyle(uint compound, nint name, ref NotesCompoundStyleNative style, out uint styleId);
}
