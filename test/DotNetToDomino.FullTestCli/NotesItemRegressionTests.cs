using DotNetToDomino;

internal static class NotesItemRegressionTests
{
    internal static void Run(NotesSession session, string server, string? path, Action<string, Action> check)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        using var database = session.GetDatabase(server, path, false) ?? throw new InvalidOperationException("Database not found.");
        void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        foreach (var flag in typeof(NotesItem).GetProperties().Where(p => p.CanWrite && p.PropertyType == typeof(bool)))
            check("item surface regression repeated " + flag.Name, () =>
            {
                using var doc = database.CreateDocument();
                using var item = doc.ReplaceItemValue("FlagItem", "Åäö 日本語 café");
                for (var i = 0; i < 20; i++)
                {
                    var enabled = i % 2 == 0;
                    flag.SetValue(item, enabled);
                    Assert(Equals(flag.GetValue(item), enabled), "Flag not retained at iteration " + i);
                    Assert(item.Text == "Åäö 日本語 café", "Flag mutation corrupted Unicode value.");
                    Assert(doc.Items.Count(x => x.Name == "FlagItem") == 1, "Flag mutation duplicated item.");
                }
            });
        check("item surface regression names flag interactions", () =>
        {
            using var doc = database.CreateDocument();
            using var item = doc.ReplaceItemValue("Access", session.UserName);
            item.IsAuthors = true;
            Assert(item.IsAuthors && item.IsNames && item.IsSummary && !item.IsReaders, "Authors dependencies failed.");
            item.IsReaders = true;
            Assert(item.IsReaders && item.IsNames && item.IsSummary && !item.IsAuthors, "Readers dependencies failed.");
            item.IsNames = false;
            Assert(!item.IsReaders && !item.IsAuthors && !item.IsNames, "Clearing names left access flags.");
        });
        foreach (var kind in new[] { "text", "text list", "number", "number list", "date", "date list", "empty" })
            check("item surface regression values copy persistence " + kind, () =>
            {
                using var doc = database.CreateDocument();
                using var destination = database.CreateDocument();
                using var date = session.CreateDateTime("2026-10-12 12:34:56");
                object value = kind switch
                {
                    "text" => "Åäö 日本語 café",
                    "text list" => new[] { "first", "", "Åäö", "first" },
                    "number" => -1234.125,
                    "number list" => new[] { -1.25, 0.0, 123456.5 },
                    "date" => date,
                    "date list" => new[] { date, date },
                    _ => ""
                };
                using var item = doc.ReplaceItemValue("Data", value);
                item.IsSummary = false;
                item.IsProtected = true;
                string[] Snapshot(NotesItem candidate) => candidate.Values.Select(x => x is NotesDateTime dt ? dt.LocalTime : Convert.ToString(x, System.Globalization.CultureInfo.InvariantCulture) ?? "").ToArray();
                var expected = Snapshot(item);
                var expectedType = item.Type;
                Assert(expected.Length == (kind == "text list" ? 4 : kind == "number list" ? 3 : kind == "date list" ? 2 : 1), "Unexpected value count.");
                if (kind == "text") Assert(expected.SequenceEqual(new[] { "Åäö 日本語 café" }), "Unicode text corrupted.");
                if (kind == "text list") Assert(expected.SequenceEqual(new[] { "first", "", "Åäö", "first" }), "Text list entries corrupted.");
                if (kind == "number") Assert(expected.SequenceEqual(new[] { "-1234.125" }), "Number corrupted.");
                if (kind == "number list") Assert(expected.SequenceEqual(new[] { "-1.25", "0", "123456.5" }), "Number list corrupted.");
                if (kind.StartsWith("date")) Assert(expected.All(x => x == date.LocalTime), "Date value corrupted.");
                using var copied = item.CopyToDocument(destination, "Copy");
                Assert(Snapshot(copied).SequenceEqual(expected) && copied.Type == expectedType && copied.IsProtected && !copied.IsSummary, "Copy lost value/type/flags.");
                doc.Save();
                using var reopened = database.GetDocumentByUNID(doc.UniversalID) ?? throw new InvalidOperationException("Saved note not found.");
                try
                {
                    using var persisted = reopened.GetFirstItem("Data") ?? throw new InvalidOperationException("Saved item not found.");
                    Assert(Snapshot(persisted).SequenceEqual(expected) && persisted.Type == expectedType && persisted.IsProtected && !persisted.IsSummary, "Save/reopen lost value/type/flags.");
                    _ = persisted.LastModified.LocalTime;
                }
                finally { reopened.Remove(true); }
                copied.Remove();
                Assert(!destination.HasItem("Copy"), "Remove did not delete copied item.");
            });
        check("item surface regression value transitions preserve flags", () =>
        {
            using var doc = database.CreateDocument();
            using var item = doc.ReplaceItemValue("Changing", "initial");
            using var date = session.CreateDateTime("2026-10-12 12:34:56");
            item.IsSummary = false;
            item.IsProtected = true;
            foreach (var values in new object?[][] { [1.5, -2.25], [date, date], ["Åäö", "", "last"], [], ["final"] })
            {
                item.Values = values;
                Assert(item.IsProtected && !item.IsSummary, "Values setter lost flags.");
                Assert(item.Values.Length == Math.Max(1, values.Length), "Values setter lost entries.");
                Assert(doc.Items.Count(x => x.Name == "Changing") == 1, "Values setter duplicated item.");
            }
            try { item.Values = ["text", 1.0]; throw new InvalidOperationException("Mixed types accepted."); }
            catch (ArgumentException) { }
            Assert(item.Text == "final", "Invalid update changed the old value.");
        });
        check("item surface regression DateTimeValue preserves flags", () =>
        {
            using var doc = database.CreateDocument();
            using var date = session.CreateDateTime("2026-10-12 12:34:56");
            using var item = doc.ReplaceItemValue("Date", date);
            item.IsSummary = false;
            item.IsProtected = true;
            item.DateTimeValue = date;
            Assert(item.IsProtected && !item.IsSummary, "DateTimeValue setter lost flags.");
        });
        check("item surface regression large nonsummary text", () =>
        {
            using var doc = database.CreateDocument();
            using var item = doc.ReplaceItemValue("Large", "");
            item.IsSummary = false;
            var text = new string('x', 40000);
            item.Values = [text];
            item.IsProtected = true;
            Assert(item.Values.Length == 1 && Equals(item.Values[0], text) && !item.IsSummary, "Large nonsummary item corrupted.");
        });
        check("item surface regression richtext flag mutation", () =>
        {
            using var doc = database.CreateDocument();
            using var rich = doc.CreateRichTextItem("Body");
            rich.AppendText("Rich Åäö 日本語");
            var before = rich.Text;
            rich.IsProtected = true;
            rich.IsProtected = false;
            Assert(rich.Text == before && rich.Type == 1 && !rich.IsSummary, "Rich-text flag update corrupted composite data.");
        });
        check("item surface regression rejected list preserves original", () =>
        {
            using var doc = database.CreateDocument();
            using var item = doc.ReplaceItemValue("Original", "keep this");
            try { item.Values = [new string('x', 40000)]; throw new InvalidOperationException("Oversized summary list accepted."); }
            catch (NotesException) { }
            Assert(item.Text == "keep this" && item.IsSummary && doc.Items.Count(x => x.Name == "Original") == 1, "Native rejection lost original item.");
            try { item.Values = [new string('x', 70000)]; throw new InvalidOperationException("Oversized list entry accepted."); }
            catch (ArgumentException) { }
            Assert(item.Text == "keep this", "Validation rejection lost original value.");
        });
        check("item surface regression copy within same document", () =>
        {
            using var doc = database.CreateDocument();
            using var item = doc.ReplaceItemValue("Original", "Åäö original");
            item.IsProtected = true;
            using var alias = item.CopyToDocument(doc, "Alias");
            Assert(alias.Text == item.Text && alias.IsProtected, "Same-document rename copy failed.");
            using var same = item.CopyToDocument(doc);
            Assert(same.Text == "Åäö original" && same.IsProtected, "Self-copy lost original value or flags.");
            Assert(doc.Items.Count(x => x.Name == "Original") == 1, "Self-copy duplicated original.");
        });
        check("item surface regression all flag persistence", () =>
        {
            using var doc = database.CreateDocument();
            using var item = doc.ReplaceItemValue("Flags", "persisted flags");
            item.IsProtected = true;
            item.IsSigned = true;
            item.IsEncrypted = true;
            item.IsSummary = false;
            doc.Save();
            using var reopened = database.GetDocumentByUNID(doc.UniversalID) ?? throw new InvalidOperationException("Saved note missing.");
            try
            {
                using var saved = reopened.GetFirstItem("Flags") ?? throw new InvalidOperationException("Saved item missing.");
                Assert(saved.IsProtected && saved.IsSigned && saved.IsEncrypted && !saved.IsSummary && saved.Text == "persisted flags", "Saving lost item flags or value.");
            }
            finally { reopened.Remove(true); }
        });
    }
}
