using DotNetToDomino.Interop;

namespace DotNetToDomino;

/// <summary>Represents one column definition in a Notes view.</summary>
public sealed class NotesViewColumn : NotesObject
{
    private readonly NotesViewColumnData _data;

    internal NotesViewColumn(NotesSession session, NotesView parent, NotesViewColumnData data) : base(session)
    {
        Parent = parent;
        _data = data;
    }

    public NotesView Parent { get; }
    public int Position { get { EnsureAlive(); return _data.Position; } }
    public string ItemName { get { EnsureAlive(); return _data.ItemName; } }
    public string Title { get { EnsureAlive(); return _data.Title; } }
    public string Formula { get { EnsureAlive(); return _data.Formula; } }
    public int Width { get { EnsureAlive(); return _data.DisplayWidth; } }

    public int FontFace { get { EnsureAlive(); return _data.FontFace; } }
    public int FontColor { get { EnsureAlive(); return _data.FontColor; } }
    public int FontPointSize { get { EnsureAlive(); return _data.FontPointSize; } }
    public int FontStyle { get { EnsureAlive(); return _data.FontAttributes; } }
    public bool IsFontBold { get { EnsureAlive(); return (_data.FontAttributes & 0x01) != 0; } }
    public bool IsFontItalic { get { EnsureAlive(); return (_data.FontAttributes & 0x02) != 0; } }
    public bool IsFontUnderline { get { EnsureAlive(); return (_data.FontAttributes & 0x04) != 0; } }
    public bool IsFontStrikethrough { get { EnsureAlive(); return (_data.FontAttributes & 0x08) != 0; } }

    public int NumberDigits { get { EnsureAlive(); return _data.NumberDigits; } }
    public int NumberFormat { get { EnsureAlive(); return _data.NumberFormat; } }
    public int NumberAttrib { get { EnsureAlive(); return _data.NumberAttributes; } }
    public bool IsNumberAttribPunctuated { get { EnsureAlive(); return (_data.NumberAttributes & 0x01) != 0; } }
    public bool IsNumberAttribPercent { get { EnsureAlive(); return (_data.NumberAttributes & 0x02) != 0; } }
    public bool IsNumberAttribParens { get { EnsureAlive(); return (_data.NumberAttributes & 0x04) != 0; } }

    public int DateFmt { get { EnsureAlive(); return _data.DateFormat; } }
    public int TimeFmt { get { EnsureAlive(); return _data.TimeFormat; } }
    public int TimeZoneFmt { get { EnsureAlive(); return _data.TimeZoneFormat; } }
    public int TimeDateFmt { get { EnsureAlive(); return _data.TimeDateFormat; } }
    public int ListSep { get { EnsureAlive(); return _data.ListSep; } }

    public bool IsSorted { get { EnsureAlive(); return HasFlag(0); } }
    public bool IsCategory { get { EnsureAlive(); return HasFlag(1); } }
    public bool IsSortDescending { get { EnsureAlive(); return HasFlag(2); } }
    public bool IsHidden { get { EnsureAlive(); return HasFlag(3); } }
    public bool IsResponse { get { EnsureAlive(); return HasFlag(4); } }
    public bool IsHideDetail { get { EnsureAlive(); return HasFlag(5); } }
    public bool IsIcon { get { EnsureAlive(); return HasFlag(6); } }
    public bool IsResize { get { EnsureAlive(); return !HasFlag(7); } }
    public bool IsResortAscending { get { EnsureAlive(); return HasFlag(8); } }
    public bool IsResortDescending { get { EnsureAlive(); return HasFlag(9); } }
    public bool IsShowTwistie { get { EnsureAlive(); return HasFlag(10); } }
    public bool IsResortToView { get { EnsureAlive(); return HasFlag(11); } }
    public bool IsSecondaryResort { get { EnsureAlive(); return HasFlag(12); } }
    public bool IsSecondaryResortDescending { get { EnsureAlive(); return HasFlag(13); } }
    public bool IsCaseSensitiveSort { get { EnsureAlive(); return !HasFlag(14); } }
    public bool IsAccentSensitiveSort { get { EnsureAlive(); return !HasFlag(15); } }

    public bool IsField
    {
        get
        {
            EnsureAlive();
            return _data.Formula.Length == 0 || string.Equals(_data.Formula.Trim(), _data.ItemName, StringComparison.OrdinalIgnoreCase);
        }
    }
    public bool IsFormula { get { EnsureAlive(); return !IsField; } }
    public bool IsValidDominoQueryColumn { get { EnsureAlive(); return _data.ItemName.Length > 0; } }
    public bool IsValidDominoQueryField { get { EnsureAlive(); return IsField && _data.ItemName.Length > 0; } }

    private bool HasFlag(int bit) => (_data.Flags1 & (1 << bit)) != 0;
    protected override void ReleaseNative() { }
}
