using System.Runtime.InteropServices;

namespace DotNetToDomino.Interop;

internal readonly record struct NotesComputeWithFormResult(bool Success, string[] FailedFields);

internal readonly record struct NotesModifiedNoteResult(
    uint[] NoteIds,
    uint[] DocumentNoteIds,
    uint[] DesignNoteIds,
    NotesTimeDate UntilTime);

internal sealed partial class NotesNativeApi
{
    private const ushort ComputeWithFormValidationFailedStatus = 0x038d;
    private const ushort ComputeWithFormNextFieldAction = 2;
    private const int ComputeWithFormCdFieldFixedLength = 36;
    private const int ComputeWithFormDvLengthOffset = 22;
    private const int ComputeWithFormItLengthOffset = 24;
    private const int ComputeWithFormIvLengthOffset = 28;
    private const int ComputeWithFormNameLengthOffset = 30;

    internal ushort GetNoteClass(nint note)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var value = Marshal.AllocHGlobal(sizeof(ushort));
        try
        {
            Marshal.WriteInt16(value, 0);
            Resolve<Parity_NSFNoteGetInfo>("NSFNoteGetInfo")(note, 3, value);
            return unchecked((ushort)Marshal.ReadInt16(value));
        }
        finally { Marshal.FreeHGlobal(value); }
    }

    internal bool ComputeDocumentWithForm(nint note)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var status = Resolve<Parity_NSFNoteComputeWithForm>("NSFNoteComputeWithForm")(note, 0, 0, null, 0);
        if (status == 0) return true;
        if (status == ComputeWithFormValidationFailedStatus) return false;
        CheckStatus(status, "NSFNoteComputeWithForm");
        return false;
    }

    internal NotesComputeWithFormResult ComputeDocumentWithFormAndCollectErrors(nint note)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var validationFailed = false;
        var failedFields = new List<string>();
        var seenFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Parity_ComputeWithFormErrorProc callback = (field, _, _, _, _, _) =>
        {
            validationFailed = true;
            var fieldName = GetComputeWithFormFieldName(field);
            if (fieldName.Length > 0 && seenFields.Add(fieldName)) failedFields.Add(fieldName);
            return ComputeWithFormNextFieldAction;
        };

        var status = Resolve<Parity_NSFNoteComputeWithForm>("NSFNoteComputeWithForm")(note, 0, 0, callback, 0);
        GC.KeepAlive(callback);
        if (status != 0 && status != ComputeWithFormValidationFailedStatus)
            CheckStatus(status, "NSFNoteComputeWithForm");

        return new NotesComputeWithFormResult(
            !validationFailed && status != ComputeWithFormValidationFailedStatus,
            failedFields.ToArray());
    }

    internal NotesModifiedNoteResult GetModifiedNoteIds(nint database, ushort noteClassMask, NotesTimeDate? since)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var start = since ?? GetMinimumTimeDate();
        var all = ReadModifiedNoteTable(database, noteClassMask, start, out var until);

        const ushort documentClass = 0x0001;
        const ushort designClasses = 0x0FBE;
        var documentMask = (ushort)(noteClassMask & documentClass);
        var designMask = (ushort)(noteClassMask & designClasses);

        var documents = documentMask == 0
            ? Array.Empty<uint>()
            : IntersectModifiedIds(all, ReadModifiedNoteTable(database, documentMask, start, out _));
        var design = designMask == 0
            ? Array.Empty<uint>()
            : IntersectModifiedIds(all, ReadModifiedNoteTable(database, designMask, start, out _));

        return new NotesModifiedNoteResult(all, documents, design, until);
    }

    internal string[] GetViewColumnNames(nint database, string viewName)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        ArgumentException.ThrowIfNullOrWhiteSpace(viewName);
        using var name = ToLmbcs(viewName.Trim());
        CheckStatus(Resolve<Parity_NIFFindDesignNote>("NIFFindDesignNote")(database, name.Pointer, 0x0008, out var viewNoteId), "NIFFindDesignNote(view)");
        var note = TryOpenNote(database, viewNoteId);
        if (note == 0) return [];
        try
        {
            var info = GetItemMetadata(note, "$VIEWFORMAT");
            if (info.DataType != 0x0005) throw new NotesException("NotesView $VIEWFORMAT has an unexpected data type.");
            return ParseViewColumnNames(CopyItemValueWithoutType(info));
        }
        finally { CloseNote(note); }
    }

    internal void SaveAgent(nint database, uint noteId)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus(Resolve<Parity_NSFNoteOpen>("NSFNoteOpen")(database, noteId, 0, out var note), "NSFNoteOpen(agent)");
        try
        {
            CheckStatus(Resolve<Parity_NSFNoteSign>("NSFNoteSign")(note), "NSFNoteSign(agent)");
            CheckStatus(Resolve<NSFNoteUpdateDelegate>("NSFNoteUpdate")(note, 0), "NSFNoteUpdate(agent)");
        }
        finally { CloseNote(note); }
    }

    internal string GetDxlExporterLog(uint exporter)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        var value = Marshal.AllocHGlobal(sizeof(uint));
        try
        {
            Marshal.WriteInt32(value, 0);
            CheckStatus(Resolve<Dxl_GetExporterProperty>("DXLGetExporterProperty")(exporter, 1, value), "DXLGetExporterProperty(eDxlExportResultLog)");
            return ReadDxlMemoryString(unchecked((uint)Marshal.ReadInt32(value)));
        }
        finally { Marshal.FreeHGlobal(value); }
    }

    private string GetComputeWithFormFieldName(nint field)
    {
        if (field == 0) return string.Empty;
        var dvLength = unchecked((ushort)Marshal.ReadInt16(field, ComputeWithFormDvLengthOffset));
        var itLength = unchecked((ushort)Marshal.ReadInt16(field, ComputeWithFormItLengthOffset));
        var ivLength = unchecked((ushort)Marshal.ReadInt16(field, ComputeWithFormIvLengthOffset));
        var nameLength = unchecked((ushort)Marshal.ReadInt16(field, ComputeWithFormNameLengthOffset));
        if (nameLength == 0) return string.Empty;
        var name = nint.Add(field, ComputeWithFormCdFieldFixedLength + dvLength + itLength + ivLength);
        return FromLmbcs(name, nameLength);
    }

    private NotesTimeDate GetMinimumTimeDate()
    {
        Resolve<Parity_TimeConstant>("TimeConstant")(0, out var value);
        return value;
    }

    private uint[] ReadModifiedNoteTable(nint database, ushort noteClassMask, NotesTimeDate since, out NotesTimeDate until)
    {
        var status = Resolve<Parity_NSFDbGetModifiedNoteTable>("NSFDbGetModifiedNoteTable")(
            database, noteClassMask, since, out until, out var table);
        if (status != 0)
        {
            var message = TryLoadStatusText(status);
            if (message.Contains("No documents have been modified", StringComparison.OrdinalIgnoreCase)) return [];
            CheckStatus(status, "NSFDbGetModifiedNoteTable");
        }
        if (table == 0) return [];
        try
        {
            var ids = new List<uint>();
            uint id = 0;
            var first = 1;
            while (Resolve<Doc_IDScan>("IDScan")(table, first, ref id) != 0)
            {
                first = 0;
                if (id != 0) ids.Add(id);
            }
            return ids.Distinct().ToArray();
        }
        finally { _ = Resolve<Doc_IDDestroyTable>("IDDestroyTable")(table); }
    }

    private static uint[] IntersectModifiedIds(uint[] all, uint[] subset)
    {
        if (all.Length == 0 || subset.Length == 0) return [];
        var selected = new HashSet<uint>(subset);
        return all.Where(selected.Contains).ToArray();
    }

    private string[] ParseViewColumnNames(byte[] data)
    {
        const int tableFormatSize = 10;
        const int columnFormatSize = 32;
        const ushort columnSignature = 17238;
        if (data.Length < tableFormatSize) throw new NotesException("Invalid Notes view format data.");
        var count = ReadCanonicalUInt16(data, 2);
        if (count == 0) return [];
        var descriptorBytes = checked(count * columnFormatSize);
        if (data.Length < tableFormatSize + descriptorBytes) throw new NotesException("Truncated Notes view column format data.");
        var itemNameSizes = new ushort[count];
        var titleSizes = new ushort[count];
        var formulaSizes = new ushort[count];
        var constantSizes = new ushort[count];
        for (var i = 0; i < count; i++)
        {
            var offset = tableFormatSize + i * columnFormatSize;
            if (ReadCanonicalUInt16(data, offset) != columnSignature) throw new NotesException("Invalid Notes view column format signature.");
            itemNameSizes[i] = ReadCanonicalUInt16(data, offset + 4);
            titleSizes[i] = ReadCanonicalUInt16(data, offset + 6);
            formulaSizes[i] = ReadCanonicalUInt16(data, offset + 8);
            constantSizes[i] = ReadCanonicalUInt16(data, offset + 10);
        }
        var cursor = tableFormatSize + descriptorBytes;
        var result = new string[count];
        for (var i = 0; i < count; i++)
        {
            var itemNameSize = itemNameSizes[i];
            var packedSize = checked((int)itemNameSize + titleSizes[i] + formulaSizes[i] + constantSizes[i]);
            if (cursor + packedSize > data.Length) throw new NotesException("Truncated Notes view column name data.");
            result[i] = DecodeLmbcs(data, cursor, itemNameSize);
            cursor += packedSize;
        }
        return result;
    }

    private string DecodeLmbcs(byte[] data, int offset, int length)
    {
        if (length == 0) return string.Empty;
        var pointer = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.Copy(data, offset, pointer, length);
            return FromLmbcs(pointer, length);
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    private static ushort ReadCanonicalUInt16(byte[] data, int offset) => (ushort)(data[offset] | (data[offset + 1] << 8));

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void Parity_NSFNoteGetInfo(nint note, ushort noteMember, nint value);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort Parity_ComputeWithFormErrorProc(nint field, ushort phase, ushort error, uint errorText, ushort errorTextSize, nint context);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort Parity_NSFNoteComputeWithForm(nint note, nint formNote, uint flags, Parity_ComputeWithFormErrorProc? errorRoutine, nint context);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort Parity_NSFDbGetModifiedNoteTable(nint database, ushort noteClassMask, NotesTimeDate since, out NotesTimeDate until, out uint table);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void Parity_TimeConstant(ushort constantType, out NotesTimeDate value);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort Parity_NIFFindDesignNote(nint database, nint name, ushort noteClass, out uint noteId);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort Parity_NSFNoteOpen(nint database, uint noteId, ushort flags, out nint note);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort Parity_NSFNoteSign(nint note);
}
