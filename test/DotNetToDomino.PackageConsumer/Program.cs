using DotNetToDomino;

Console.WriteLine(typeof(NotesSession).Assembly.GetName().Name);
Console.WriteLine($"Package version: {System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(NotesSession).Assembly)?.InformationalVersion}");
if (typeof(NotesEmbeddedObject).GetMethod(nameof(NotesEmbeddedObject.ToByteArray), Type.EmptyTypes)?.ReturnType != typeof(byte[]))
    throw new InvalidOperationException("Package API sample failed.");
Console.WriteLine("PASS package API/type-ahead sample");
if (typeof(NotesDocument).GetMethod(nameof(NotesDocument.Sign), Type.EmptyTypes) is null
    || typeof(NotesDocument).GetProperty(nameof(NotesDocument.Signer))?.PropertyType != typeof(string)
    || typeof(NotesDocument).GetProperty(nameof(NotesDocument.Verifier))?.PropertyType != typeof(string)
    || typeof(NotesDocument).GetProperty(nameof(NotesDocument.IsSigned))?.PropertyType != typeof(bool))
    throw new InvalidOperationException("Package document signing surface missing.");
Console.WriteLine("PASS package document signing surface");
