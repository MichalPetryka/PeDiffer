namespace PeDiffer;

public readonly record struct PeSymbol(string Name, uint Start, ulong Length, bool Function);
