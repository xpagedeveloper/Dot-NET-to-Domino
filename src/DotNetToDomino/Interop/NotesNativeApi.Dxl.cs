using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    private const ushort DxlReplaceElseCreate = 6;

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
        var pointer = Marshal.AllocHGlobal(sizeof(ushort));
        try
        {
            Marshal.WriteInt16(pointer, 0);
            CheckStatus(Resolve<Dxl_GetImporterProperty>("DXLGetImporterProperty")(importer, property, pointer), "DXLGetImporterProperty");
            return unchecked((ushort)Marshal.ReadInt16(pointer));
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    internal bool GetDxlImporterBool(uint importer, ushort property)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var pointer = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            Marshal.WriteInt32(pointer, 0);
            CheckStatus(Resolve<Dxl_GetImporterProperty>("DXLGetImporterProperty")(importer, property, pointer), "DXLGetImporterProperty");
            return Marshal.ReadInt32(pointer) != 0;
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    internal void SetDxlImporterWord(uint importer, ushort property, int value)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var pointer = Marshal.AllocHGlobal(sizeof(ushort));
        try
        {
            Marshal.WriteInt16(pointer, unchecked((short)checked((ushort)value)));
            CheckStatus(Resolve<Dxl_SetImporterProperty>("DXLSetImporterProperty")(importer, property, pointer), "DXLSetImporterProperty");
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    internal void SetDxlImporterBool(uint importer, ushort property, bool value)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var pointer = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            Marshal.WriteInt32(pointer, value ? 1 : 0);
            CheckStatus(Resolve<Dxl_SetImporterProperty>("DXLSetImporterProperty")(importer, property, pointer), "DXLSetImporterProperty");
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

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
        CheckStatus(Resolve<Dxl_Import>("DXLImport")(reader, 0, importer, unchecked((uint)database)), "DXLImport");
        GC.KeepAlive(reader);
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
            if (handle == 0) return string.Empty;
            var size = Resolve<Dxl_OSMemoryGetSize>("OSMemoryGetSize")(handle);
            if (size == 0) return string.Empty;
            var pointer = Resolve<Dxl_OSMemoryLock>("OSMemoryLock")(handle);
            if (pointer == 0) return string.Empty;
            try { return FromLmbcsZeroTerminated(pointer, checked((int)Math.Min(size, int.MaxValue))); }
            finally { Resolve<Dxl_OSMemoryUnlock>("OSMemoryUnlock")(handle); }
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
        using var output = new MemoryStream();
        Dxl_XMLWrite writer = (buffer, length, _) => CopyNativeToStream(buffer, length, output);
        CheckStatus(Resolve<Dxl_ExportNote>("DXLExportNote")(exporter, writer, unchecked((uint)note), 0), "DXLExportNote");
        GC.KeepAlive(writer);
        return output.ToArray();
    }

    internal byte[] ExportDxlCollection(uint exporter, nint database, IReadOnlyList<uint> noteIds)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        if (database == 0) throw new InvalidOperationException("DXL export requires an open NotesDatabase.");
        CheckStatus(Resolve<Dxl_IDCreateTable>("IDCreateTable")(4, out var table), "IDCreateTable(DXL)");
        try
        {
            foreach (var noteId in noteIds) CheckStatus(Resolve<Dxl_IDInsert>("IDInsert")(table, noteId, 0), "IDInsert(DXL)");
            using var output = new MemoryStream();
            Dxl_XMLWrite writer = (buffer, length, _) => CopyNativeToStream(buffer, length, output);
            CheckStatus(Resolve<Dxl_ExportIDTable>("DXLExportIDTable")(exporter, writer, unchecked((uint)database), table, 0), "DXLExportIDTable");
            GC.KeepAlive(writer);
            return output.ToArray();
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
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Dxl_Import(Dxl_XMLRead reader, nint action, uint importer, uint database);
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
}
