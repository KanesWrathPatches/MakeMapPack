using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MakeMapPack.SAGE;

[StructLayout(LayoutKind.Sequential)]
public struct AssetEntry
{
    public uint TypeId;
    public uint InstanceId;
    public uint TypeHash;
    public uint InstanceHash;
    public int AssetReferenceOffset;
    public int AssetReferenceCount;
    public int NameOffset;
    public int SourceFileNameOffset;
    public int InstanceDataSize;
    public int RelocationDataSize;
    public int ImportsDataSize;

    public void Swap()
    {
    }

    public unsafe void SaveToStream(Stream output, bool isBigEndian)
    {
        if (isBigEndian)
        {
            Swap();
        }
        byte[] buffer = new byte[Unsafe.SizeOf<AssetEntry>()];
        fixed (AssetEntry* pThis = &this)
        {
            new UnmanagedMemoryStream((byte*)pThis, buffer.Length).Read(buffer, 0, buffer.Length);
        }
        output.Write(buffer);
        if (isBigEndian)
        {
            Swap();
        }
    }
}
