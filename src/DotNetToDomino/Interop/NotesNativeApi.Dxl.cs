using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    private const ushort DxlReplaceElseCreate = 6;
    private const ushort DxlNoteClassForm = 0x0004;
    private const ushort DxlNoteClassView = 0x0008;
    private const ushort DxlNoteClassIcon = 0x0010;
    private const ushort DxlNoteClassDesign = 0x0020;
    private const ushort DxlNoteClassHelpIndex = 0x0080;
    private const ushort DxlNoteClassHelp = 0x0100;
    private const ushort DxlNoteClassFilter = 0x0200;
    private const ushort DxlNoteClassField = 0x0400;
    private const ushort DxlNoteClassReplFormula = 0x0800;
    private const ushort DxlAllDesignNoteClasses = DxlNoteClassForm | DxlNoteClassView | DxlNoteClassIcon | DxlNoteClassDesign | DxlNoteClassHelpIndex | DxlNoteClassHelp | DxlNoteClassFilter | DxlNoteClassField | DxlNoteClassReplFormula;

    internal uint CreateDxlImporter()
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus(Resolve<Dxl_CreateImporter>("DXLCreateImporter")(out var handle), "DXLCreateImporter");
        try
        {
            SetDxlImporterWord(handle, 2, DxlReplaceElseCreate);
            SetDxlImporterWord(handle, 3, DxlReplaceElseCreate);
            SetDxlImporterBool(handle, 7, false);
            return handle;
        }
        catch
        {
            Resolve<Dxl_DeleteImporter>("DXLDeleteImporter")(handle);
            throw;
        }
    }

    internal void DeleteDxlImporter(uint handle)
    {
        if (handle == 0 || !IsInitialized) return;
        using var thread = EnterThread();
        Resolve<Dxl_DeleteImporter>("DXLDeleteImporter")(handle);
    }

    internal int GetDxlImporterWord(uint importer, ushort property)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var pointer = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            Marshal.WriteInt32(pointer, 0);
            CheckStatus(Resolve<Dxl_GetImporterProperty>("DXLGetImporterProperty")(importer, property, pointer), "DXLGetImporterProperty");
            return checked((ushort)Marshal.ReadInt32(pointer));
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    internal int GetDxlImporterInt(uint importer, ushort property)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var pointer = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            Marshal.WriteInt32(pointer, 0);
            CheckStatus(Resolve<Dxl_GetImporterProperty>("DXLGetImporterProperty")(importer, property, pointer), "DXLGetImporterProperty");
            return Marshal.ReadInt32(pointer);
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    internal bool GetDxlImporterBool(uint importer, ushort property) => GetDxlImporterInt(importer, property) != 0;

    internal void SetDxlImporterWord(uint importer, ushort property, int value) => SetDxlImporterInt(importer, property, checked((ushort)value));

    internal void SetDxlImporterInt(uint importer, ushort property, int value)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var pointer = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            Marshal.WriteInt32(pointer, value);
            CheckStatus(Resolve<Dxl_SetImporterProperty>("DXLSetImporterProperty")(importer, property, pointer), "DXLSetImporterProperty");
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    internal void SetDxlImporterBool(uint importer, ushort property, bool value) => SetDxlImporterInt(importer, property, value ? 1 : 0);

    internal void ImportDxl(uint importer, Stream stream, nint database)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        ArgumentNullException.ThrowIfNull(stream);
        if (database == 0) throw new InvalidOperationException("DXL import requires an open NotesDatabase.");
        Dxl_XMLRead reader = (buffer, length, _) =>
        {
            if (buffer == 0 || length == 0) return 0;
            var requested = checked((int)Math.Min(length, 1024u * 1024u));
            var managed = new byte[requested];
            var read = stream.Read(managed, 0, requested);
            if (read > 0) Marshal.Copy(managed, 0, buffer, read);
            return checked((uint)read);
        };
        CheckStatus(Resolve<Dxl_Import>("DXLImport")(importer, reader, unchecked((uint)database), 0), "DXLImport");
        GC.KeepAlive(reader);
    }

    internal void ImportDxlFile(uint importer, string filePath, nint database)
    {
        filePath = Path.GetFullPath(filePath);
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        ImportDxl(importer, stream, database);
    }

    internal string GetDxlImporterLog(uint importer)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var value = Marshal.AllocHGlobal(sizeof(uint));
        try
        {
            Marshal.WriteInt32(value, 0);
            CheckStatus(Resolve<Dxl_GetImporterProperty>("DXLGetImporterProperty")(importer, 11, value), "DXLGetImporterProperty(iResultLog)");
            var handle = unchecked((uint)Marshal.ReadInt32(value));
            return ReadDxlMemoryString(handle);
        }
        finally { Marshal.FreeHGlobal(value); }
    }

    internal int GetDxlImporterNoteCount(uint importer)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var value = Marshal.AllocHGlobal(sizeof(uint));
        try
        {
            Marshal.WriteInt32(value, 0);
            CheckStatus(Resolve<Dxl_GetImporterProperty>("DXLGetImporterProperty")(importer, 12, value), "DXLGetImporterProperty(iImportedNoteList)");
            var table = unchecked((uint)Marshal.ReadInt32(value));
            return table == 0 ? 0 : checked((int)Resolve<Dxl_IDEntries>("IDEntries")(table));
        }
        finally { Marshal.FreeHGlobal(value); }
    }

    internal uint CreateDxlExporter()
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus(Resolve<Dxl_CreateExporter>("DXLCreateExporter")(out var handle), "DXLCreateExporter");
        return handle;
    }

    internal void DeleteDxlExporter(uint handle)
    {
        if (handle == 0 || !IsInitialized) return;
        using var thread = EnterThread();
        Resolve<Dxl_DeleteExporter>("DXLDeleteExporter")(handle);
    }

    internal int GetDxlExporterInt(uint exporter, ushort property)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var pointer = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            Marshal.WriteInt32(pointer, 0);
            CheckStatus(Resolve<Dxl_GetExporterProperty>("DXLGetExporterProperty")(exporter, property, pointer), "DXLGetExporterProperty");
            return Marshal.ReadInt32(pointer);
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    internal bool GetDxlExporterBool(uint exporter, ushort property) => GetDxlExporterInt(exporter, property) != 0;

    internal void SetDxlExporterInt(uint exporter, ushort property, int value)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var pointer = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            Marshal.WriteInt32(pointer, value);
            CheckStatus(Resolve<Dxl_SetExporterProperty>("DXLSetExporterProperty")(exporter, property, pointer), "DXLSetExporterProperty");
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    internal void SetDxlExporterBool(uint exporter, ushort property, bool value) => SetDxlExporterInt(exporter, property, value ? 1 : 0);

    internal byte[] ExportDxlDocument(uint exporter, nint note)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        if (note == 0) throw new InvalidOperationException("DXL export requires an open NotesDocument.");
        return CaptureDxl(writer => CheckStatus(Resolve<Dxl_ExportNote>("DXLExportNote")(exporter, writer, unchecked((uint)note), 0), "DXLExportNote"));
    }

    internal byte[] ExportDxlCollection(uint exporter, nint database, IReadOnlyList<uint> noteIds)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        if (database == 0) throw new InvalidOperationException("DXL export requires an open NotesDatabase.");
        return WithDxlIdTable(noteIds, table => CaptureDxl(writer =>
            CheckStatus(Resolve<Dxl_ExportIDTable>("DXLExportIDTable")(exporter, writer, unchecked((uint)database), table, 0), "DXLExportIDTable")));
    }

    internal byte[] ExportDxlDesign(uint exporter, nint database)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        if (database == 0) throw new InvalidOperationException("DXL export requires an open NotesDatabase.");
        var noteIds = SearchByNoteClass(database, DxlAllDesignNoteClasses);
        return ExportDxlCollection(exporter, database, noteIds);
    }

    internal byte[] ExportDxlDesignElement(uint exporter, nint database, string name, string designType)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        if (database == 0) throw new InvalidOperationException("DXL export requires an open NotesDatabase.");
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var noteClass = DxlDesignTypeToNoteClass(designType);
        using var designName = ToLmbcs(name.Trim());
        CheckStatus(Resolve<Dxl_NIFFindDesignNote>("NIFFindDesignNote")(database, designName.Pointer, noteClass, out var noteId), $"NIFFindDesignNote({designType})");
        var note = TryOpenNote(database, noteId);
        if (note == 0) throw new NotesException($"Design element '{name}' could not be opened.");
        try { return ExportDxlDocument(exporter, note); }
        finally { CloseNote(note); }
    }

    internal void WriteDxlBytes(string filePath, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            try { Directory.CreateDirectory(directory); }
            catch (IOException ex) { throw new IOException($"The DXL output directory '{directory}' could not be created.", ex); }
        try { File.WriteAllBytes(fullPath, bytes); }
        catch (UnauthorizedAccessException ex) { throw new UnauthorizedAccessException($"Access was denied when writing DXL file '{fullPath}'.", ex); }
        catch (IOException ex) { throw new IOException($"The DXL file '{fullPath}' could not be written.", ex); }
    }

    private ushort DxlDesignTypeToNoteClass(string designType)
    {
        designType = designType?.Trim() ?? string.Empty;
        if (designType.Equals("Form", StringComparison.OrdinalIgnoreCase) || designType.Equals("Subform", StringComparison.OrdinalIgnoreCase) || designType.Equals("Page", StringComparison.OrdinalIgnoreCase)) return DxlNoteClassForm;
        if (designType.Equals("View", StringComparison.OrdinalIgnoreCase) || designType.Equals("Folder", StringComparison.OrdinalIgnoreCase)) return DxlNoteClassView;
        if (designType.Equals("Agent", StringComparison.OrdinalIgnoreCase) || designType.Equals("Filter", StringComparison.OrdinalIgnoreCase)) return DxlNoteClassFilter;
        if (designType.Equals("Field", StringComparison.OrdinalIgnoreCase)) return DxlNoteClassField;
        if (designType.Equals("Icon", StringComparison.OrdinalIgnoreCase)) return DxlNoteClassIcon;
        if (designType.Equals("Help", StringComparison.OrdinalIgnoreCase)) return DxlNoteClassHelp;
        if (designType.Equals("HelpIndex", StringComparison.OrdinalIgnoreCase)) return DxlNoteClassHelpIndex;
        if (designType.Equals("ReplicationFormula", StringComparison.OrdinalIgnoreCase)) return DxlNoteClassReplFormula;
        if (designType.Equals("DesignCollection", StringComparison.OrdinalIgnoreCase)) return DxlNoteClassDesign;
        throw new ArgumentException("Unsupported design element type: " + designType + ".", nameof(designType));
    }

    private string ReadDxlMemoryString(uint handle)
    {
        if (handle == 0) return string.Empty;
        var size = Resolve<Dxl_OSMemoryGetSize>("OSMemoryGetSize")(handle);
        if (size == 0) return string.Empty;
        var pointer = Resolve<Dxl_OSMemoryLock>("OSMemoryLock")(handle);
        if (pointer == 0) return string.Empty;
        try { return FromLmbcsZeroTerminated(pointer, checked((int)Math.Min(size, int.MaxValue))); }
        finally { Resolve<Dxl_OSMemoryUnlock>("OSMemoryUnlock")(handle); }
    }

    private byte[] CaptureDxl(Action<Dxl_XMLWrite> export)
    {
        using var output = new MemoryStream();
        Dxl_XMLWrite writer = (buffer, length, _) => CopyNativeToStream(buffer, length, output);
        export(writer);
        GC.KeepAlive(writer);
        return output.ToArray();
    }

    private T WithDxlIdTable<T>(IReadOnlyList<uint> noteIds, Func<uint, T> action)
    {
        CheckStatus(Resolve<Dxl_IDCreateTable>("IDCreateTable")(4, out var table), "IDCreateTable(DXL)");
        try
        {
            foreach (var noteId in noteIds) CheckStatus(Resolve<Dxl_IDInsert>("IDInsert")(table, noteId, 0), "IDInsert(DXL)");
            return action(table);
        }
        finally { _ = Resolve<Dxl_IDDestroyTable>("IDDestroyTable")(table); }
    }

    private static void CopyNativeToStream(nint buffer, uint length, Stream output)
    {
        if (buffer == 0 || length == 0) return;
        var remaining = length;
        var offset = 0;
        while (remaining > 0)
        {
            var block = checked((int)Math.Min(remaining, 1024u * 1024u));
            var managed = new byte[block];
            Marshal.Copy(nint.Add(buffer, offset), managed, 0, block);
            output.Write(managed, 0, block);
            offset += block;
            remaining -= checked((uint)block);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Dxl_CreateImporter(out uint importer);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Dxl_DeleteImporter(uint importer);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Dxl_GetImporterProperty(uint importer, ushort property, nint value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Dxl_SetImporterProperty(uint importer, ushort property, nint value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate uint Dxl_XMLRead(nint buffer, uint length, nint action);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Dxl_Import(uint importer, Dxl_XMLRead reader, uint database, nint action);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate uint Dxl_OSMemoryGetSize(uint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate nint Dxl_OSMemoryLock(uint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Dxl_OSMemoryUnlock(uint handle);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate uint Dxl_IDEntries(uint table);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Dxl_CreateExporter(out uint exporter);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Dxl_DeleteExporter(uint exporter);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Dxl_GetExporterProperty(uint exporter, ushort property, nint value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Dxl_SetExporterProperty(uint exporter, ushort property, nint value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Dxl_XMLWrite(nint buffer, uint length, nint action);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Dxl_ExportNote(uint exporter, Dxl_XMLWrite writer, uint note, nint action);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Dxl_ExportIDTable(uint exporter, Dxl_XMLWrite writer, uint database, uint idTable, nint action);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Dxl_IDCreateTable(ushort alignment, out uint table);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Dxl_IDInsert(uint table, uint id, nint inserted);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Dxl_IDDestroyTable(uint table);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Dxl_NIFFindDesignNote(nint db, nint name, ushort noteClass, out uint noteId);
}
