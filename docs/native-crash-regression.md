# Native crash regression audit

The native test failure was reproduced against HCL Notes 12.0.2 x64. XPScript was inspected read-only, including the final runtime emitted by `NotesRuntimeSourceBuilder.Build()`, rather than only the source templates.

Corrections:

- Cache native delegates by export name and delegate type, preventing casts between distinct declarations for the same export.
- Use a 32-bit pool in BLOCKID and 16-bit HCOLLECTION handles, matching the generated XPScript runtime.
- Bind OSMemGetSize with its output-size pointer, as specified in the HCL C API reference. This deliberately corrects the one-argument declaration also found in the reference runtime.
- Remove children from recycle queues before invoking child cleanup. Cleanup uses owner fields rather than public getters that reject already recycled objects.
- Use INDEXPOSITION `0x4000`, SUMMARYVALUES `0x2000`, and MIME_INCLUDE_HEADERS `0x10`, matching the final generated reference runtime.
- Itemize MIME on an unsaved temporary note and copy Body and file items by BLOCKID, matching XPScript's final JNX-derived writer.
- Resolve relative ID-file paths against the Notes data directory, and resolve an explicit relative notes.ini before loading the runtime changes the working directory.
- Handle empty and wildcard date/time properties without invoking native conversions on missing values.
- Replace item flags with an append-before-delete operation: Notes 12.0.2 retained old flags when NSFItemModifyValue received unchanged bytes. Regression tests verify all seven writable flags, value preservation and absence of duplicate items.
- Preserve item flags in DateTimeValue updates. Write text lists using their actual flags and the native LIST layout, avoiding temporary summary items and preserving the original on a failed append.
- Use a structured DXL agent with a background-compatible trigger; the richer fixture also includes a view, table, section and document link.

Regression coverage includes typed delegate caching, native BLOCKID layout, already recycled children, native ACL access, agent output, view navigation, rich text attachments, MIME roots/children, property round trips, and a real-instance property sweep.

The property sweep reports its actual coverage. Passing it does not imply every method overload, live mail delivery, remote server operation, ID configuration, or runtime architecture has been exercised.

The expanded NotesItem regression suite passes 25 cases on Notes 12.0.2 x64: repeated flag toggles, authors/readers/names dependencies, Unicode and duplicate/empty list entries, text/numeric/date values and lists, copying and save/reopen persistence, type transitions, DateTimeValue flag preservation, 40 KB nonsummary lists, rich-text flag mutation, original-value preservation after rejected writes, self-copy, and all-flag persistence. Test-only native calls also verify signatures after save/reopen, reject tampered signed content after save/reopen, and encrypt/decrypt signed data using the authenticated ID's public key. The signature is inside the sealed data and is verified after decryption.

On 2026-10-13 all 19 isolated native groups pass, including four new database lifecycle cases. These verify metadata and replica-ID persistence, design-only/full copies, independent file names/titles/replica IDs, and independent document edits. See [database operation examples](database-operations.sv.md). From alpha.6 the public NotesDocument API includes Sign(), IsSigned, Signer and Verifier. The test harness uses public Sign() and signer/verifier properties and calls native encryption and independent signature verification directly; encryption is not exposed as a public document method.

Authoritative references:

- [OSMemGetSize](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Func/OSMemGetSize/)
- [LIST](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Data/LIST/)
- [NSFItemAppend](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Func/NSFItemAppend/)
- [NIFReadEntries](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Func/NIFReadEntries/)
- [MIMEStreamOpen](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Func/MIMEStreamOpen/)
- [MIMEStreamItemize](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Func/MIMEStreamItemize/)
- [HCL JNX ReadMask constants](https://github.com/HCL-TECH-SOFTWARE/domino-jnx/blob/develop/domino-jnx-commons/src/main/java/com/hcl/domino/commons/views/ReadMask.java)
