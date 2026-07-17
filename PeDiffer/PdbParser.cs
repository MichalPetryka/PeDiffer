using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using TerraFX.Interop.Windows;

[assembly: DisableRuntimeMarshalling]

namespace PeDiffer;

public static unsafe class PdbParser
{
    [DllImport("msdia140")]
    private static extern int DllGetClassObject(Guid* rclsid, Guid* riid, void** ppv);

    public static (List<(string name, (int start, int length) range)> methods, ((ulong start, ulong end) range, string name)[] symbols) ReadSymbols(string pdb)
    {
        using ComPtr<IClassFactory> factory = new();
        Marshal.ThrowExceptionForHR(DllGetClassObject(Windows.__uuidof<DiaSource>(), Windows.__uuidof<IClassFactory>(), (void**)factory.GetAddressOf()));

        using ComPtr<IDiaDataSource> source = new();
        Marshal.ThrowExceptionForHR(factory.Get()->CreateInstance(null, Windows.__uuidof<IDiaDataSource>(), (void**)source.GetAddressOf()));

        fixed (char* ptr = pdb)
            Marshal.ThrowExceptionForHR(source.Get()->loadDataFromPdb(ptr));

        using ComPtr<IDiaSession> session = new();
        Marshal.ThrowExceptionForHR(source.Get()->openSession(session.GetAddressOf()));

        using ComPtr<IDiaTable> table = new();
        using ComPtr<IDiaEnumTables> enumTables = new();
        Marshal.ThrowExceptionForHR(session.Get()->getEnumTables(enumTables.GetAddressOf()));

        using ComPtr<IDiaSymbol> globalScope = new();
        Marshal.ThrowExceptionForHR(session.Get()->get_globalScope(globalScope.GetAddressOf()));

        using ComPtr<IDiaEnumSymbols> publicSymbols = new();
        Marshal.ThrowExceptionForHR(globalScope.Get()->findChildren(SymTagEnum.SymTagPublicSymbol, null, 0, publicSymbols.GetAddressOf()));

        int count;
        Marshal.ThrowExceptionForHR(publicSymbols.Get()->get_Count(&count));

        List<((ulong start, ulong end) range, string name)> symbols = [];
        List<(string name, (int start, int length) range)> methods = [];

        uint fetched;
        using ComPtr<IDiaSymbol> publicSymbol = new();
        while (Windows.SUCCEEDED(publicSymbols.Get()->Next(1, publicSymbol.ReleaseAndGetAddressOf(), &fetched)) && fetched != 0)
        {
            uint rva;
            Marshal.ThrowExceptionForHR(publicSymbol.Get()->get_relativeVirtualAddress(&rva));

            ulong length;
            Marshal.ThrowExceptionForHR(publicSymbol.Get()->get_length(&length));

            using Bstr name = new();
            Marshal.ThrowExceptionForHR(publicSymbol.Get()->get_name(name.GetAddressOf()));

            string symbolName = name.ToString();

            symbols.Add(((rva, rva + length), symbolName));

            BOOL bFunction;
            Marshal.ThrowExceptionForHR(publicSymbol.Get()->get_function(&bFunction));

            if (!bFunction)
                continue;

            methods.Add((symbolName, (checked((int)rva), checked((int)length))));
        }
        return (methods, symbols.ToArray());
    }
}