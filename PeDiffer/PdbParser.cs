using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using TerraFX.Interop.Windows;

[assembly: DisableRuntimeMarshalling]

namespace PeDiffer;

public static unsafe class PdbParser
{
    [DllImport("msdia140")]
    private static extern int DllGetClassObject(Guid* rclsid, Guid* riid, void** ppv);

    public static PeSymbol[] ReadSymbols(string pdb)
    {
        using ComPtr<IClassFactory> factory = new();
        Marshal.ThrowExceptionForHR(DllGetClassObject(Windows.__uuidof<DiaSource>(), Windows.__uuidof<IClassFactory>(), (void**)factory.GetAddressOf()));

        using ComPtr<IDiaDataSource> source = new();
        Marshal.ThrowExceptionForHR(factory.Get()->CreateInstance(null, Windows.__uuidof<IDiaDataSource>(), (void**)source.GetAddressOf()));

        fixed (char* ptr = Path.GetFullPath(pdb))
            Marshal.ThrowExceptionForHR(source.Get()->loadDataFromPdb(ptr));

        using ComPtr<IDiaSession> session = new();
        Marshal.ThrowExceptionForHR(source.Get()->openSession(session.GetAddressOf()));

        using ComPtr<IDiaSymbol> globalScope = new();
        Marshal.ThrowExceptionForHR(session.Get()->get_globalScope(globalScope.GetAddressOf()));

        using ComPtr<IDiaEnumSymbols> diaSymbols = new();
        Marshal.ThrowExceptionForHR(globalScope.Get()->findChildrenEx(SymTagEnum.SymTagNull, null, 0, diaSymbols.GetAddressOf()));

        List<PeSymbol> symbols = [];

        uint fetched;
        using ComPtr<IDiaSymbol> publicSymbol = new();
        while (Windows.SUCCEEDED(diaSymbols.Get()->Next(1, publicSymbol.ReleaseAndGetAddressOf(), &fetched)) && fetched != 0)
        {
            uint rva;
            Marshal.ThrowExceptionForHR(publicSymbol.Get()->get_relativeVirtualAddress(&rva));

            ulong length;
            Marshal.ThrowExceptionForHR(publicSymbol.Get()->get_length(&length));

            using Bstr name = new();
            Marshal.ThrowExceptionForHR(publicSymbol.Get()->get_name(name.GetAddressOf()));

            BOOL bFunction;
            Marshal.ThrowExceptionForHR(publicSymbol.Get()->get_function(&bFunction));

            symbols.Add(new PeSymbol(name.ToString(), rva, length, bFunction));
        }

        return [.. symbols];
    }
}