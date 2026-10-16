using DotNetToDomino;

internal static class AclRegressionTests
{
    internal static void Run(NotesSession session, Action<string, Action> check)
    {
        check("ACL editable regression", () =>
        {
            var path = "DotNetToDominoAcl-" + Guid.NewGuid().ToString("N") + ".nsf";
            using var database = session.GetDatabase(" LoCaL ", path, true)!;
            if (database.Server.Length != 0) throw new InvalidOperationException("local server alias was not normalized.");
            using var directory = session.GetDbDirectory("local");
            if (directory.Name.Length != 0) throw new InvalidOperationException("local directory alias was not normalized.");
            database.Create("local");
            void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
            try
            {
                Assert(database.CurrentAccessLevel is >= 0 and <= 6, "Invalid current access level.");
                Assert(database.DesignTemplateName == "", "New database reports a design template.");
                using (var acl = database.ACL)
                {
                    Assert(acl.GetEntry("-Default-") is not null, "Default entry missing.");
                    using var entry = acl.CreateACLEntry("CN=ACL Test/O=Regression", NotesConst.ACLLEVEL_EDITOR);
                    entry.IsPerson = true;
                    entry.CanDeleteDocuments = false;
                    entry.IsPublicReader = true;
                    acl.AddRole("TestRole");
                    entry.EnableRole("[TestRole]");
                    Assert(entry.IsRoleEnabled("TestRole") && entry.Roles.Contains("[TestRole]"), "Role assignment failed.");
                    acl.RenameRole("TestRole", "RenamedRole");
                    Assert(entry.IsRoleEnabled("RenamedRole"), "Role rename lost assignment.");
                    entry.Name = "CN=Renamed ACL Test/O=Regression";
                    Assert(entry.Level == 4 && entry.IsPerson && !entry.CanDeleteDocuments && entry.IsPublicReader && entry.IsRoleEnabled("RenamedRole"), "Entry rename lost access information.");
                    using var notSaved = database.ACL;
                    Assert(notSaved.GetEntry(entry.Name) is null, "ACL changed before Save.");
                    try { entry.Level = 7; throw new InvalidOperationException("Invalid ACL level accepted."); } catch (ArgumentOutOfRangeException) { }
                    acl.Save();
                }
                database.Recycle();
                using var reopened = session.GetDatabase("local", path, false)!;
                using (var acl = reopened.ACL)
                {
                    using var entry = acl.GetEntry("CN=Renamed ACL Test/O=Regression") ?? throw new InvalidOperationException("Saved entry missing.");
                    Assert(entry.Level == 4 && entry.IsPerson && !entry.CanDeleteDocuments && entry.IsPublicReader && entry.IsRoleEnabled("RenamedRole"), "Saved ACL lost level/flags/roles.");
                    var count = 0;
                    for (var current = acl.GetFirstEntry(); current is not null; current = acl.GetNextEntry(current)) count++;
                    Assert(count == acl.Entries.Count, "ACL iteration lost entries.");
                    acl.DeleteRole("RenamedRole");
                    acl.AddRole("Replacement");
                    Assert(!entry.IsRoleEnabled("Replacement"), "Reused role slot leaked privileges.");
                    entry.Remove();
                    Assert(acl.GetEntry("CN=Renamed ACL Test/O=Regression") is null, "Entry removal failed.");
                    acl.Save();
                }
                using var savedAcl = reopened.ACL;
                Assert(savedAcl.GetEntry("CN=Renamed ACL Test/O=Regression") is null && savedAcl.Roles.Contains("[Replacement]"), "ACL removal did not persist.");
            }
            finally
            {
                database.Recycle();
                using var cleanup = session.GetDatabase("", path, false);
                cleanup?.Remove();
            }
        });
    }
}
