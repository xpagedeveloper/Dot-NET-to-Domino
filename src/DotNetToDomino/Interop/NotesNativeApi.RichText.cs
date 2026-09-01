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
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort RichText_MIMEConvertMIMEPartsCC(uint note, int canonical, nint conversionControls);
}
