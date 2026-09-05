using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    internal string ConvertItemToText(nint note, string name, bool xpscriptParity = true)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        return ConvertItemToText(note, name);
    }

    internal void RemoveItemByBlock(nint note, string name)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var info = GetItemMetadata(note, name);
        CheckStatus(Resolve<ItemParity_NSFItemDeleteByBlockId>("NSFItemDeleteByBLOCKID")(note, info.ItemBlock), "NSFItemDeleteByBLOCKID");
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort ItemParity_NSFItemDeleteByBlockId(nint note, NotesBlockId itemBlock);
}
