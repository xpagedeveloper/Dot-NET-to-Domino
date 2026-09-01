using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal readonly record struct NotesItemMetadata(
    string Name,
    NotesNativeApi.NotesBlockId ItemBlock,
    NotesNativeApi.NotesBlockId ValueBlock,
    ushort Flags,
    ushort DataType,
    uint ValueLength);

internal sealed partial class NotesNativeApi
{
    internal const ushort NotesTypeComposite = 0x0001;
    internal const ushort NotesTypeNumber = 0x0300;
    internal const ushort NotesTypeNumberRange = 0x0301;
    internal const ushort NotesTypeTime = 0x0400;
    internal const ushort NotesTypeTimeRange = 0x0401;
    internal const ushort NotesTypeText = 0x0500;
    internal const ushort NotesTypeTextList = 0x0501;
    internal const ushort NotesItemSign = 0x0001;
    internal const ushort NotesItemSeal = 0x0002;
    internal const ushort NotesItemSummary = 0x0004;
    internal const ushort NotesItemReadWriters = 0x0020;
    internal const ushort NotesItemNames = 0x0040;
    internal const ushort NotesItemProtected = 0x0200;
    internal const ushort NotesItemReaders = 0x0400;

    internal NotesItemMetadata GetItemMetadata(nint note, string name)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var itemName = ToLmbcs(name);
        CheckStatus(Resolve<ItemEx_NSFItemInfo>("NSFItemInfo")(
            note, itemName.Pointer, checked((ushort)itemName.Length), out var itemBlock,
            out var dataType, out var valueBlock, out var valueLength), "NSFItemInfo");
        const int capacity = 1024;
        var nameBuffer = Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(nameBuffer, capacity);
            Resolve<ItemEx_NSFItemQuery>("NSFItemQuery")(note, itemBlock, nameBuffer, checked((ushort)(capacity - 1)),
                out var actualNameLength, out var flags, out var queryType, out var queryValueBlock, out var queryLength);
            var actualName = actualNameLength == 0 ? name : FromLmbcs(nameBuffer, actualNameLength);
            return new NotesItemMetadata(
                actualName,
                itemBlock,
                queryValueBlock.Pool == 0 ? valueBlock : queryValueBlock,
                flags,
                queryType == 0 ? dataType : queryType,
                queryLength == 0 ? valueLength : queryLength);
        }
        finally { Marshal.FreeHGlobal(nameBuffer); }
    }

    internal NotesTimeDate GetItemTime(nint note, string name)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var itemName = ToLmbcs(name);
        if (Resolve<ItemEx_NSFItemGetTime>("NSFItemGetTime")(note, itemName.Pointer, out var value) == 0)
            throw new InvalidOperationException($"Notes item '{name}' is missing or is not a time/date.");
        return value;
    }

    internal NotesTimeDate GetItemModifiedTime(nint note, string name)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var itemName = ToLmbcs(name);
        CheckStatus(Resolve<ItemEx_NSFItemGetModifiedTime>("NSFItemGetModifiedTime")(
            note, itemName.Pointer, checked((ushort)itemName.Length), 0, out var value), "NSFItemGetModifiedTime");
        return value;
    }

    internal void SetItemTime(nint note, string name, NotesTimeDate value)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var itemName = ToLmbcs(name);
        CheckStatus(Resolve<ItemEx_NSFItemSetTime>("NSFItemSetTime")(note, itemName.Pointer, ref value), "NSFItemSetTime");
    }

    internal object?[] GetItemValues(nint note, string name, Func<NotesTimeDate, object> timeFactory)
    {
        if (!HasItem(note, name)) return [];
        var info = GetItemMetadata(note, name);
        return info.DataType switch
        {
            NotesTypeText => [GetItemText(note, name)],
            NotesTypeTextList => GetTextListValues(note, name),
            NotesTypeNumber => [GetItemNumber(note, name)],
            NotesTypeNumberRange => GetNumberListValues(info),
            NotesTypeTime => [timeFactory(GetItemTime(note, name))],
            NotesTypeTimeRange => GetTimeListValues(info, timeFactory),
            _ => [ConvertItemToText(note, name)]
        };
    }

    internal void SetItemValues(nint note, string name, object? value)
    {
        EnsureInitialized();
        var isSequence = value is Array || (value is System.Collections.IEnumerable && value is not string && value is not byte[]);
        var values = ExpandManagedValues(value);
        if (!HasItem(note, name)) SetItemText(note, name, string.Empty);
        var info = GetItemMetadata(note, name);

        if (values.Length == 0)
        {
            SetTextValuePreserveFlags(note, info, string.Empty);
            return;
        }

        if (values.All(v => v is NotesDateTime or DateTime or DateTimeOffset))
        {
            var dates = values.Select(ToNativeTimeDate).ToArray();
            if (!isSequence && dates.Length == 1)
            {
                SetItemTime(note, name, dates[0]);
                SetItemFlags(note, name, info.Flags);
            }
            else SetTimeListValue(note, info, dates);
            return;
        }

        if (values.All(IsNumericValue))
        {
            var numbers = values.Select(v => Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            if (!isSequence && numbers.Length == 1)
            {
                SetItemNumber(note, name, numbers[0]);
                SetItemFlags(note, name, info.Flags);
            }
            else SetNumberListValue(note, info, numbers);
            return;
        }

        if (values.All(v => v is null or string))
        {
            var strings = values.Select(v => v?.ToString() ?? string.Empty).ToArray();
            if (!isSequence && strings.Length == 1) SetTextValuePreserveFlags(note, info, strings[0]);
            else SetTextListValue(note, info, strings);
            return;
        }

        throw new ArgumentException("Notes item values must be homogeneous text, numeric, or NotesDateTime values.", nameof(value));
    }

    internal void SetItemFlag(nint note, string name, ushort flag, bool enabled)
    {
        var info = GetItemMetadata(note, name);
        var flags = enabled ? (ushort)(info.Flags | flag) : (ushort)(info.Flags & ~flag);
        SetItemFlags(note, name, flags);
    }

    internal void SetItemFlags(nint note, string name, ushort newFlags)
    {
        var info = GetItemMetadata(note, name);
        var raw = CopyItemValueWithoutType(info);
        var pointer = Marshal.AllocHGlobal(Math.Max(1, raw.Length));
        try
        {
            if (raw.Length > 0) Marshal.Copy(raw, 0, pointer, raw.Length);
            CheckStatus(Resolve<ItemEx_NSFItemModifyValue>("NSFItemModifyValue")(
                note, info.ItemBlock, newFlags, info.DataType, pointer, checked((uint)raw.Length)), "NSFItemModifyValue(flags)");
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    private object?[] GetTextListValues(nint note, string name)
    {
        using var itemName = ToLmbcs(name);
        var count = Resolve<ItemEx_NSFItemGetTextListEntries>("NSFItemGetTextListEntries")(note, itemName.Pointer);
        var values = new object?[count];
        for (ushort i = 0; i < count; i++)
        {
            const int capacity = 65535;
            var buffer = Marshal.AllocHGlobal(capacity);
            try
            {
                Zero(buffer, capacity);
                var length = Resolve<ItemEx_NSFItemGetTextListEntry>("NSFItemGetTextListEntry")(note, itemName.Pointer, i, buffer, ushort.MaxValue);
                values[i] = length == 0 ? string.Empty : FromLmbcs(buffer, length);
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        return values;
    }

    private object?[] GetNumberListValues(NotesItemMetadata info)
    {
        var raw = CopyItemValueWithoutType(info);
        if (raw.Length < 4) return [];
        var count = BitConverter.ToUInt16(raw, 0);
        var required = checked(4 + count * sizeof(double));
        if (raw.Length < required) throw new NotesException("Invalid Notes number-list item data.");
        var values = new object?[count];
        for (var i = 0; i < count; i++) values[i] = BitConverter.ToDouble(raw, 4 + i * sizeof(double));
        return values;
    }

    private object?[] GetTimeListValues(NotesItemMetadata info, Func<NotesTimeDate, object> timeFactory)
    {
        var raw = CopyItemValueWithoutType(info);
        if (raw.Length < 4) return [];
        var count = BitConverter.ToUInt16(raw, 0);
        var size = Marshal.SizeOf<NotesTimeDate>();
        var required = checked(4 + count * size);
        if (raw.Length < required) throw new NotesException("Invalid Notes date-time-list item data.");
        var values = new object?[count];
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            for (var i = 0; i < count; i++)
            {
                Marshal.Copy(raw, 4 + i * size, buffer, size);
                values[i] = timeFactory(Marshal.PtrToStructure<NotesTimeDate>(buffer));
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
        return values;
    }

    private void SetTextValuePreserveFlags(nint note, NotesItemMetadata info, string value)
    {
        SetItemText(note, info.Name, value);
        SetItemFlags(note, info.Name, info.Flags);
    }

    private void SetTextListValue(nint note, NotesItemMetadata info, string[] values)
    {
        DeleteItem(note, info.Name);
        using var itemName = ToLmbcs(info.Name);
        using var first = ToLmbcs(values.Length == 0 ? string.Empty : values[0]);
        CheckStatus(Resolve<ItemEx_NSFItemCreateTextList>("NSFItemCreateTextList")(
            note, itemName.Pointer, first.Pointer, checked((ushort)first.Length)), "NSFItemCreateTextList");
        for (var i = 1; i < values.Length; i++)
        {
            using var entry = ToLmbcs(values[i]);
            CheckStatus(Resolve<ItemEx_NSFItemAppendTextList>("NSFItemAppendTextList")(
                note, itemName.Pointer, entry.Pointer, checked((ushort)entry.Length), 1), "NSFItemAppendTextList");
        }
        SetItemFlags(note, info.Name, info.Flags);
    }

    private void SetNumberListValue(nint note, NotesItemMetadata info, double[] values)
    {
        var length = checked(4 + values.Length * sizeof(double));
        var pointer = Marshal.AllocHGlobal(length);
        try
        {
            Zero(pointer, length);
            Marshal.WriteInt16(pointer, 0, checked((short)values.Length));
            Marshal.WriteInt16(pointer, 2, 0);
            var bytes = new byte[values.Length * sizeof(double)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            if (bytes.Length > 0) Marshal.Copy(bytes, 0, nint.Add(pointer, 4), bytes.Length);
            CheckStatus(Resolve<ItemEx_NSFItemModifyValue>("NSFItemModifyValue")(
                note, info.ItemBlock, info.Flags, NotesTypeNumberRange, pointer, checked((uint)length)), "NSFItemModifyValue(number list)");
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    private void SetTimeListValue(nint note, NotesItemMetadata info, NotesTimeDate[] values)
    {
        var tdSize = Marshal.SizeOf<NotesTimeDate>();
        var length = checked(4 + values.Length * tdSize);
        var pointer = Marshal.AllocHGlobal(length);
        try
        {
            Zero(pointer, length);
            Marshal.WriteInt16(pointer, 0, checked((short)values.Length));
            Marshal.WriteInt16(pointer, 2, 0);
            for (var i = 0; i < values.Length; i++) Marshal.StructureToPtr(values[i], nint.Add(pointer, 4 + i * tdSize), false);
            CheckStatus(Resolve<ItemEx_NSFItemModifyValue>("NSFItemModifyValue")(
                note, info.ItemBlock, info.Flags, NotesTypeTimeRange, pointer, checked((uint)length)), "NSFItemModifyValue(time list)");
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    private byte[] CopyItemValueWithoutType(NotesItemMetadata info)
    {
        if (info.ValueLength <= 2 || info.ValueBlock.Pool == 0) return [];
        var basePointer = Resolve<ItemEx_OSLockObject>("OSLockObject")(info.ValueBlock.Pool);
        if (basePointer == 0) throw new NotesException("Unable to lock Notes item value memory.");
        try
        {
            var length = checked((int)info.ValueLength - 2);
            var raw = new byte[length];
            Marshal.Copy(nint.Add(basePointer, info.ValueBlock.Block + 2), raw, 0, length);
            return raw;
        }
        finally { Resolve<ItemEx_OSUnlockObject>("OSUnlockObject")(info.ValueBlock.Pool); }
    }

    private NotesTimeDate ToNativeTimeDate(object? value) => value switch
    {
        NotesDateTime notesDateTime => notesDateTime.NativeValue,
        DateTimeOffset dateTimeOffset => ParseTimeDate(dateTimeOffset.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)),
        DateTime dateTime => ParseTimeDate(dateTime.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)),
        _ => throw new ArgumentException("Value is not a date/time.")
    };

    private static object?[] ExpandManagedValues(object? value)
    {
        if (value is null || value is string || value is NotesDateTime || value is DateTime || value is DateTimeOffset || IsNumericValue(value)) return [value];
        if (value is Array array)
        {
            var result = new object?[array.Length];
            for (var i = 0; i < array.Length; i++) result[i] = array.GetValue(i);
            return result;
        }
        if (value is System.Collections.IEnumerable enumerable)
        {
            var result = new List<object?>();
            foreach (var item in enumerable) result.Add(item);
            return result.ToArray();
        }
        return [value];
    }

    private static bool IsNumericValue(object? value) => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private string ConvertItemToText(nint note, string name)
    {
        using var itemName = ToLmbcs(name);
        const int capacity = 60000;
        var buffer = Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(buffer, capacity);
            var length = Resolve<ItemEx_NSFItemConvertToText>("NSFItemConvertToText")(
                note, itemName.Pointer, buffer, checked((ushort)capacity), (byte)';');
            return length == 0 ? string.Empty : FromLmbcs(buffer, length);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort ItemEx_NSFItemInfo(nint note, nint itemName, ushort nameLength, out NotesBlockId itemBlock, out ushort dataType, out NotesBlockId valueBlock, out uint valueLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void ItemEx_NSFItemQuery(nint note, NotesBlockId itemBlock, nint itemName, ushort returnBufferLength, out ushort nameLength, out ushort itemFlags, out ushort dataType, out NotesBlockId valueBlock, out uint valueLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int ItemEx_NSFItemGetTime(nint note, nint itemName, out NotesTimeDate value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort ItemEx_NSFItemSetTime(nint note, nint itemName, ref NotesTimeDate value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort ItemEx_NSFItemGetModifiedTime(nint note, nint itemName, ushort itemNameLength, uint flags, out NotesTimeDate value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort ItemEx_NSFItemGetTextListEntries(nint note, nint itemName);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort ItemEx_NSFItemGetTextListEntry(nint note, nint itemName, ushort entry, nint text, ushort textLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort ItemEx_NSFItemConvertToText(nint note, nint itemName, nint text, ushort textLength, byte separator);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort ItemEx_NSFItemCreateTextList(nint note, nint itemName, nint firstText, ushort firstTextLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort ItemEx_NSFItemAppendTextList(nint note, nint itemName, nint text, ushort textLength, int allowDuplicates);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort ItemEx_NSFItemModifyValue(nint note, NotesBlockId itemBlock, ushort itemFlags, ushort dataType, nint value, uint valueLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint ItemEx_OSLockObject(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void ItemEx_OSUnlockObject(nint handle);
}
