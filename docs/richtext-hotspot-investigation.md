# Hotspot investigation 2026-10-15

## Verified findings

The HCL C API and JNX describe the three variable strings in CDLINK2 and CDLINKEXPORT2 as the comment, server hint, and optional target-document anchor. The third string is not the visible hotspot text.

The implementation treats visible text separately from CDLINK2 metadata. `HotSpotText` reads visible CDTEXT within the surrounding V4 HOTLINK region, while `Anchor` reads the third CDLINK2 string.

## Link updates

JNX `CDLink2.setTexts` changes the variable data on the selected CDLINK2 record. LinkID and the `$Links` list do not need to change for comment, hint, or anchor updates. Visible text changes must preserve surrounding links, anchors, targets, and format records.

## Sources

- [HCL C API: CDLINKEXPORT2](https://opensource.hcltech.sw.com/domino-c-api-docs/reference/Data/CDLINKEXPORT2/)
- [HCL C API: CDHOTSPOTEND](https://opensource.hcltech.sw.com/domino-c-api-docs/reference/Data/CDHOTSPOTEND/)
- [HCL JNX: CDLink2](https://github.com/HCL-TECH-SOFTWARE/domino-jnx/blob/develop/domino-jnx-api/src/main/java/com/hcl/domino/richtext/records/CDLink2.java)
- [HCL JNX: CDLinkExport2](https://github.com/HCL-TECH-SOFTWARE/domino-jnx/blob/develop/domino-jnx-api/src/main/java/com/hcl/domino/richtext/records/CDLinkExport2.java)

## Completed correction and native test

`SetLinkMetadata(comment, serverHint, anchor)` changes only the selected link record's variable data. `SetLinkText` delegates to the same method; its third argument remains the anchor text, not visible text. `SetHotSpotText` changes a single CDTEXT run while preserving its style; multi-run replacements are rejected until every format record can be preserved safely.

Native tests verify Unicode comments and anchors after save/reopen, byte-identical neighboring CD records, unchanged link targets and visible text, and hotspot text style preservation.
