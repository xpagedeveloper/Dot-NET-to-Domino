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
            try
            {
                status = Resolve<Search_NIFFindPrivateDesignNote>("NIFFindPrivateDesignNote")(
                    db, agentName.Pointer, SearchNoteClassFilter, out noteId);
            }
            catch (EntryPointNotFoundException)
            {
                // Older Notes/Domino runtimes may not export the private-design lookup.
                // Preserve the original NIFFindDesignNote status in that case.
            }
        }
        if (status == 0) return noteId;

        var text = TryLoadStatusText(status);
        if (text.Contains("not found", StringComparison.OrdinalIgnoreCase)) return 0;
        CheckStatus(status, "NIFFindDesignNote(agent)");
        return 0;
    }

    internal string RunAgentByNoteId(nint db, uint noteId, nint documentContext)
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

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort Search_NIFFindPrivateDesignNote(nint db, nint name, ushort noteClass, out uint noteId);
}
