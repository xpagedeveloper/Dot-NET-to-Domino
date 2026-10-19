# Sign Notes documents in C#

`NotesDocument.Sign()` signs a document with the Notes ID used to authenticate the session. Call `Save()` afterward to persist the signature.

```csharp
using var document = database.CreateDocument();
document.ReplaceItemValue("Subject", "Signed example").Recycle();
document.Sign();
document.Save();
```

`IsSigned` indicates that the document contains `$Signature`; it does not by itself prove that the signature is valid. `Signer` returns the signer name from native verification. `Verifier` returns the certificate or certifier that verified the signature.

Change document content before calling `Sign()`. After content changes, sign the document again. To verify persisted changes, close and reopen the document.

## Lifetime and recycle

All `NotesObject` wrappers implement `IDisposable`. `using var` calls `Dispose()` automatically, including when an exception is thrown. `Dispose()` calls `Recycle()` and is idempotent. Use `using` for the code that owns an object and do not recycle a wrapper while other code still uses it.

## Verification

Native regression tests verify signer and verifier names, persistence after save/reopen, invalidation after content changes, and signed/encrypted content after decryption.

References: [HCL Sign](https://www.help.hcl-software.com/dom_designer/9.0.1/reference/r_domino_Document_sign.html), [Verifier](https://help.hcl-software.com/dom_designer/12.0.2/basic/H_VERIFIER_PROPERTY.html), and [NSFNoteVerifySignature](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Func/NSFNoteVerifySignature/).
