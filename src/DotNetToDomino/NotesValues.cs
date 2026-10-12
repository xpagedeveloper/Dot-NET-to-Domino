using System.Globalization;
using DotNetToDomino.Interop;

namespace DotNetToDomino;

public sealed class NotesName : NotesObject
{
    private readonly Dictionary<string, string> _parts = new(StringComparer.OrdinalIgnoreCase);
    internal NotesName(NotesSession session, string value) : base(session)
    {
        Source = (value ?? string.Empty).Trim();
        Canonical = session.Api.CanonicalizeName(Source);
        Abbreviated = session.Api.AbbreviateName(Canonical);
        ParseCanonical(Canonical);
        ParseInternet(Source);
    }
    public NotesSession Parent { get { EnsureAlive(); return Session; } }
    public string Original => Source;
    public string Source { get; }
    public string Canonical { get; }
    public string Abbreviated { get; }
    public bool IsHierarchical => Canonical.Contains('=');
    public string Common => Part("CN");
    public string Country => Part("C");
    public string Organization => Part("O");
    public string OrgUnit1 => Part("OU1");
    public string OrgUnit2 => Part("OU2");
    public string OrgUnit3 => Part("OU3");
    public string OrgUnit4 => Part("OU4");
    public string ADMD => Part("A");
    public string PRMD => Part("P");
    public string Addr821 => Part("ADDR821");
    public string Addr822LocalPart => Part("LOCALPART");
    public string Addr822Phrase => Part("PHRASE");
    public string Addr822Comment1 => Part("COMMENT1");
    public string Addr822Comment2 => Part("COMMENT2");
    public string Addr822Comment3 => Part("COMMENT3");
    public string Generation => Part("G");
    public string Given => Part("GIVEN");
    public string Initials => Part("INITIALS");
    public string Surname => Part("SURNAME");
    public string Keyword => Part("KEYWORD");
    public string Language => Part("LANGUAGE");
    private string Part(string key) { EnsureAlive(); return _parts.TryGetValue(key, out var value) ? value : string.Empty; }
    private void ParseCanonical(string value)
    {
        var ou = 0;
        foreach (var raw in value.Split('/'))
        {
            var part = raw.Trim();
            var separator = part.IndexOf('=');
            if (separator <= 0) continue;
            var key = part[..separator].Trim().ToUpperInvariant();
            var text = part[(separator + 1)..].Trim();
            if (key == "OU") { ou++; if (ou <= 4) _parts["OU" + ou.ToString(CultureInfo.InvariantCulture)] = text; }
            else _parts[key] = text;
        }
    }
    private void ParseInternet(string source)
    {
        var at = source.LastIndexOf('@');
        if (at <= 0 || at >= source.Length - 1) return;
        var before = source[..at].Trim();
        var lt = before.LastIndexOf('<');
        var gt = source.IndexOf('>', lt + 1);
        if (lt >= 0 && gt > lt)
        {
            _parts["PHRASE"] = before[..lt].Trim().Trim('"');
            var address = source[(lt + 1)..gt].Trim();
            _parts["ADDR821"] = address;
            _parts["LOCALPART"] = address.Split('@')[0];
        }
        else
        {
            var end = source.IndexOfAny([' ', '\t', '(']);
            var address = (end < 0 ? source : source[..end]).Trim();
            _parts["ADDR821"] = address;
            _parts["LOCALPART"] = address.Split('@')[0];
        }

        var comments = System.Text.RegularExpressions.Regex.Matches(source[(at + 1)..], @"\(([^)]*)\)");
        for (var index = 0; index < comments.Count && index < 3; index++)
            _parts["COMMENT" + (index + 1).ToString(CultureInfo.InvariantCulture)] = comments[index].Groups[1].Value.Trim();
    }
    protected override void ReleaseNative() => _parts.Clear();
}

public sealed class NotesDateTime : NotesObject
{
    private NotesTimeDate _value;
    internal NotesDateTime(NotesSession session, string value) : base(session)
    {
        _value = string.IsNullOrWhiteSpace(value) ? session.Api.TimeDateWildcard() : session.Api.ParseTimeDate(value);
    }
    internal NotesDateTime(NotesSession session, DateTimeOffset value) : base(session) =>
        _value = session.Api.ParseTimeDate(value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
    private NotesDateTime(NotesSession session, NotesTimeDate value) : base(session) => _value = value;
    internal static NotesDateTime FromNative(NotesSession session, NotesTimeDate value) => new(session, value);
    internal NotesTimeDate NativeValue { get { EnsureAlive(); return _value; } }
    public NotesSession Parent => Session;
    public bool IsValidDate { get { EnsureAlive(); return !IsEmpty(); } }
    public bool IsDST { get { EnsureAlive(); return !IsEmpty() && !IsAnyDate() && !IsAnyTime() && Session.Api.ExpandTimeDate(_value).Dst != 0; } }
    public int TimeZone { get { EnsureAlive(); return IsEmpty() || IsAnyDate() || IsAnyTime() ? 0 : Session.Api.ExpandTimeDate(_value).Zone; } }
    public string Text => LocalTime;
    public string LocalTime { get { EnsureAlive(); return Session.Api.FormatTimeDate(_value); } }
    public string GMTTime { get { EnsureAlive(); return IsEmpty() ? string.Empty : IsAnyDate() || IsAnyTime() ? LocalTime : FormatExpanded(Session.Api.ExpandTimeDateGmt(_value)); } }
    public string ZoneTime { get { EnsureAlive(); return IsEmpty() ? string.Empty : IsAnyDate() || IsAnyTime() ? LocalTime : FormatExpanded(Session.Api.ExpandTimeDate(_value)); } }
    public string DateOnly { get { EnsureAlive(); if (IsEmpty()) return string.Empty; if (IsAnyDate()) return "AnyDay"; var v = Session.Api.ExpandTimeDate(_value); return $"{v.Year:D4}-{v.Month:D2}-{v.Day:D2}"; } }
    public string TimeOnly { get { EnsureAlive(); if (IsEmpty()) return string.Empty; if (IsAnyTime()) return "AllDay"; var v = Session.Api.ExpandTimeDate(_value); return $"{v.Hour:D2}:{v.Minute:D2}:{v.Second:D2}"; } }
    public DateTimeOffset? Value
    {
        get
        {
            EnsureAlive();
            if (IsEmpty() || IsAnyDate() || IsAnyTime()) return null;
            var v = Session.Api.ExpandTimeDate(_value);
            try { return new DateTimeOffset(v.Year, v.Month, v.Day, v.Hour, v.Minute, v.Second, TimeSpan.Zero); }
            catch { return null; }
        }
    }
    public void SetNow()
    {
        EnsureAlive();
        _value = Session.Api.CurrentTimeDate();
    }
    public void SetAnyDate()
    {
        EnsureAlive();
        var wildcard = Session.Api.TimeDateWildcard();
        _value.Innards1 = (_value.Innards1 & 0xFF000000u) | (wildcard.Innards1 & 0x00FFFFFFu);
    }
    public void SetAnyTime()
    {
        EnsureAlive();
        _value.Innards0 = Session.Api.TimeDateWildcard().Innards0;
    }
    public double TimeDifference(NotesDateTime other)
    {
        EnsureAlive();
        ArgumentNullException.ThrowIfNull(other);
        other.EnsureAlive();
        var left = Session.Api.ExpandTimeDateGmt(_value);
        var right = Session.Api.ExpandTimeDateGmt(other._value);
        if (left.Year < 1 || right.Year < 1 || left.Month < 1 || right.Month < 1)
            throw new InvalidOperationException("TimeDifference cannot be used with wildcard NotesDateTime values.");
        var leftUtc = new DateTime(left.Year, left.Month, left.Day, left.Hour, left.Minute, left.Second, DateTimeKind.Utc);
        var rightUtc = new DateTime(right.Year, right.Month, right.Day, right.Hour, right.Minute, right.Second, DateTimeKind.Utc);
        return (leftUtc - rightUtc).TotalSeconds;
    }
    public double TimeDifferenceDouble(NotesDateTime other) => TimeDifference(other);
    public void ConvertToZone(int zone)
    {
        EnsureAlive();
        Session.Api.ConvertTimeDateToZone(ref _value, zone);
    }
    public void AdjustSecond(int amount) => Adjust(amount, 0, 0, 0, 0, 0);
    public void AdjustMinute(int amount) => Adjust(0, amount, 0, 0, 0, 0);
    public void AdjustHour(int amount) => Adjust(0, 0, amount, 0, 0, 0);
    public void AdjustDay(int amount) => Adjust(0, 0, 0, amount, 0, 0);
    public void AdjustMonth(int amount) => Adjust(0, 0, 0, 0, amount, 0);
    public void AdjustYear(int amount) => Adjust(0, 0, 0, 0, 0, amount);
    private void Adjust(int seconds, int minutes, int hours, int days, int months, int years)
    {
        EnsureAlive();
        Session.Api.AdjustTimeDate(ref _value, seconds, minutes, hours, days, months, years);
    }
    private static string FormatExpanded(NotesExpandedTime v) => $"{v.Year:D4}-{v.Month:D2}-{v.Day:D2} {v.Hour:D2}:{v.Minute:D2}:{v.Second:D2}";
    private bool IsAnyDate() { var wildcard = Session.Api.TimeDateWildcard(); return (_value.Innards1 & 0x00FFFFFFu) == (wildcard.Innards1 & 0x00FFFFFFu); }
    private bool IsAnyTime() => _value.Innards0 == Session.Api.TimeDateWildcard().Innards0;
    private bool IsWildcard() => IsAnyDate() && IsAnyTime();
    private bool IsEmpty() => _value.Innards0 == 0 && _value.Innards1 == 0;
    protected override void ReleaseNative() => _value = default;
}
