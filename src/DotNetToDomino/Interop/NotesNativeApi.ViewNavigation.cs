using System.Globalization;
using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal enum NotesViewEntryKind
{
    Document,
    Category,
    Total
}

internal sealed class NotesViewRow
{
    private readonly NotesSession _session;
    private object?[]? _columnValues;
    private bool _columnValuesLoaded;
    private readonly object _columnValuesGate = new();

    internal NotesViewRow(
        NotesSession session,
        NotesDatabase database,
        string viewName,
        uint viewNoteId,
        uint noteId,
        string position,
        int level,
        int indentLevel,
        NotesViewEntryKind kind)
    {
        _session = session;
        Database = database;
        ViewName = viewName;
        ViewNoteId = viewNoteId;
        NoteId = noteId;
        Position = position;
        Level = level;
        IndentLevel = indentLevel;
        Kind = kind;
    }

    internal NotesDatabase Database { get; }
    internal string ViewName { get; }
    internal uint ViewNoteId { get; }
    internal uint NoteId { get; }
    internal string Position { get; }
    internal int Level { get; }
    internal int IndentLevel { get; }
    internal NotesViewEntryKind Kind { get; }
    internal int FTSearchScore { get; set; }
    internal bool IsDocument => Kind == NotesViewEntryKind.Document;

    internal object?[] GetColumnValues()
    {
        if (_columnValuesLoaded) return _columnValues ?? [];
        lock (_columnValuesGate)
        {
            if (_columnValuesLoaded) return _columnValues ?? [];
            _columnValues = _session.Api.ReadViewRowColumnValues(Database.Handle, ViewName, Position, _session);
            _columnValuesLoaded = true;
            return _columnValues;
        }
    }
}

internal sealed partial class NotesNativeApi
{
    private const uint ViewReadMaskIndentLevels = 0x00000080;
    private const uint ViewReadMaskIndexPosition = 0x00004000;
    private const uint ViewReadMaskSummaryValues = 0x00002000;
    private const uint ViewNoteIdCategory = 0x80000000u;
    private const uint ViewNoteIdCategoryTotal = 0xC0000000u;

    internal NotesViewRow[] ReadViewRows(ushort collection, NotesDatabase database, string viewName, NotesSession session)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var name = ToLmbcs(viewName);
        CheckStatus(Resolve<Search_NIFFindDesignNote>("NIFFindDesignNote")(database.Handle, name.Pointer, SearchNoteClassView, out var viewNoteId), "NIFFindDesignNote(view rows)");

        var rows = new List<NotesViewRow>();
        var position = NotesCollectionPosition.Create();
        position.Level = 0;
        position.Tumbler[0] = 0;

        while (true)
        {
            CheckStatus(Resolve<Search_NIFReadEntries>("NIFReadEntries")(
                collection,
                ref position,
                NavigateNext,
                1,
                NavigateNext,
                1024,
                ReadMaskNoteId | ViewReadMaskIndentLevels | ViewReadMaskIndexPosition,
                out var buffer,
                out var bufferLength,
                out _,
                out var returned,
                out var signalFlags), "NIFReadEntries(view rows)");

            if (buffer == 0 || returned == 0)
            {
                if (buffer != 0) Resolve<Search_OSMemFree>("OSMemFree")(buffer);
                break;
            }

            try
            {
                var pointer = Resolve<Search_OSLockObject>("OSLockObject")(buffer);
                if (pointer == 0) throw new NotesException("Unable to lock Notes view row buffer.");
                try
                {
                    var cursor = 0;
                    for (var i = 0u; i < returned; i++)
                    {
                        EnsureViewBuffer(bufferLength, cursor, 10);
                        var noteId = unchecked((uint)Marshal.ReadInt32(pointer, cursor));
                        cursor += 4;
                        var indent = unchecked((ushort)Marshal.ReadInt16(pointer, cursor));
                        cursor += 2;
                        var level = unchecked((ushort)Marshal.ReadInt16(pointer, cursor));
                        var positionSize = checked(4 + ((int)level + 1) * 4);
                        EnsureViewBuffer(bufferLength, cursor, positionSize);

                        var parts = new string[level + 1];
                        for (var part = 0; part <= level; part++)
                        {
                            var tumbler = unchecked((uint)Marshal.ReadInt32(pointer, cursor + 4 + part * 4));
                            parts[part] = tumbler.ToString(CultureInfo.InvariantCulture);
                        }
                        cursor += positionSize;

                        var kind = (noteId & ViewNoteIdCategoryTotal) == ViewNoteIdCategoryTotal
                            ? NotesViewEntryKind.Total
                            : (noteId & ViewNoteIdCategory) != 0
                                ? NotesViewEntryKind.Category
                                : NotesViewEntryKind.Document;

                        rows.Add(new NotesViewRow(session, database, viewName, viewNoteId, noteId, string.Join('.', parts), level, indent, kind));
                    }
                }
                finally { Resolve<Search_OSUnlockObject>("OSUnlockObject")(buffer); }
            }
            finally { Resolve<Search_OSMemFree>("OSMemFree")(buffer); }
            if ((signalFlags & 0x0020) == 0) break; // SIGNAL_MORE_TO_DO
        }

        return rows.ToArray();
    }

    internal object?[] ReadViewRowColumnValues(nint db, string viewName, string positionText, NotesSession session)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var collection = OpenView(db, viewName);
        try { return ReadViewRowColumnValuesCore(collection, positionText, session); }
        finally { CloseView(collection); }
    }

    private object?[] ReadViewRowColumnValuesCore(ushort collection, string positionText, NotesSession session)
    {
        var position = ParseViewPosition(positionText);
        CheckStatus(Resolve<Search_NIFReadEntries>("NIFReadEntries")(
            collection,
            ref position,
            NavigateCurrent,
            0,
            NavigateCurrent,
            1,
            ViewReadMaskSummaryValues,
            out var buffer,
            out var bufferLength,
            out _,
            out var returned,
            out _), "NIFReadEntries(view column values)");

        if (buffer == 0 || returned == 0)
        {
            if (buffer != 0) Resolve<Search_OSMemFree>("OSMemFree")(buffer);
            return [];
        }

        try
        {
            var pointer = Resolve<Search_OSLockObject>("OSLockObject")(buffer);
            if (pointer == 0) throw new NotesException("Unable to lock Notes view summary buffer.");
            try
            {
                EnsureViewBuffer(bufferLength, 0, 4);
                var tableLength = unchecked((ushort)Marshal.ReadInt16(pointer, 0));
                var itemCount = unchecked((ushort)Marshal.ReadInt16(pointer, 2));
                if (tableLength > bufferLength || tableLength < 4 + itemCount * 2)
                    throw new NotesException("Invalid Notes ITEM_VALUE_TABLE.");

                var lengths = new ushort[itemCount];
                var cursor = 4;
                for (var i = 0; i < itemCount; i++)
                {
                    lengths[i] = unchecked((ushort)Marshal.ReadInt16(pointer, cursor));
                    cursor += 2;
                }

                var values = new object?[itemCount];
                for (var i = 0; i < itemCount; i++)
                {
                    EnsureViewBuffer(tableLength, cursor, lengths[i]);
                    values[i] = DecodeViewValue(nint.Add(pointer, cursor), lengths[i], session);
                    cursor += lengths[i];
                }
                return values;
            }
            finally { Resolve<Search_OSUnlockObject>("OSUnlockObject")(buffer); }
        }
        finally { Resolve<Search_OSMemFree>("OSMemFree")(buffer); }
    }

    internal bool IsDocumentUnread(nint db, uint noteId, string userName)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        userName = userName.Trim();
        if (userName.Length == 0) throw new ArgumentException("Unread status requires a user name.", nameof(userName));
        using var user = ToLmbcs(userName);
        CheckStatus(Resolve<Doc_NSFDbGetUnreadNoteTable>("NSFDbGetUnreadNoteTable")(
            db, user.Pointer, checked((ushort)user.Length), 1, out var table), "NSFDbGetUnreadNoteTable");
        if (table == 0) return false;
        try { return Resolve<View_IDIsPresent>("IDIsPresent")(table, noteId & 0x7fffffffu) != 0; }
        finally { _ = Resolve<Doc_IDDestroyTable>("IDDestroyTable")(table); }
    }

    private object? DecodeViewValue(nint pointer, int length, NotesSession session)
    {
        if (length < 2) return null;
        var type = unchecked((ushort)Marshal.ReadInt16(pointer, 0));
        var value = nint.Add(pointer, 2);
        var valueLength = length - 2;

        if (type == NotesTypeText) return valueLength == 0 ? string.Empty : FromLmbcs(value, valueLength);
        if (type == NotesTypeNumber && valueLength >= sizeof(double)) return Marshal.PtrToStructure<double>(value);
        if (type == NotesTypeTime && valueLength >= Marshal.SizeOf<NotesTimeDate>()) return NotesDateTime.FromNative(session, Marshal.PtrToStructure<NotesTimeDate>(value));
        if (type == NotesTypeTextList) return DecodeViewTextList(value, valueLength);
        return null;
    }

    private object?[] DecodeViewTextList(nint pointer, int length)
    {
        if (length < 2) return [];
        var count = unchecked((ushort)Marshal.ReadInt16(pointer, 0));
        if (length < 2 + count * 2) return [];
        var lengths = new ushort[count];
        var cursor = 2;
        for (var i = 0; i < count; i++)
        {
            lengths[i] = unchecked((ushort)Marshal.ReadInt16(pointer, cursor));
            cursor += 2;
        }

        var values = new object?[count];
        for (var i = 0; i < count; i++)
        {
            if (cursor + lengths[i] > length) throw new NotesException("Invalid Notes text-list view value.");
            values[i] = lengths[i] == 0 ? string.Empty : FromLmbcs(nint.Add(pointer, cursor), lengths[i]);
            cursor += lengths[i];
        }
        return values;
    }

    private static NotesCollectionPosition ParseViewPosition(string text)
    {
        var parts = (text ?? string.Empty).Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Length > 32) throw new ArgumentException("Invalid Notes view position.", nameof(text));
        var position = NotesCollectionPosition.Create();
        position.Level = checked((ushort)(parts.Length - 1));
        for (var i = 0; i < parts.Length; i++)
            position.Tumbler[i] = uint.Parse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture);
        return position;
    }

    private static void EnsureViewBuffer(int bufferLength, int offset, int required)
    {
        if (offset < 0 || required < 0 || offset > bufferLength - required)
            throw new NotesException($"Truncated Notes view entry buffer: length={bufferLength}, offset={offset}, required={required}.");
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int View_IDIsPresent(uint table, uint id);
}
