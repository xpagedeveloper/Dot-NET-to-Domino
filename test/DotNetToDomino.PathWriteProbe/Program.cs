using System.Text;

var notesData = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("NOTES_DATA");
if (string.IsNullOrWhiteSpace(notesData))
{
    Console.Error.WriteLine("Usage: DotNetToDomino.PathWriteProbe <notes-data-directory>");
    return 2;
}

var targets = new[]
{
    (Name: "TEMP", Path: Path.GetTempPath()),
    (Name: "Notes Data", Path: notesData)
};
var failed = 0;
foreach (var target in targets)
{
    var file = Path.Combine(target.Path, $"dotnet-to-domino-write-probe-{Guid.NewGuid():N}.tmp");
    try
    {
        Directory.CreateDirectory(target.Path);
        var expected = $"probe:{target.Name}:{Guid.NewGuid():N}";
        await File.WriteAllTextAsync(file, expected, Encoding.UTF8);
        var actual = await File.ReadAllTextAsync(file, Encoding.UTF8);
        if (!string.Equals(expected, actual, StringComparison.Ordinal)) throw new IOException("Read-back content did not match.");
        Console.WriteLine($"PASS {target.Name}: write/read {file}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {target.Name}: {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
        try
        {
            if (File.Exists(file))
            {
                File.Delete(file);
                Console.WriteLine($"  Deleted: {file}");
            }
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine($"FAIL {target.Name}: cleanup: {ex.Message}");
        }
    }
}
return failed == 0 ? 0 : 1;
