using System;
using System.Runtime.InteropServices;

using TerraFX.Interop.Windows;

namespace PeDiffer;

internal static class PeHelpers
{
    public static unsafe (Architecture Architecture, uint Size) GetPeImageInfo(ReadOnlySpan<byte> file)
    {
        IMAGE_DOS_HEADER dos = MemoryMarshal.Read<IMAGE_DOS_HEADER>(file);

        if (dos.e_magic != IMAGE.IMAGE_DOS_SIGNATURE)
            throw new ArgumentException("Not a PE image.");

        file = file[dos.e_lfanew..];

        if (MemoryMarshal.Read<uint>(file) != IMAGE.IMAGE_NT_SIGNATURE)
            throw new ArgumentException("Invalid PE signature.");

        file = file[sizeof(uint)..];

        return (GetArchitecture(file), GetImageSize(file[sizeof(IMAGE_FILE_HEADER)..]));
    }

    private static uint GetImageSize(ReadOnlySpan<byte> optionalHeader)
    {
        ushort magic = MemoryMarshal.Read<ushort>(optionalHeader);
        return magic switch
        {
            IMAGE.IMAGE_NT_OPTIONAL_HDR32_MAGIC =>
                MemoryMarshal.Read<IMAGE_OPTIONAL_HEADER32>(optionalHeader).SizeOfImage,

            IMAGE.IMAGE_NT_OPTIONAL_HDR64_MAGIC =>
                MemoryMarshal.Read<IMAGE_OPTIONAL_HEADER64>(optionalHeader).SizeOfImage,

            _ => throw new NotSupportedException(
                $"Unsupported PE optional header magic: 0x{magic:X4}")
        };
    }

    private static Architecture GetArchitecture(ReadOnlySpan<byte> fileHeader)
    {
        ushort machine = MemoryMarshal.Read<IMAGE_FILE_HEADER>(fileHeader).Machine;
        return machine switch
        {
            IMAGE.IMAGE_FILE_MACHINE_I386 =>
                Architecture.X86,

            IMAGE.IMAGE_FILE_MACHINE_AMD64 =>
                Architecture.X64,

            IMAGE.IMAGE_FILE_MACHINE_ARMNT or
            IMAGE.IMAGE_FILE_MACHINE_ARM =>
                Architecture.Arm,

            IMAGE.IMAGE_FILE_MACHINE_ARM64 or
                0xA641 or // IMAGE_FILE_MACHINE_ARM64EC
                0xA64E => // IMAGE_FILE_MACHINE_ARM64X
                Architecture.Arm64,

            0x6264 => // IMAGE_FILE_MACHINE_LOONGARCH64
                Architecture.LoongArch64,

            IMAGE.IMAGE_FILE_MACHINE_POWERPC or
            IMAGE.IMAGE_FILE_MACHINE_POWERPCFP =>
                Architecture.Ppc64le,

            0x5064 => // IMAGE_FILE_MACHINE_RISCV64
                Architecture.RiscV64,

            _ => throw new NotSupportedException(
                $"Unsupported PE machine: 0x{machine:X4}")
        };
    }
}
