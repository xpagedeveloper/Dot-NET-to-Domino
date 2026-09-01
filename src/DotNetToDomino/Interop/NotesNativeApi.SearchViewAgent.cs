using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NotesCollectionPosition
{
    public ushort Level;
    public byte MinLevel;
    public byte MaxLevel;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
    public uint[] Tumbler;
    public static NotesCollectionPosition Create() => new() { Tumbler = new uint[32] };
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct NotesGlobalInstanceId
{
    public NotesTimeDate File;
    public NotesTimeDate Note;
    public uint NoteId;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct NotesOriginatorId
{
    public NotesTimeDate File;
    public NotesTimeDate Note;
    public uint Sequence;
    public NotesTimeDate SequenceTime;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct NotesSearchMatch
{
    public NotesGlobalInstanceId Id;
    public NotesOriginatorId OriginatorId;
    public ushort NoteClass;
    public byte SERetFlags;
    public byte Privileges;
    public ushort SummaryLength;
}

internal sealed partial class NotesNativeApi
{
    private const ushort SearchNoteClassDocument = 0x0001;
    private const ushort SearchNoteClassView = 0x0008;
    private const ushort SearchNoteClassFilter = 0x0200;
    private const byte SearchMatchFlag = 0x01;
    private const uint ReadMaskNoteId = 0x00000001;
    private const ushort NavigateCurrent = 0;
    private const ushort NavigateNext = 1;
    private const ushort FindPartial = 0x0001;
    private const uint FtSearchSetCollection = 0x00000001;
    private const uint FtSearchReturnIdTable = 0x00000010;
    private const ushort AgentRedirectMemory = 2;
    private const ushort AgentUnsignedStatus = 0x0259;

    internal nint OpenView(nint db, string name)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var viewName = ToLmbcs(name);
        CheckStatus(Resolve<Search_NIFFindDesignNote>("NIFFindDesignNote")(db, viewName.Pointer, SearchNoteClassView, out var noteId), "NIFFindDesignNote(view)");
        CheckStatus(Resolve<Search_NIFOpenCollection>("NIFOpenCollection")(db, db, noteId, 0, 0, out var collection, 0, 0, 0, 0), "NIFOpenCollection");
        return collection;
    }

    internal void CloseView(nint collection)
    {
        if (collection == 0 || !IsInitialized) return;
        using var thread = EnterThread();
        CheckStatus(Resolve<Search_NIFCloseCollection>("NIFCloseCollection")(collection), "NIFCloseCollection");
    }

    internal void UpdateCollection(nint collection)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus(Resolve<Search_NIFUpdateCollection>("NIFUpdateCollection")(collection), "NIFUpdateCollection");
    }

    internal IReadOnlyList<uint> ReadViewNoteIds(nint collection, int maximum = 0)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var position = NotesCollectionPosition.Create();
        position.Tumbler[0] = 1;
        return ReadNoteIds(collection, ref position, maximum <= 0 ? uint.MaxValue : checked((uint)maximum));
    }

    internal IReadOnlyList<uint> FindViewByTextKey(nint collection, string key, int maximum, bool exactMatch)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var text = ToLmbcs(key);
        var position = NotesCollectionPosition.Create();
        var flags = exactMatch ? (ushort)0 : FindPartial;
        var status = Resolve<Search_NIFFindByName>("NIFFindByName")(collection, text.Pointer, flags, ref position, out var matches);
        if (status != 0) return Array.Empty<uint>();
        if (matches == 0) return Array.Empty<uint>();
        var requested = maximum > 0 ? Math.Min(matches, (uint)maximum) : matches;
        return ReadNoteIds(collection, ref position, requested);
    }

    private IReadOnlyList<uint> ReadNoteIds(nint collection, ref NotesCollectionPosition position, uint requested)
    {
        var ids = new List<uint>();
        var remaining = requested;
        var firstRead = true;
        while (remaining > 0)
        {
            var batch = Math.Min(remaining, 1024u);
            CheckStatus(Resolve<Search_NIFReadEntries>("NIFReadEntries")(
                collection, ref position, firstRead ? NavigateCurrent : NavigateNext, firstRead ? 0u : 1u,
                NavigateNext, batch, ReadMaskNoteId,
                out var buffer, out var bufferLength, out _, out var returned, out _), "NIFReadEntries");
            if (buffer == 0 || returned == 0)
            {
                if (buffer != 0) Resolve<Search_OSMemFree>("OSMemFree")(buffer);
                break;
            }
            try
            {
                if (bufferLength < returned * sizeof(uint)) throw new NotesException("NIFReadEntries returned an invalid NOTEID buffer.");
                var pointer = Resolve<Search_OSLockObject>("OSLockObject")(buffer);
                if (pointer == 0) throw new NotesException("Unable to lock Notes NIF result memory.");
                try
                {
                    for (var i = 0u; i < returned; i++)
                        ids.Add(unchecked((uint)Marshal.ReadInt32(pointer, checked((int)i * sizeof(uint)))));
                }
                finally { Resolve<Search_OSUnlockObject>("OSUnlockObject")(buffer); }
            }
            finally { Resolve<Search_OSMemFree>("OSMemFree")(buffer); }
            if (requested != uint.MaxValue) remaining -= Math.Min(remaining, returned);
            firstRead = false;
            if (requested == uint.MaxValue && returned < batch) break;
        }
        return ids.Distinct().ToArray();
    }

    internal IReadOnlyList<uint> Search(nint db, string formula, int maximum)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        if (string.IsNullOrWhiteSpace(formula)) formula = "@All";
        using var formulaText = ToLmbcs(formula);
        if (formulaText.Length > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(formula));
        var status = Resolve<Search_NSFFormulaCompile>("NSFFormulaCompile")(
            0, 0, formulaText.Pointer, checked((ushort)formulaText.Length), out var formulaHandle, out _,
            out var compileError, out var errorLine, out var errorColumn, out _, out _);
        if (status != 0 || compileError != 0)
        {
            if (formulaHandle != 0) Resolve<Search_OSMemFree>("OSMemFree")(formulaHandle);
            throw new NotesException(status != 0 ? status : compileError, $"Notes formula compilation failed at line {errorLine}, column {errorColumn}.");
        }
        var ids = new List<uint>();
        Search_NSFSearchProc callback = (_, matchPointer, _) =>
        {
            if (matchPointer == 0) return 0;
            var match = Marshal.PtrToStructure<NotesSearchMatch>(matchPointer);
            if ((match.SERetFlags & SearchMatchFlag) != 0 && (match.NoteClass & SearchNoteClassDocument) != 0 && match.Id.NoteId != 0)
            {
                ids.Add(match.Id.NoteId);
                if (maximum > 0 && ids.Count >= maximum) return 1;
            }
            return 0;
        };
        try
        {
            var searchStatus = Resolve<Search_NSFSearch>("NSFSearch")(db, formulaHandle, 0, 0, SearchNoteClassDocument, 0, callback, 0, 0);
            if (searchStatus != 0 && !(maximum > 0 && ids.Count >= maximum)) CheckStatus(searchStatus, "NSFSearch");
            return ids.Distinct().ToArray();
        }
        finally
        {
            Resolve<Search_OSMemFree>("OSMemFree")(formulaHandle);
            GC.KeepAlive(callback);
        }
    }

    internal IReadOnlyList<uint> FullTextSearch(nint db, nint collection, string query, int maximum)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("Full-text query cannot be empty.", nameof(query));
        using var queryText = ToLmbcs(query);
        CheckStatus(Resolve<Search_FTOpenSearch>("FTOpenSearch")(out var searchHandle), "FTOpenSearch");
        nint results = 0;
        try
        {
            var options = FtSearchReturnIdTable | (collection != 0 ? FtSearchSetCollection : 0);
            var limit = maximum <= 0 ? (ushort)0 : checked((ushort)Math.Min(maximum, ushort.MaxValue));
            var status = Resolve<Search_FTSearch>("FTSearch")(db, ref searchHandle, collection, queryText.Pointer, options, limit, 0, out var count, 0, out results);
            if (status != 0) return Array.Empty<uint>();
            if (results == 0 || count == 0) return Array.Empty<uint>();
            var scan = Resolve<Search_IDScan>("IDScan");
            var ids = new List<uint>();
            uint id = 0;
            var first = 1;
            while (scan(results, first, ref id) != 0)
            {
                first = 0;
                if (id != 0) ids.Add(id);
                if (maximum > 0 && ids.Count >= maximum) break;
            }
            return ids;
        }
        finally
        {
            if (results != 0) Resolve<Search_OSMemFree>("OSMemFree")(results);
            if (searchHandle != 0) CheckStatus(Resolve<Search_FTCloseSearch>("FTCloseSearch")(searchHandle), "FTCloseSearch");
        }
    }

    internal uint FindAgentNoteId(nint db, string name)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var agentName = ToLmbcs(name);
        var status = Resolve<Search_NIFFindDesignNote>("NIFFindDesignNote")(db, agentName.Pointer, SearchNoteClassFilter, out var noteId);
        return status == 0 ? noteId : 0;
    }

    internal string RunAgent(nint db, string name, nint documentContext)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var noteId = FindAgentNoteId(db, name);
        if (noteId == 0) throw new NotesException($"Notes agent '{name}' was not found.");
        var openAgent = Resolve<Search_AgentOpen>("AgentOpen");
        var openStatus = openAgent(db, noteId, out var agent);
        if (openStatus == AgentUnsignedStatus)
        {
            SaveAgent(db, noteId);
            openStatus = openAgent(db, noteId, out agent);
        }
        CheckStatus(openStatus, "AgentOpen");
        nint context = 0;
        try
        {
            CheckStatus(Resolve<Search_AgentCreateRunContext>("AgentCreateRunContext")(agent, 0, 0, out context), "AgentCreateRunContext");
            if (documentContext != 0) CheckStatus(Resolve<Search_AgentSetDocumentContext>("AgentSetDocumentContext")(context, documentContext), "AgentSetDocumentContext");
            CheckStatus(Resolve<Search_AgentRedirectStdout>("AgentRedirectStdout")(context, AgentRedirectMemory), "AgentRedirectStdout");
            CheckStatus(Resolve<Search_AgentRun>("AgentRun")(agent, context, 0, 0), "AgentRun");
            Resolve<Search_AgentQueryStdoutBuffer>("AgentQueryStdoutBuffer")(context, out var outputHandle, out var outputLength);
            if (outputHandle == 0 || outputLength == 0) return string.Empty;
            var pointer = Resolve<Search_OSLockObject>("OSLockObject")(outputHandle);
            if (pointer == 0) return string.Empty;
            try { return FromLmbcs(pointer, checked((int)outputLength)); }
            finally { Resolve<Search_OSUnlockObject>("OSUnlockObject")(outputHandle); }
        }
        finally
        {
            if (context != 0) Resolve<Search_AgentDestroyRunContext>("AgentDestroyRunContext")(context);
            Resolve<Search_AgentClose>("AgentClose")(agent);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Search_NIFFindDesignNote(nint db, nint name, ushort noteClass, out uint noteId);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Search_NIFOpenCollection(nint viewDb, nint dataDb, uint viewNoteId, ushort openFlags, nint unreadList, out nint collection, nint viewNote, nint viewUnid, nint collapsedList, nint selectedList);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Search_NIFUpdateCollection(nint collection);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Search_NIFCloseCollection(nint collection);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Search_NIFFindByName(nint collection, nint name, ushort flags, ref NotesCollectionPosition position, out uint matches);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Search_NIFReadEntries(nint collection, ref NotesCollectionPosition position, ushort skipNavigator, uint skipCount, ushort returnNavigator, uint returnCount, uint readMask, out nint buffer, out ushort bufferLength, out uint entriesSkipped, out uint entriesReturned, out ushort signalFlags);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Search_OSLockObject(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Search_OSUnlockObject(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Search_OSMemFree(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Search_NSFFormulaCompile(nint formulaName, ushort formulaNameLength, nint formulaText, ushort formulaTextLength, out nint formula, out ushort formulaLength, out ushort compileError, out ushort errorLine, out ushort errorColumn, out ushort errorOffset, out ushort errorLength);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Search_NSFSearchProc(nint parameter, nint match, nint summary);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Search_NSFSearch(nint db, nint formula, nint viewTitle, ushort searchFlags, ushort noteClassMask, nint since, Search_NSFSearchProc callback, nint callbackParameter, nint until);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Search_FTOpenSearch(out nint search);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Search_FTSearch(nint db, ref nint search, nint collection, nint query, uint options, ushort limit, nint idTable, out uint numDocs, nint reserved, out nint results);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Search_FTCloseSearch(nint search);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Search_IDScan(nint table, int first, ref uint noteId);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Search_AgentOpen(nint db, uint noteId, out nint agent);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Search_AgentClose(nint agent);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Search_AgentCreateRunContext(nint agent, nint reserved, uint flags, out nint context);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Search_AgentDestroyRunContext(nint context);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Search_AgentSetDocumentContext(nint context, nint note);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Search_AgentRedirectStdout(nint context, ushort redirectType);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Search_AgentRun(nint agent, nint context, nint selection, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Search_AgentQueryStdoutBuffer(nint context, out nint outputHandle, out uint outputLength);
}
