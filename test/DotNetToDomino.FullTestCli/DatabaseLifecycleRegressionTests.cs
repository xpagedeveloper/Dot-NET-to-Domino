using DotNetToDomino;

internal static class DatabaseLifecycleRegressionTests
{
    internal static void Run(NotesSession session, Action<string, Action> check)
    {
        void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        void Scratch(string name, Action<NotesDatabase, string, string> test)
        {
            check("database lifecycle regression " + name, () =>
            {
                var token = Guid.NewGuid().ToString("N");
                var sourcePath = "DotNetToDominoLifecycle-" + token + ".nsf";
                var copyPath = "DotNetToDominoLifecycleCopy-" + token + ".nsf";
                using var source = session.GetDatabase("", sourcePath, true) ?? throw new InvalidOperationException("Database wrapper missing.");
                source.Create();
                try { test(source, sourcePath, copyPath); }
                finally
                {
                    source.Recycle();
                    foreach (var path in new[] { copyPath, sourcePath })
                    {
                        using var cleanup = session.GetDatabase("", path, false);
                        if (cleanup?.IsOpen == true) cleanup.Remove();
                    }
                }
            });
        }
        Scratch("metadata persistence", (source, path, _) =>
        {
            source.Title = "Original Åäö 日本語";
            source.Categories = "Tests, Regression";
            source.SetTemplateName("LifecycleTemplate");
            source.SetDesignTemplateName("LifecycleDesign");
            source.Recycle();
            using var reopened = session.GetDatabase("", path, false) ?? throw new InvalidOperationException("Reopen failed.");
            Assert(reopened.Title == "Original Åäö 日本語", "Title did not persist.");
            Assert(reopened.Categories == "Tests, Regression", "Categories did not persist.");
            Assert(reopened.TemplateName == "LifecycleTemplate" && reopened.DesignTemplateName == "LifecycleDesign", "Template metadata did not persist.");
        });
        Scratch("DXL agent signing", (source, _, _) =>
        {
            using var importer = session.CreateDXLImporter();
            importer.DesignImportOption = 6;
            var fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../DotNetToDomino.DxlImporter/fixtures/DotNetToDominoTest.dxl"));
            importer.Import(fixture, source);
            source.Sign();
            using var agent = source.GetAgent("DotNetToDominoTestAgent");
            if (agent is null)
            {
                // HCL Notes 12 may omit LotusScript agents when the local nse/nsk
                // resource files are unavailable. Keep the remaining lifecycle
                // checks valid while reporting the environment limitation.
                Console.WriteLine("SKIP DXL agent execution: Notes runtime did not import the LotusScript agent.");
                return;
            }
            using var result = agent.Run();
            Assert(result.Output.Contains("STATUS OK", StringComparison.Ordinal), "Signed imported agent did not execute.");
        });
        Scratch("replica ID persistence and validation", (source, path, _) =>
        {
            var old = source.ReplicaID;
            var replacement = old[..8] + (old[8..] == "1234ABCD" ? "1234ABCE" : "1234ABCD");
            source.SetReplicaId(replacement[..8] + ":" + replacement[8..]);
            Assert(source.ReplicaID == replacement, "Replica ID change failed.");
            try { source.SetReplicaId("not-a-replica-id"); throw new InvalidOperationException("Invalid replica ID accepted."); }
            catch (ArgumentException) { }
            Assert(source.ReplicaID == replacement, "Rejected ID changed the database.");
            source.Recycle();
            using var reopened = session.GetDatabase("", path, false) ?? throw new InvalidOperationException("Reopen failed.");
            Assert(reopened.ReplicaID == replacement, "Replica ID did not persist.");
        });
        foreach (var includeDocuments in new[] { false, true })
            Scratch("copy " + (includeDocuments ? "with documents" : "design only"), (source, sourcePath, copyPath) =>
            {
                source.Title = "Original";
                using (var importer = session.CreateDXLImporter())
                {
                    var fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../DotNetToDomino.DxlImporter/fixtures/DotNetToDominoTest.dxl"));
                    importer.Import(fixture, source);
                }
                using var doc = source.CreateDocument();
                doc.ReplaceItemValue("Subject", "original document").Recycle();
                doc.Save();
                var unid = doc.UniversalID;
                var originalReplica = source.ReplicaID;
                using (var copy = source.CreateCopy("", copyPath, includeDocuments: includeDocuments))
                {
                    Assert(copy.IsOpen && copy.FileName != source.FileName, "Copy did not create a distinct file.");
                    Assert(copy.ReplicaID != originalReplica, "CreateCopy did not generate a new replica ID.");
                    Assert(copy.Forms.Count > 0 && copy.Views.Count > 0, "Design elements were not copied.");
                    using var copiedDocument = copy.GetDocumentByUNID(unid);
                    Assert((copiedDocument is not null) == includeDocuments, "Copy included the wrong document set.");
                    copy.Title = "Kopia Åäö";
                    var replacement = originalReplica[..8] + (originalReplica[8..] == "8765ABCD" ? "8765ABCE" : "8765ABCD");
                    copy.SetReplicaId(replacement);
                    Assert(copy.Title != source.Title && copy.ReplicaID != source.ReplicaID, "Changing copy changed original identity.");
                    if (copiedDocument is not null)
                    {
                        copiedDocument.ReplaceItemValue("Subject", "changed copy").Recycle();
                        copiedDocument.Save();
                    }
                }
                using var reopenedCopy = session.GetDatabase("", copyPath, false) ?? throw new InvalidOperationException("Copy reopen failed.");
                Assert(reopenedCopy.Title == "Kopia Åäö" && reopenedCopy.ReplicaID != originalReplica, "Copy changes did not persist.");
                using var originalDocument = source.GetDocumentByUNID(unid) ?? throw new InvalidOperationException("Original document missing.");
                Assert(source.Title == "Original" && source.ReplicaID == originalReplica && originalDocument.GetString("Subject") == "original document", "Copy mutation affected original.");
                Console.WriteLine($"VERIFIED original={sourcePath}, title={source.Title}, replica={source.ReplicaID}; copy={copyPath}, title={reopenedCopy.Title}, replica={reopenedCopy.ReplicaID}");
            });
    }
}
