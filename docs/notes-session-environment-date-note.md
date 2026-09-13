# DateTime environment values

XPScript routes DateTime environment values through its runtime string conversion rather than invariant numeric conversion. The wrapper retains DateTime as an accepted category; exact locale/date formatting should be covered by a live compatibility test before claiming byte-for-byte date text parity.
