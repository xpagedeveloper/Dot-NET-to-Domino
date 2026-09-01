using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NotesTimeDate
{
    public uint Innards0;
    public uint Innards1;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NotesExpandedTime
{
    public int Year;
    public int Month;
    public int Day;
    public int Weekday;
    public int Hour;
    public int Minute;
    public int Second;
    public int Hundredth;
    public int Dst;
    public int Zone;
    public NotesTimeDate GM;
}

internal sealed partial class NotesNativeApi
{
    internal NotesTimeDate ParseTimeDate(string value)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var text = ToLmbcs(value);
        var current = text.Pointer;
        CheckStatus(Resolve<ConvertTextToTimeDateDelegate>("ConvertTextToTIMEDATE")(
            0, 0, ref current, checked((ushort)Math.Min(text.Length, ushort.MaxValue)), out var result),
            "ConvertTextToTIMEDATE");
        return result;
    }

    internal NotesTimeDate CurrentTimeDate()
    {
        EnsureInitialized();
        using var thread = EnterThread();
        Resolve<OSCurrentTimeDateDelegate>("OSCurrentTIMEDATE")(out var value);
        return value;
    }

    internal string FormatTimeDate(NotesTimeDate value)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        const int capacity = 1024;
        var buffer = Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(buffer, capacity);
            CheckStatus(Resolve<ConvertTimeDateToTextDelegate>("ConvertTIMEDATEToText")(
                0, 0, ref value, buffer, checked((ushort)(capacity - 1)), out var length),
                "ConvertTIMEDATEToText");
            return FromLmbcs(buffer, length);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    internal NotesExpandedTime ExpandTimeDate(NotesTimeDate value)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var expanded = new NotesExpandedTime { GM = value };
        if (Resolve<TimeGMToLocalDelegate>("TimeGMToLocal")(ref expanded) != 0)
            throw new NotesException("TimeGMToLocal failed.");
        return expanded;
    }

    internal NotesExpandedTime ExpandTimeDateGmt(NotesTimeDate value)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var expanded = new NotesExpandedTime { GM = value, Zone = 0, Dst = 0 };
        if (Resolve<TimeGMToLocalZoneDelegate>("TimeGMToLocalZone")(ref expanded) != 0)
            throw new NotesException("TimeGMToLocalZone failed.");
        return expanded;
    }

    internal void AdjustTimeDate(ref NotesTimeDate value, int seconds, int minutes, int hours, int days, int months, int years)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        if (Resolve<TimeDateAdjustDelegate>("TimeDateAdjust")(ref value, seconds, minutes, hours, days, months, years) != 0)
            throw new NotesException("TimeDateAdjust failed.");
    }

    internal string AbbreviateName(string value)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var input = ToLmbcs(value);
        const int capacity = 4096;
        var output = Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(output, capacity);
            CheckStatus(Resolve<DNAbbreviateDelegate>("DNAbbreviate")(
                0, 0, input.Pointer, output, checked((ushort)(capacity - 1)), out var length),
                "DNAbbreviate");
            return FromLmbcs(output, length);
        }
        finally { Marshal.FreeHGlobal(output); }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort ConvertTextToTimeDateDelegate(nint intlFormat, nint textFormat, ref nint text, ushort maxLength, out NotesTimeDate output);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort ConvertTimeDateToTextDelegate(nint intlFormat, nint textFormat, ref NotesTimeDate value, nint output, ushort outputLength, out ushort textLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void OSCurrentTimeDateDelegate(out NotesTimeDate value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int TimeDateAdjustDelegate(ref NotesTimeDate value, int seconds, int minutes, int hours, int days, int months, int years);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int TimeGMToLocalDelegate(ref NotesExpandedTime value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int TimeGMToLocalZoneDelegate(ref NotesExpandedTime value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort DNAbbreviateDelegate(uint flags, nint templateName, nint input, nint output, ushort outputSize, out ushort outputLength);
}
