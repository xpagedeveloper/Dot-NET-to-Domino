# NotesDatabase: properties compared with HCL 14.5

Comparison performed on 2026-10-13 against [HCL NotesDatabase](https://help.hcl-software.com/dom_designer/14.5.0/basic/H_NOTESDATABASE_CLASS.html). The list covers public C# properties, not every overload or behavioral detail.

## Implemented

The implementation includes `ACL`, `Agents`, `AllDocuments`, `Categories`, `Created`, `CurrentAccessLevel`, `DesignTemplateName`, `FileFormat`, `FileName`, `FilePath`, `Forms`, `IsFTIndexed`, `IsOpen`, `LastFTIndexed`, `LastModified`, `Parent`, `PercentUsed`, `ReplicaID`, `Server`, `Size`, `TemplateName`, `Title`, and `Views`.

Presence of a property does not imply complete semantic parity. Several properties require an open database, and local `FilePath` preserves the supplied path.

## Missing properties

`ACLActivityLog`, `Managers`, `MaxSize`, `SizeQuota`, `SizeWarning`, `FTIndexFrequency`, `IsInMultiDbIndexing`, `IsMultiDbSearch`, `ListInDbCatalog`, `Type`, `IsConfigurationDirectory`, `IsDirectoryCatalog`, `IsDesignLockingEnabled`, `IsDocumentLockingEnabled`, `IsInService`, `IsPendingDelete`, `ReplicationInfo`, `EncryptionStrength`, `IsLocallyEncrypted`, `FolderReferencesEnabled`, `LimitRevisions`, `LimitUpdatedBy`, `NotesURL`, and `UndeleteExpireTime` remain unavailable as public properties.

## Intentionally not implemented

`UnprocessedDocuments` requires agent or selection context. `DelayUpdates` controls client-side server batching. `IsClusterReplication` requires Domino cluster status. `HttpURL` cannot be derived safely without HTTP configuration. `IsLink` requires filesystem link metadata, and `LastFixup` has no stable documented C API structure.
