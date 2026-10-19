# Rich text: table records and verification

`NotesRichTextItem.AppendTable(rows, columns)` supports 1–255 rows and columns. Dimensions outside that range are rejected before the native call. `NotesRichTextTable.RemoveRow` validates the complete row range before rewriting CD records.

## Record layout

The HCL C API and HCL JNX define BSIG as two bytes: a signature followed by the record's total length. Table records use BSIG rather than WSIG.

| Record | Length | Important offsets |
| --- | ---: | --- |
| CDTABLEBEGIN | 14 | LeftMargin 2, inter-cell spacing 4/6/8/10, Flags 12 |
| CDTABLECELL | 18 | Row 2, Column 3, LeftMargin 4, RightMargin 6, FractionalWidth 8, Border 10, Flags 11, v42Border 12, RowSpan 14, ColumnSpan 15, BackgroundColor 16 |
| CDTABLEEND | 6 | Four zeroed reserved bytes after BSIG |

`CDTABLECELL_USE_BKGCOLOR` is `0x01`; `CDTABLECELL_USE_V42BORDERS` is `0x02`.

## Verification

The native test suite verifies table dimensions, cell metadata, paragraph styles, default fonts, pass-through HTML, links, attachments, copying, ranges, sections, tabbed tables, list types, and compacting after save/reopen. Rich-text objects are position-bound; reacquire navigator objects after an item rewrite.

## References

- [HCL C API: BSIG](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Data/BSIG/)
- [HCL Designer: RichTextItem Compact](https://help.hcl-software.com/dom_designer/10.0.1/reference/r_domino_RichTextItem_compact.html)
- [HCL C API: CDTABLECELL](https://opensource.hcltechsw.com/domino-c-api-docs/reference/Data/CDTABLECELL/)
- [HCL JNX: BSIG](https://github.com/HCL-TECH-SOFTWARE/domino-jnx/blob/develop/domino-jnx-api/src/main/java/com/hcl/domino/richtext/structures/BSIG.java)
