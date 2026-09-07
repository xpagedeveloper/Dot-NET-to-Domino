using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace DotNetToDomino.Interop;

internal sealed partial class NotesNativeApi
{
    internal string ConvertRichTextToHtml(nint db, nint note, string itemName, bool inlineImages)
    {
        EnsureInitialized();
        using var thread = EnterThread();
        CheckStatus(Resolve<Html_HTMLCreateConverter>("HTMLCreateConverter")(out var converter), "HTMLCreateConverter");
        try
        {
            using var name = ToLmbcs(itemName);
            CheckStatus(Resolve<Html_HTMLConvertItem>("HTMLConvertItem")(converter, db, note, name.Pointer), "HTMLConvertItem");
            var html = ReadHtmlConverterText(converter);
            return inlineImages ? InlineHtmlImages(db, note, itemName, html) : html;
        }
        finally { _ = Resolve<Html_HTMLDestroyConverter>("HTMLDestroyConverter")(converter); }
    }

    private string ReadHtmlConverterText(uint converter)
    {
        uint length = 0;
        CheckStatus(Resolve<Html_HTMLGetProperty>("HTMLGetProperty")(converter, 0, ref length), "HTMLGetProperty(TEXTLENGTH)");
        if (length == 0) return string.Empty;
        if (length > int.MaxValue) throw new NotesException("Converted HTML is too large.");
        var buffer = Marshal.AllocHGlobal(checked((int)length + 1));
        try
        {
            uint requested = length;
            CheckStatus(Resolve<Html_HTMLGetText>("HTMLGetText")(converter, 0, ref requested, buffer), "HTMLGetText");
            return FromLmbcs(buffer, checked((int)requested));
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private string InlineHtmlImages(nint db, nint note, string itemName, string html)
    {
        if (html.Length == 0) return html;
        var escaped = Regex.Escape(itemName);
        var pattern = "(?<url>[^\\\"']*/" + escaped + "/(?<item>[0-9]+)\\.(?<offset>[0-9A-Fa-f]+)\\?OpenElement)";
        return Regex.Replace(html, pattern, match =>
        {
            if (!uint.TryParse(match.Groups["item"].Value, out var itemIndex) ||
                !uint.TryParse(match.Groups["offset"].Value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var offset))
                return match.Value;
            var bytes = ConvertHtmlElement(db, note, itemName, itemIndex, offset);
            if (bytes.Length == 0) return match.Value;
            return "data:" + DetectImageMimeType(bytes) + ";base64," + Convert.ToBase64String(bytes);
        }, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }

    private byte[] ConvertHtmlElement(nint db, nint note, string itemName, uint itemIndex, uint offset)
    {
        CheckStatus(Resolve<Html_HTMLCreateConverter>("HTMLCreateConverter")(out var converter), "HTMLCreateConverter(element)");
        try
        {
            using var name = ToLmbcs(itemName);
            CheckStatus(Resolve<Html_HTMLConvertElement>("HTMLConvertElement")(converter, db, note, name.Pointer, itemIndex, offset), "HTMLConvertElement");
            uint length = 0;
            CheckStatus(Resolve<Html_HTMLGetProperty>("HTMLGetProperty")(converter, 0, ref length), "HTMLGetProperty(element length)");
            if (length == 0) return [];
            if (length > int.MaxValue) throw new NotesException("Converted rich-text image is too large.");
            var buffer = Marshal.AllocHGlobal(checked((int)length));
            try
            {
                uint requested = length;
                CheckStatus(Resolve<Html_HTMLGetText>("HTMLGetText")(converter, 0, ref requested, buffer), "HTMLGetText(element)");
                var bytes = new byte[checked((int)requested)];
                Marshal.Copy(buffer, bytes, 0, bytes.Length);
                return bytes;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        finally { _ = Resolve<Html_HTMLDestroyConverter>("HTMLDestroyConverter")(converter); }
    }

    private static string DetectImageMimeType(byte[] data)
    {
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4e && data[3] == 0x47) return "image/png";
        if (data.Length >= 3 && data[0] == 0xff && data[1] == 0xd8 && data[2] == 0xff) return "image/jpeg";
        if (data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46) return "image/gif";
        if (data.Length >= 2 && data[0] == 0x42 && data[1] == 0x4d) return "image/bmp";
        return "application/octet-stream";
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Html_HTMLCreateConverter(out uint converter);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Html_HTMLDestroyConverter(uint converter);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Html_HTMLConvertItem(uint converter, nint db, nint note, nint itemName);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Html_HTMLConvertElement(uint converter, nint db, nint note, nint itemName, uint itemIndex, uint offset);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Html_HTMLGetProperty(uint converter, uint property, ref uint value);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate ushort Html_HTMLGetText(uint converter, uint startingOffset, ref uint textLength, nint text);
}
