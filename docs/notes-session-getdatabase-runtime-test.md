# GetDatabase runtime checks

With a disposable Notes/Domino test environment, verify:

1. an existing NSF returns an open `NotesDatabase`;
2. a missing NSF with `createOnFail: true` returns a closed wrapper with the requested server/file location;
3. the same missing NSF with `createOnFail: false` returns null;
4. empty server/file values return a closed wrapper without attempting an invalid native open.
