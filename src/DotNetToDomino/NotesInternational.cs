using System.Globalization;

namespace DotNetToDomino;

/// <summary>
/// Provides the regional and formatting settings used by the current Notes session.
/// </summary>
/// <remarks>
/// The standalone C API does not expose a NotesInternational handle.  Notes uses the
/// process/user regional settings for these values, so this object is a session-owned
/// view of <see cref="CultureInfo.CurrentCulture"/> and is deliberately read-only.
/// </remarks>
public sealed class NotesInternational : NotesObject
{
    private readonly CultureInfo _culture;
    private readonly DateTimeFormatInfo _date;
    private readonly NumberFormatInfo _number;

    internal NotesInternational(NotesSession session)
        : base(session)
    {
        _culture = CultureInfo.CurrentCulture;
        _date = _culture.DateTimeFormat;
        _number = _culture.NumberFormat;
    }

    /// <summary>Gets the culture name used for this session's regional settings.</summary>
    public string Locale => _culture.Name;
    /// <summary>Gets the AM designator.</summary>
    public string AMString => _date.AMDesignator;
    /// <summary>Gets the PM designator.</summary>
    public string PMString => _date.PMDesignator;
    /// <summary>Gets the currency symbol.</summary>
    public string CurrencySymbol => _number.CurrencySymbol;
    /// <summary>Gets the number of digits used by the currency.</summary>
    public int CurrencyDigits => _number.CurrencyDecimalDigits;
    /// <summary>Gets the decimal separator.</summary>
    public string DecimalSeparator => _number.NumberDecimalSeparator;
    /// <summary>Gets the thousands/group separator.</summary>
    public string ThousandsSeparator => _number.NumberGroupSeparator;
    /// <summary>Gets whether the currency symbol precedes the amount.</summary>
    public bool IsCurrencyPrefix => _number.CurrencyPositivePattern is 0 or 2;
    /// <summary>Gets whether the current culture uses daylight saving time.</summary>
    public bool IsDST => TimeZoneInfo.Local.SupportsDaylightSavingTime && TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now);
    /// <summary>Gets the local time-zone offset in hours.</summary>
    public int TimeZone => (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalHours;
    /// <summary>Gets whether the short time pattern uses a 24-hour clock.</summary>
    public bool Is24HourClock => !_date.ShortTimePattern.Contains('h', StringComparison.OrdinalIgnoreCase);
    /// <summary>Gets the short date pattern.</summary>
    public string DateFormat => _date.ShortDatePattern;
    /// <summary>Gets the long date pattern.</summary>
    public string LongDateFormat => _date.LongDatePattern;
    /// <summary>Gets the short time pattern.</summary>
    public string TimeFormat => _date.ShortTimePattern;
    /// <summary>Gets a combined date/time pattern.</summary>
    public string TimeDateFormat => _date.ShortDatePattern + " " + _date.ShortTimePattern;
    /// <summary>Gets the first day of the week (Sunday = 1, matching Notes).</summary>
    public int FirstDayOfWeek => ((int)_date.FirstDayOfWeek) + 1;
    /// <summary>Gets the current weekday number (Sunday = 1, matching Notes).</summary>
    public int Weekday => ((int)DateTime.Today.DayOfWeek) + 1;
    /// <summary>Gets the current month number.</summary>
    public int Month => DateTime.Today.Month;
    /// <summary>Gets the localized name of a month (1-based).</summary>
    public string GetMonthName(int month) => _date.GetMonthName(RequireRange(month, 1, 12, nameof(month)));
    /// <summary>Gets the localized name of a weekday (Sunday = 1).</summary>
    public string GetWeekdayName(int weekday) => _date.GetDayName((DayOfWeek)(RequireRange(weekday, 1, 7, nameof(weekday)) - 1));
    /// <summary>Gets today's localized date.</summary>
    public string Today => DateTime.Today.ToString(_date.ShortDatePattern, _culture);
    /// <summary>Gets tomorrow's localized date.</summary>
    public string Tomorrow => DateTime.Today.AddDays(1).ToString(_date.ShortDatePattern, _culture);
    /// <summary>Gets yesterday's localized date.</summary>
    public string Yesterday => DateTime.Today.AddDays(-1).ToString(_date.ShortDatePattern, _culture);
    /// <summary>Gets the character encoding used for managed text conversion.</summary>
    public string CharacterSet => "LMBCS";

    protected override void ReleaseNative() { }
}
