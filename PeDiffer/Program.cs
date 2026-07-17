using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PeDiffer;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        switch (args.Length)
        {
            case 1:
                Console.Write(Format(Disassembler.Process(args[0], new Disassembler.Options { XArchSyntax = Disassembler.XArchSyntax.Nasm })));
                break;
            case 2:
                break;
        }
        return 0;
    }

    public static string Format(Dictionary<string, string> data)
    {
        return string.Join("\r\n\r\n", data.Select(method => $"{method.Key}\r\n{method.Value}"));
    }
}