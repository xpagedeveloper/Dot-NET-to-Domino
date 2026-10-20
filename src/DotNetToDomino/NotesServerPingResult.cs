namespace DotNetToDomino;

/// <summary>Result of checking whether a Domino server is reachable.</summary>
public sealed record NotesServerPingResult(string ServerName, bool IsReachable, int Status, uint AvailabilityIndex);
