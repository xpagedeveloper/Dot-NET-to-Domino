using DotNetToDomino;

Console.WriteLine(typeof(NotesSession).Assembly.GetName().Name);
if (typeof(NotesEmbeddedObject).GetMethod(nameof(NotesEmbeddedObject.ToByteArray), Type.EmptyTypes)?.ReturnType != typeof(byte[]))
    throw new InvalidOperationException("Package API sample failed.");
Console.WriteLine("PASS package API/type-ahead sample");
