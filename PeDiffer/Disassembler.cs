using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using AsmArm64;

using Iced.Intel;

using Decoder = Iced.Intel.Decoder;

#pragma warning disable CS8500

namespace PeDiffer;

internal static class Disassembler
{
    public class Options
    {
        public XArchSyntax XArchSyntax { get; init; }
    }

    public static Dictionary<string, string> Process(string path, Options options)
    {
        string pdb = Path.ChangeExtension(path, "pdb");
        return File.Exists(pdb) ? Process(path, pdb, options) : throw new FileNotFoundException();
    }

    public static unsafe Dictionary<string, string> Process(string binary, string pdb, Options options)
    {
        PeSymbol[] symbols = PdbParser.ReadSymbols(pdb);

        symbols.Sort((first, second) => first.Start.CompareTo(second.Start));

        using DataModule module = new(binary);
        SymbolResolver resolver = new(symbols, (ulong)module.Data);

        ReadOnlySpan<byte> bytes = module.Span;

        Dictionary<string, string> results = new();
        foreach (PeSymbol symbol in symbols)
        {
            if (!symbol.Function)
                continue;

            ReadOnlySpan<byte> range = bytes.Slice(checked((int)symbol.Start), checked((int)symbol.Length));
            results.Add(symbol.Name, GetMethod(range, resolver, module.Architecture, options));
        }

        return results;
    }

    public static string GetMethod(ReadOnlySpan<byte> bytes, SymbolResolver? resolver, Architecture architecture, Options options)
    {
        return architecture switch
        {
            Architecture.X86 => GetMethodXArch(bytes, resolver, false, options),
            Architecture.X64 => GetMethodXArch(bytes, resolver, true, options),
            Architecture.Arm64 => GetMethodArm64(bytes, resolver, options),
            _ => throw new NotSupportedException($"{architecture} is not supported!")
        };
    }

    private static string GetMethodArm64(ReadOnlySpan<byte> bytes, SymbolResolver? resolver, Options options)
    {
        Arm64DisassemblerOptions disassemblerOptions = new();
        resolver?.Configure(disassemblerOptions);
        return new Arm64Disassembler(disassemblerOptions).Disassemble(bytes).Trim();
    }

    private static unsafe string GetMethodXArch(ReadOnlySpan<byte> bytes, SymbolResolver? resolver, bool is64, Options options)
    {
        Decoder decoder = Decoder.Create(is64 ? 64 : 32, new SpanReader(&bytes));
        decoder.IP = 0;

        Formatter formatter = options.XArchSyntax switch
        {
            XArchSyntax.Masm => new MasmFormatter(resolver),
            XArchSyntax.Nasm => new NasmFormatter(resolver),
            XArchSyntax.Gas => new GasFormatter(resolver),
            XArchSyntax.Intel => new IntelFormatter(resolver),
            _ => throw new ArgumentOutOfRangeException(nameof(options.XArchSyntax), options.XArchSyntax, null)
        };
        formatter.Options.RipRelativeAddresses = true;

        StringBuilder combined = new();
        StringOutput output = new();

        ulong endRip = decoder.IP + (uint)bytes.Length;
        while (decoder.IP < endRip)
        {
            formatter.Format(decoder.Decode(), output);
            combined.AppendLine(output.ToStringAndReset());
        }

        return combined.ToString().Trim();
    }

    public enum XArchSyntax
    {
        Masm,
        Nasm,
        Gas,
        Intel
    }

    private sealed unsafe class SpanReader(ReadOnlySpan<byte>* buffer) : CodeReader
    {
        public override int ReadByte()
        {
            ReadOnlySpan<byte> bytes = *buffer;
            if (bytes.Length == 0)
                return -1;
            byte value = bytes[0];
            *buffer = bytes[1..];
            return value;
        }
    }
}
