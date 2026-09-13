namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    internal uint FindAgentNoteIdWithPrivateLookup(nint db, string name)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        using var agentName = ToLmbcs(name);
        var status = Resolve<Search_NIFFindDesignNote>("NIFFindDesignNote")(db, agentName.Pointer, SearchNoteClassFilter, out var noteId);
        if (status != 0)
        {
            try { status = Resolve<Search_NIFFindPrivateDesignNote>("NIFFindPrivateDesignNote")(db, agentName.Pointer, SearchNoteClassFilter, out noteId); }
            catch (EntryPointNotFoundException) { }
        }
        if (status == 0) return noteId;
        var text = TryLoadStatusText(status);
        if (text.Contains("not found", StringComparison.OrdinalIgnoreCase)) return 0;
        CheckStatus(status, "NIFFindDesignNote(agent)");
        return 0;
    }

    internal bool GetAgentEnabled(nint db, uint noteId)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus(Resolve<Search_AgentOpen>("AgentOpen")(db, noteId, out var agent), "AgentOpen(enabled)");
        try { return Resolve<AgentParity_AgentIsEnabled>("AgentIsEnabled")(agent) != 0; }
        finally { Resolve<Search_AgentClose>("AgentClose")(agent); }
    }

    internal bool GetAgentHasRunSinceModified(nint db, uint noteId)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus(Resolve<Parity_NSFNoteOpen>("NSFNoteOpen")(db, noteId, 0, out var note), "NSFNoteOpen(agent state)");
        try
        {
            if (!HasItem(note, "$AssistLastRun") || !HasItem(note, "$AssistVersion")) return false;
            var lastRun = GetItemTime(note, "$AssistLastRun");
            if (lastRun.Innards0 == 0 && lastRun.Innards1 == 0) return false;
            var version = GetItemTime(note, "$AssistVersion");
            return Resolve<AgentParity_TimeDateCollate>("TimeDateCollate")(ref lastRun, ref version) >= 0;
        }
        finally { CloseNote(note); }
    }

    internal void SaveAgentState(nint db, uint noteId, bool? enabled, string? serverName)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus(Resolve<Parity_NSFNoteOpen>("NSFNoteOpen")(db, noteId, 0, out var note), "NSFNoteOpen(agent state)");
        try
        {
            if (enabled is bool enabledValue)
            {
                var flags = HasItem(note, "$AssistFlags") ? GetItemText(note, "$AssistFlags") : string.Empty;
                flags = enabledValue
                    ? (flags.Contains('E') ? flags : flags.Replace("D", string.Empty, StringComparison.Ordinal) + "E")
                    : flags.Replace("E", string.Empty, StringComparison.Ordinal);
                SetItemText(note, "$AssistFlags", flags);
            }
            if (serverName is not null) SetItemText(note, "$MachineName", serverName);
            if (HasItem(note, "$AssistVersion")) SetItemTime(note, "$AssistVersion", CurrentTimeDate());
            CheckStatus(Resolve<Parity_NSFNoteSign>("NSFNoteSign")(note), "NSFNoteSign(agent state)");
            CheckStatus(Resolve<NSFNoteUpdateDelegate>("NSFNoteUpdate")(note, 0), "NSFNoteUpdate(agent state)");
        }
        finally { CloseNote(note); }
    }

    internal string RunAgentByNoteId(nint db, uint noteId, nint documentContext, uint parameterNoteId = 0)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        if (noteId == 0) throw new NotesException("Notes agent note ID cannot be zero.");

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
            if (documentContext != 0)
                CheckStatus(Resolve<Search_AgentSetDocumentContext>("AgentSetDocumentContext")(context, documentContext), "AgentSetDocumentContext");
            if (parameterNoteId != 0)
                CheckStatus(Resolve<AgentParity_SetParamNoteID>("SetParamNoteID")(context, parameterNoteId), "SetParamNoteID");
            CheckStatus(Resolve<Search_AgentRedirectStdout>("AgentRedirectStdout")(context, AgentRedirectMemory), "AgentRedirectStdout(memory)");
            CheckStatus(Resolve<Search_AgentRun>("AgentRun")(agent, context, 0, 0x10), "AgentRun");
            Resolve<Search_AgentQueryStdoutBuffer>("AgentQueryStdoutBuffer")(context, out var outputHandle, out var outputLength);
            if (outputHandle == 0 || outputLength == 0) return string.Empty;
            var pointer = Resolve<Search_OSLockObject>("OSLockObject")(outputHandle);
            if (pointer == 0) return string.Empty;
            try { return NotesAgentOutput.Normalize(FromLmbcs(pointer, checked((int)outputLength))); }
            finally { Resolve<Search_OSUnlockObject>("OSUnlockObject")(outputHandle); }
        }
        finally
        {
            if (context != 0) Resolve<Search_AgentDestroyRunContext>("AgentDestroyRunContext")(context);
            Resolve<Search_AgentClose>("AgentClose")(agent);
        }
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort Search_NIFFindPrivateDesignNote(nint db, nint name, ushort noteClass, out uint noteId);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate int AgentParity_AgentIsEnabled(nint agent);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate int AgentParity_TimeDateCollate(ref NotesTimeDate left, ref NotesTimeDate right);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort AgentParity_SetParamNoteID(nint context, uint noteId);
}
