namespace DotNetToDomino;

/// <summary>Builds and sends a mail memo through the Notes/Domino mail path.</summary>
public sealed class NotesMail : NotesObject
{
    private string[] _sendTo = [];
    private string[] _copyTo = [];
    private string[] _blindCopyTo = [];
    private string _from;
    private string _replyTo = string.Empty;
    private string _principal = string.Empty;
    private string _subject = string.Empty;
    private string _body = string.Empty;
    private string _deliveryPriority = "N";
    private string _deliveryReport = "N";
    private bool _returnReceipt;
    private readonly List<string> _attachments = [];
    private Action<NotesRichTextItem>? _richTextComposer;

    internal NotesMail(NotesSession session) : base(session) => _from = session.UserName;

    /// <summary>Primary recipients.</summary>
    public IReadOnlyList<string> SendTo { get { EnsureAlive(); return _sendTo; } set => _sendTo = NormalizeRecipients(value, nameof(SendTo), false); }
    /// <summary>Carbon-copy recipients.</summary>
    public IReadOnlyList<string> CopyTo { get { EnsureAlive(); return _copyTo; } set => _copyTo = NormalizeRecipients(value, nameof(CopyTo), true); }
    /// <summary>Blind-carbon-copy recipients.</summary>
    public IReadOnlyList<string> BlindCopyTo { get { EnsureAlive(); return _blindCopyTo; } set => _blindCopyTo = NormalizeRecipients(value, nameof(BlindCopyTo), true); }
    /// <summary>Sender address. Defaults to the session user.</summary>
    public string From { get { EnsureAlive(); return _from; } set { EnsureAlive(); _from = (value ?? string.Empty).Trim(); } }
    /// <summary>Reply-to address.</summary>
    public string ReplyTo { get { EnsureAlive(); return _replyTo; } set { EnsureAlive(); _replyTo = (value ?? string.Empty).Trim(); } }
    /// <summary>Principal used for delegated mail.</summary>
    public string Principal { get { EnsureAlive(); return _principal; } set { EnsureAlive(); _principal = (value ?? string.Empty).Trim(); } }
    /// <summary>Message subject.</summary>
    public string Subject { get { EnsureAlive(); return _subject; } set { EnsureAlive(); _subject = value ?? string.Empty; } }
    /// <summary>Plain-text message body.</summary>
    public string Body { get { EnsureAlive(); return _body; } set => SetBodyText(value); }
    /// <summary>Normalized delivery priority: H, N, or L.</summary>
    public string DeliveryPriority { get { EnsureAlive(); return _deliveryPriority; } set { EnsureAlive(); _deliveryPriority = NormalizePriority(value); } }
    /// <summary>Normalized delivery report: N, B, or C.</summary>
    public string DeliveryReport { get { EnsureAlive(); return _deliveryReport; } set { EnsureAlive(); _deliveryReport = NormalizeReport(value); } }
    /// <summary>Whether a return receipt is requested.</summary>
    public bool ReturnReceipt { get { EnsureAlive(); return _returnReceipt; } set { EnsureAlive(); _returnReceipt = value; } }
    /// <summary>Whether this mail has MIME content. MIME composition is not implemented yet.</summary>
    public bool IsMIME { get { EnsureAlive(); return false; } }
    /// <summary>Number of rich-text file attachments.</summary>
    public int AttachmentCount { get { EnsureAlive(); return _attachments.Count; } }

    /// <summary>Adds a file attachment to the rich-text mail body.</summary>
    public void AddAttachment(string sourcePath)
    {
        EnsureAlive();
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Mail attachment was not found.", sourcePath);
        if (_attachments.Any(path => string.Equals(path, sourcePath, StringComparison.OrdinalIgnoreCase))) return;
        _attachments.Add(sourcePath);
    }

    /// <summary>Sets the plain-text body and clears other body modes.</summary>
    public void SetBodyText(string? text) { EnsureAlive(); _richTextComposer = null; _body = text ?? string.Empty; }

    /// <summary>Configures a rich-text body. The callback runs against the new Body item during Send.</summary>
    public void SetBodyRichText(Action<NotesRichTextItem> compose)
    {
        EnsureAlive();
        ArgumentNullException.ThrowIfNull(compose);
        _richTextComposer = compose;
        _body = string.Empty;
    }

    /// <summary>Sends the memo through the configured Notes mail database.</summary>
    public void Send()
    {
        EnsureAlive();
        if (_sendTo.Length + _copyTo.Length + _blindCopyTo.Length == 0)
            throw new NotesException(5, "NotesMail requires at least one recipient in SendTo, CopyTo, or BlindCopyTo.");

        var server = Session.GetEnvironmentString("MailServer").Trim();
        var file = Session.GetEnvironmentString("MailFile").Trim();
        using var database = OpenMailDatabase(server, file);
        using var document = database.CreateDocument();
        document.ReplaceItemValue("Form", "Memo");
        document.ReplaceItemValue("SendTo", _sendTo);
        if (_copyTo.Length > 0) document.ReplaceItemValue("CopyTo", _copyTo);
        if (_blindCopyTo.Length > 0) document.ReplaceItemValue("BlindCopyTo", _blindCopyTo);
        if (_from.Length > 0) document.ReplaceItemValue("From", _from);
        if (_replyTo.Length > 0) document.ReplaceItemValue("ReplyTo", _replyTo);
        if (_principal.Length > 0) document.ReplaceItemValue("Principal", _principal);
        if (_subject.Length > 0) document.ReplaceItemValue("Subject", _subject);
        if (_richTextComposer is null && _attachments.Count == 0)
        {
            document.ReplaceItemValue("Body", _body);
        }
        else
        {
            using var richText = document.CreateRichTextItem("Body");
            if (_richTextComposer is not null) _richTextComposer(richText);
            else richText.AppendText(_body);
            foreach (var attachment in _attachments) richText.AttachFile(attachment);
        }
        document.ReplaceItemValue("DeliveryPriority", _deliveryPriority);
        document.ReplaceItemValue("DeliveryReport", _deliveryReport);
        document.ReplaceItemValue("ReturnReceipt", _returnReceipt ? "1" : "0");
        using var composed = Session.CreateDateTimeNow();
        document.ReplaceItemValue("ComposedDate", composed);
        document.Send();
    }

    protected override void ReleaseNative() { }

    private NotesDatabase OpenMailDatabase(string server, string file)
    {
        if (file.Length > 0)
        {
            var database = Session.OpenDatabase(server, file);
            if (database.IsOpen) return database;
            database.Recycle();
            if (server.Length > 0)
            {
                database = Session.OpenDatabase(string.Empty, file);
                if (database.IsOpen) return database;
                database.Recycle();
            }
        }

        var names = Session.OpenDatabase(string.Empty, "names.nsf");
        if (names.IsOpen) return names;
        names.Recycle();
        var mailBox = Session.OpenDatabase(server, "mail.box");
        if (mailBox.IsOpen) return mailBox;
        mailBox.Recycle();
        throw new NotesException(91, "NotesMail could not open a temporary Notes database. Configure MailFile/MailServer or make names.nsf available.");
    }

    private static string[] NormalizeRecipients(IEnumerable<string>? values, string property, bool allowEmpty)
    {
        if (values is null && !allowEmpty) throw new ArgumentNullException(property, $"NotesMail.{property} cannot be null.");
        var result = (values ?? []).Select(value => value is null ? throw new ArgumentNullException(property, $"NotesMail.{property} cannot contain null recipients.") : value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (!allowEmpty && result.Length == 0) throw new ArgumentException($"NotesMail.{property} cannot be empty.", property);
        return result;
    }

    private static string NormalizePriority(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "H" or "HIGH" => "H", "L" or "LOW" => "L", "N" or "NORMAL" or "" => "N",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "NotesMail.DeliveryPriority must be High/H, Normal/N, or Low/L.")
    };

    private static string NormalizeReport(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "N" or "NONE" or "0" or "" => "N", "B" or "FAILURE" or "FAILUREONLY" or "1" => "B", "C" or "CONFIRM" or "CONFIRMDELIVERY" or "2" => "C",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "NotesMail.DeliveryReport must be None/N, Failure/B, or Confirm/C.")
    };
}
