using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MakeMapPack.SAGE;

internal sealed class Asset : IEquatable<Asset>
{
    private readonly AssetEntry _assetEntry;
    private Chunk? _chunk;

    public AssetReference[] AssetReferences { get; }
    public string FileBasePath { get; }
    public string CDataPath { get; }
    public bool HasErrors { get; private set; }
    public string Source { get; }
    public string Message { get; private set; }
    public string QualifiedName { get; }
    public string TypeName { get; }
    public string InstanceName { get; }
    public int Index { get; }
    public uint TypeId => _assetEntry.TypeId;
    public uint InstanceId => _assetEntry.InstanceId;
    public uint TypeHash => _assetEntry.TypeHash;
    public uint InstanceHash => _assetEntry.InstanceHash;
    public int InstanceDataSize => _assetEntry.InstanceDataSize;
    public int RelocationDataSize => _assetEntry.RelocationDataSize;
    public int ImportsDataSize => _assetEntry.ImportsDataSize;
    public int LinkedInstanceOffset { get; }
    public int LinkedRelocationOffset { get; }
    public int LinkedImportsOffset { get; }
    public Manifest SourceManifest { get; }

    public unsafe Asset(int index, string basePath, ref AssetEntry assetEntry, Span<byte> assetNameBuffer, Span<byte> sourceFileNameBuffer, ReadOnlySpan<byte> assetReferenceBuffer, Manifest sourceManifest, int linkedInstanceOffset, int linkedRelocationOffset, int linkedImportsOffset)
    {
        _assetEntry = assetEntry;
        AssetReferences = new AssetReference[_assetEntry.AssetReferenceCount];
        if (_assetEntry.AssetReferenceCount > 0)
        {
            MemoryMarshal.Cast<byte, AssetReference>(assetReferenceBuffer[_assetEntry.AssetReferenceOffset..])[.._assetEntry.AssetReferenceCount].CopyTo(AssetReferences);
        }
        QualifiedName = Marshal.PtrToStringAnsi(new nint(Unsafe.AsPointer(ref assetNameBuffer[_assetEntry.NameOffset])))!;
        string[] id = QualifiedName.Split(':');
        TypeName = id[0];
        InstanceName = id[1];
        HasErrors = false;
        Message = string.Empty;
        Source = Marshal.PtrToStringAnsi(new nint(Unsafe.AsPointer(ref sourceFileNameBuffer[_assetEntry.SourceFileNameOffset])))!;
        Index = index;
        SourceManifest = sourceManifest;
        string path = $"{_assetEntry.TypeId:x8}.{_assetEntry.TypeHash:x8}.{_assetEntry.InstanceId:x8}.{_assetEntry.InstanceHash:x8}";
        FileBasePath = Path.Combine(Path.Combine(basePath, "assets"), path);
        CDataPath = Path.Combine(Path.Combine(basePath, "cdata"), path + ".cdata");
        LinkedInstanceOffset = linkedInstanceOffset;
        LinkedImportsOffset = linkedImportsOffset;
        LinkedRelocationOffset = linkedRelocationOffset;
    }

    public void LoadChunk()
    {
        _chunk = GetChunk();
    }

    public Chunk GetChunk()
    {
        return _chunk ?? SourceManifest.GetChunk(this);
    }

    public byte[]? GetCData()
    {
        return SourceManifest.GetCData(this);
    }

    public void Commit(string manifestBasePath)
    {
        Chunk chunk = GetChunk();

        using (Stream stream = new FileStream(manifestBasePath + ".bin", FileMode.Open, FileAccess.Write, FileShare.None))
        {
            stream.Seek(0, SeekOrigin.End);
            stream.Write(chunk.InstanceBuffer);
            stream.Flush();
        }
        using (Stream stream = new FileStream(manifestBasePath + ".relo", FileMode.Open, FileAccess.Write, FileShare.None))
        {
            stream.Seek(0, SeekOrigin.End);
            stream.Write(chunk.RelocationBuffer);
            stream.Flush();
        }
        using (Stream stream = new FileStream(manifestBasePath + ".imp", FileMode.Open, FileAccess.Write, FileShare.None))
        {
            stream.Seek(0, SeekOrigin.End);
            stream.Write(chunk.ImportsBuffer);
            stream.Flush();
        }
        byte[]? cdata = GetCData();
        if (cdata is null)
        {
            return;
        }
        throw new NotImplementedException();
    }

    public bool Equals(Asset? other)
    {
        if (other is null)
        {
            return false;
        }
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        return TypeId == other.TypeId && InstanceId == other.InstanceId && TypeHash == other.TypeHash && InstanceHash == other.InstanceHash;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Asset);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TypeId, InstanceId, TypeHash, InstanceHash);
    }

    public override string ToString()
    {
        return QualifiedName;
    }
}
