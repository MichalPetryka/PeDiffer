using System;

using AsmArm64;

using Iced.Intel;

namespace PeDiffer;

internal sealed class SymbolResolver(PeSymbol[] symbols, ulong baseAddress) : ISymbolResolver
{
    public ulong BaseAddress { get; } = baseAddress;

    private (ulong start, string label) Resolve(ulong address)
    {
        int index = symbols.BinarySearch(new AddressComparable(address));
        if (index < 0)
        {
            return default;
        }

        PeSymbol symbol = symbols[index];
        return (symbol.Start, symbol.Name);
    }

    public bool TryGetSymbol(in Instruction instruction, int operand, int instructionOperand,
        ulong address, int addressSize, out SymbolResult symbol)
    {
        (ulong start, string label) = Resolve(address);
        symbol = new SymbolResult(start, label);
        return label is not null;
    }

    internal void Configure(Arm64DisassemblerOptions options)
    {
        options.BaseAddress = (long)BaseAddress;
        options.TryFormatLabel = TryFormatLabel;

        bool TryFormatLabel(long offset, Span<char> destination, out int charsWritten)
        {
            string label = Resolve((ulong)offset).label;
            if (label is null)
            {
                charsWritten = 0;
                return false;
            }

            ReadOnlySpan<char> truncated = label.AsSpan(0, Math.Min(label.Length, destination.Length));
            truncated.CopyTo(destination);
            charsWritten = truncated.Length;
            return true;
        }
    }

    private readonly struct AddressComparable(ulong address) : IComparable<PeSymbol>
    {
        public int CompareTo(PeSymbol symbol)
        {
            if (address >= symbol.Start && address < symbol.Start + symbol.Length)
                return 0;

            return address.CompareTo(symbol.Start);
        }
    }
}
