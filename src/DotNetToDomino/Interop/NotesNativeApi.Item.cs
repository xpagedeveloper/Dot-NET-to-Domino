using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    private const ushort ErrMask = 0x3FFF;
    private const ushort ErrItemNotFound = 0x0222;

    internal bool HasItem(nint note, string name)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var itemName = ToLmbcs(name);
        var nameLength = checked((ushort)Math.Min(itemName.Length, ushort.MaxValue));
        var status = Resolve<NSFItemInfoDelegate>("NSFItemInfo")(
            note,
            itemName.Pointer,
            nameLength,
            out _,
            out _,
            out _,
            out _);

        if (status == 0)
            return true;
        if ((status & ErrMask) == ErrItemNotFound)
            return false;

        CheckStatus(status, "NSFItemInfo");
        return false;
    }

    internal string GetItemText(nint note, string name)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var itemName = ToLmbcs(name);
        const int capacity = 65535;
        var output = Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(output, capacity);
            var length = Resolve<NSFItemGetTextDelegate>("NSFItemGetText")(
                note,
                itemName.Pointer,
                output,
                ushort.MaxValue);
            return length == 0 ? string.Empty : FromLmbcs(output, length).Replace('\0', '\n');
        }
        finally
        {
            Marshal.FreeHGlobal(output);
        }
    }

    internal bool TryGetItemNumber(nint note, string name, out double value)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var itemName = ToLmbcs(name);
        return Resolve<NSFItemGetNumberDelegate>("NSFItemGetNumber")(note, itemName.Pointer, out value) != 0;
    }

    internal double GetItemNumber(nint note, string name)
    {
        if (!TryGetItemNumber(note, name, out var value))
            throw new InvalidOperationException($"Notes item '{name}' is missing or is not numeric.");
        return value;
    }

    internal object? GetItemValue(nint note, string name)
    {
        if (!HasItem(note, name))
            return null;
        if (TryGetItemNumber(note, name, out var number))
            return number;
        return GetItemText(note, name);
    }

    internal void SetItemText(nint note, string name, string value)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var itemName = ToLmbcs(name);
        using var text = ToLmbcs((value ?? string.Empty).Replace("\r\n", "\0", StringComparison.Ordinal).Replace('\n', '\0'));
        if (text.Length > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value), "Notes text item exceeds the 65535-byte LMBCS limit.");

        CheckStatus(
            Resolve<NSFItemSetTextDelegate>("NSFItemSetText")(
                note,
                itemName.Pointer,
                text.Pointer,
                checked((ushort)text.Length)),
            "NSFItemSetText");
    }

    internal void SetItemNumber(nint note, string name, double value)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var itemName = ToLmbcs(name);
        CheckStatus(Resolve<NSFItemSetNumberDelegate>("NSFItemSetNumber")(note, itemName.Pointer, ref value), "NSFItemSetNumber");
    }

    internal void SetItemValue(nint note, string name, object? value)
    {
        if (value is null)
        {
            DeleteItem(note, name);
            return;
        }

        if (value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
        {
            SetItemNumber(note, name, Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture));
            return;
        }

        if (value is NotesDateTime or DateTime or DateTimeOffset)
            throw new NotSupportedException("Native TIMEDATE item writes are not implemented yet.");

        SetItemText(note, name, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
    }

    internal void DeleteItem(nint note, string name)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var itemName = ToLmbcs(name);

        if (!HasItem(note, name))
            return;

        CheckStatus(
            Resolve<NSFItemDeleteDelegate>("NSFItemDelete")(
                note,
                itemName.Pointer,
                checked((ushort)Math.Min(itemName.Length, ushort.MaxValue))),
            "NSFItemDelete");
    }

    internal void SaveNote(nint note)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus(Resolve<NSFNoteUpdateDelegate>("NSFNoteUpdate")(note, 0), "NSFNoteUpdate");
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort NSFItemInfoDelegate(
        nint note,
        nint itemName,
        ushort itemNameLength,
        out ushort itemFlags,
        out ushort dataType,
        out nint valueBlockId,
        out uint valueLength);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NSFItemGetTextDelegate(nint note, nint itemName, nint output, ushort outputLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int NSFItemGetNumberDelegate(nint note, nint itemName, out double value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NSFItemSetTextDelegate(nint note, nint itemName, nint text, ushort textLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NSFItemSetNumberDelegate(nint note, nint itemName, ref double value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NSFItemDeleteDelegate(nint note, nint itemName, ushort itemNameLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort NSFNoteUpdateDelegate(nint note, ushort flags);
}
