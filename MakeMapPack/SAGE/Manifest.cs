using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MakeMapPack.SAGE;

internal sealed class Manifest
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct ManifestHeader
    {
        public bool IsBigEndian;
        public bool IsLinked;
        public ushort Version;
        public uint StreamChecksum;
        public uint AllTypesHash;
        public int AssetCount;
        public int TotalInstanceDataSize;
        public int MaxInstanceChunkSize;
        public int MaxRelocationChunkSize;
        public int MaxImportsChunkSize;
        public int AssetReferenceBufferSize;
        public int ExternalManifestNameBufferSize;
        public int AssetNameBufferSize;
        public int SourceFileNameBufferSize;

        public void Swap()
        {
        }

        public unsafe void SaveToStream(Stream output, bool isBigEndian)
        {
            if (isBigEndian)
            {
                Swap();
            }
            byte[] buffer = new byte[Unsafe.SizeOf<ManifestHeader>()];
            fixed (ManifestHeader* pThis = &this)
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

    private readonly string _directory;
    private readonly string _name;
    private readonly ManifestHeader _header;
    private readonly Asset[] _assets;
    private readonly string? _patchManifest;
    private readonly string[] _externalManifests;

    public bool IsLinked => _header.IsLinked;
    public uint StreamChecksum => _header.StreamChecksum;
    public uint AllTypesHash => _header.AllTypesHash;
    public int AssetCount => _header.AssetCount;
    public Asset[] Assets => _assets;
    public string? PatchManifest => _patchManifest;
    public string[] ExternalManifests => _externalManifests;

    public unsafe Manifest(string path)
    {
        _directory = Path.GetDirectoryName(path)!;
        _name = Path.GetFileNameWithoutExtension(path);
        byte[] data = File.ReadAllBytes(path);
        Span<byte> span = data.AsSpan();
        _header = Unsafe.As<byte, ManifestHeader>(ref data[0]);
        int offset = Unsafe.SizeOf<ManifestHeader>();

        if (IsLinked)
        {
            using (Stream stream = File.OpenRead(Path.Combine(_directory, _name + ".bin")))
            {
                BinaryReader reader = new(stream);
                if (reader.ReadUInt32() != StreamChecksum)
                {
                    throw new InvalidDataException("Checksum mismatch with instance data.");
                }
            }
            using (Stream stream = File.OpenRead(Path.Combine(_directory, _name + ".relo")))
            {
                BinaryReader reader = new(stream);
                if (reader.ReadUInt32() != StreamChecksum)
                {
                    throw new InvalidDataException("Checksum mismatch with relocation data.");
                }
            }
            using (Stream stream = File.OpenRead(Path.Combine(_directory, _name + ".imp")))
            {
                BinaryReader reader = new(stream);
                if (reader.ReadUInt32() != StreamChecksum)
                {
                    throw new InvalidDataException("Checksum mismatch with imports data.");
                }
            }
        }

        _assets = new Asset[AssetCount];

        Span<AssetEntry> assetEntries = MemoryMarshal.Cast<byte, AssetEntry>(span.Slice(offset, AssetCount * Unsafe.SizeOf<AssetEntry>()));
        offset += AssetCount * Unsafe.SizeOf<AssetEntry>();
        Span<byte> assetReferenceBuffer = span.Slice(offset, _header.AssetReferenceBufferSize);
        offset += _header.AssetReferenceBufferSize;
        Span<byte> externalManifestNameBuffer = span.Slice(offset, _header.ExternalManifestNameBufferSize);
        offset += _header.ExternalManifestNameBufferSize;
        Span<byte> assetNameBuffer = span.Slice(offset, _header.AssetNameBufferSize);
        offset += _header.AssetNameBufferSize;
        Span<byte> sourceFileNameBuffer = span.Slice(offset, _header.SourceFileNameBufferSize);
        offset += _header.SourceFileNameBufferSize;

        int linkedInstanceOffset = 4;
        int linkedRelocationOffset = 4;
        int linkedImportsOffset = 4;
        for (int idx = 0; idx < AssetCount; ++idx)
        {
            ref AssetEntry assetEntry = ref assetEntries[idx];
            _assets[idx] = new Asset(idx, _name, ref assetEntry, assetNameBuffer, sourceFileNameBuffer, assetReferenceBuffer, this, linkedInstanceOffset, linkedRelocationOffset, linkedImportsOffset);
            linkedInstanceOffset += assetEntry.InstanceDataSize;
            linkedRelocationOffset += assetEntry.RelocationDataSize;
            linkedImportsOffset += assetEntry.ImportsDataSize;
        }

        List<string> externalManifests = new();
        while (!externalManifestNameBuffer.IsEmpty)
        {
            bool isPatch = externalManifestNameBuffer[0] == 2;
            string manifestName = Marshal.PtrToStringAnsi(new nint(Unsafe.AsPointer(ref externalManifestNameBuffer[1])))!;
            externalManifestNameBuffer = externalManifestNameBuffer[(2 + manifestName.Length)..];
            if (isPatch)
            {
                _patchManifest = manifestName;
            }
            else
            {
                externalManifests.Add(manifestName);
            }
        }
        _externalManifests = externalManifests.ToArray();
    }

    public Chunk GetChunk(Asset asset)
    {
        Chunk chunk = new();
        chunk.Allocate(asset.InstanceDataSize, asset.RelocationDataSize, asset.ImportsDataSize);
        if (IsLinked)
        {
            using (Stream stream = File.OpenRead(Path.Combine(_directory, _name + ".bin")))
            {
                stream.Seek(asset.LinkedInstanceOffset, SeekOrigin.Begin);
                stream.Read(chunk.InstanceBuffer);
            }
            using (Stream stream = File.OpenRead(Path.Combine(_directory, _name + ".relo")))
            {
                stream.Seek(asset.LinkedRelocationOffset, SeekOrigin.Begin);
                stream.Read(chunk.RelocationBuffer);
            }
            using (Stream stream = File.OpenRead(Path.Combine(_directory, _name + ".imp")))
            {
                stream.Seek(asset.LinkedImportsOffset, SeekOrigin.Begin);
                stream.Read(chunk.ImportsBuffer);
            }
        }
        else
        {
            using Stream stream = File.OpenRead(Path.Combine(_directory, asset.FileBasePath + ".asset"));
            stream.Seek(32, SeekOrigin.Begin);
            stream.Read(chunk.InstanceBuffer);
            stream.Read(chunk.RelocationBuffer);
            stream.Read(chunk.ImportsBuffer);
        }
        return chunk;
    }

    public byte[]? GetCData(Asset asset)
    {
        string path = Path.Combine(_directory, asset.CDataPath);

        if (!File.Exists(path))
        {
            return null;
        }
        return File.ReadAllBytes(path);
    }

    private sealed class AssetData
    {
        public int AssetCount = 0;
        public int InstanceDataSize = 0;
        public int MaxInstanceChunkSize = 0;
        public int MaxRelocationChunkSize = 0;
        public int MaxImportsChunkSize = 0;

        public UInt32Buffer AssetReferenceBuffer = new();
        public NameBuffer NameBuffer = new();
        public NameBuffer SourceFileNameBuffer = new();
        public ReferencedFileBuffer ReferenceManifestBuffer = new();

        public AssetData()
        {
        }
    }

    private void MergeAsset(AssetData data, Asset asset, Stream assetEntryStream, string manifestBasePath)
    {
        ++data.AssetCount;
        int length = data.AssetReferenceBuffer.Length;
        foreach (AssetReference assetReference in asset.AssetReferences)
        {
            data.AssetReferenceBuffer.AddValue(assetReference.TypeId);
            data.AssetReferenceBuffer.AddValue(assetReference.InstanceId);
        }
        if (asset.SourceManifest != this)
        {
            data.InstanceDataSize += asset.InstanceDataSize;
        }
        data.MaxInstanceChunkSize = Math.Max(asset.InstanceDataSize, data.MaxInstanceChunkSize);
        data.MaxRelocationChunkSize = Math.Max(asset.RelocationDataSize, data.MaxRelocationChunkSize);
        data.MaxImportsChunkSize = Math.Max(asset.ImportsDataSize, data.MaxImportsChunkSize);
        AssetEntry assetEntry = new()
        {
            TypeId = asset.TypeId,
            InstanceId = asset.InstanceId,
            TypeHash = asset.TypeHash,
            InstanceHash = asset.InstanceHash,
            AssetReferenceOffset = length,
            AssetReferenceCount = asset.AssetReferences.Length,
            NameOffset = data.NameBuffer.AddName(asset.QualifiedName),
            SourceFileNameOffset = data.SourceFileNameBuffer.AddName(asset.Source),
            InstanceDataSize = asset.InstanceDataSize,
            RelocationDataSize = asset.RelocationDataSize,
            ImportsDataSize = asset.ImportsDataSize
        };
        if (asset.SourceManifest == this)
        {
            assetEntry.InstanceDataSize = 0;
            assetEntry.RelocationDataSize = 0;
            assetEntry.ImportsDataSize = 0;
        }
        assetEntry.SaveToStream(assetEntryStream, false);
        if (asset.SourceManifest != this)
        {
            asset.Commit(manifestBasePath);
        }
    }

    private bool SortAssetsByReference(ref int index, List<Asset> assets)
    {
        Asset asset = assets[index];
        foreach (AssetReference assetReference in asset.AssetReferences)
        {
            for (int idx = index; idx < assets.Count; ++idx)
            {
                if (assets[idx].TypeId == assetReference.TypeId && assets[idx].InstanceId == assetReference.InstanceId)
                {
                    Asset referencedAsset = assets[idx];
                    if (referencedAsset.InstanceDataSize == 0)
                    {
                        Console.WriteLine($"Cannot resort {referencedAsset.QualifiedName} referenced from {asset.QualifiedName}.");
                        Debugger.Launch();
                        return false;
                    }
                    assets.RemoveAt(idx);
                    assets.Insert(index, referencedAsset);
                    if (!SortAssetsByReference(ref index, assets))
                    {
                        return false;
                    }
                    ++index;
                    break;
                }
            }
        }
        return true;
    }

    public bool Merge(Manifest other, string? newPath)
    {
        string manifestBasePath = Path.Combine(newPath ?? _directory, _name);
        string versionName = "_leafmod";
        string patchBasePath = manifestBasePath + versionName;
        uint checksum = (uint)Random.Shared.NextInt64();
        if (!Directory.Exists(patchBasePath))
        {
            Directory.CreateDirectory(patchBasePath);
        }
        File.WriteAllLines(manifestBasePath + ".version", new string[] { versionName });
        using (Stream stream = new FileStream(patchBasePath + ".bin", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            BinaryWriter writer = new(stream);
            writer.Write(checksum);
            writer.Flush();
        }
        using (Stream stream = new FileStream(patchBasePath + ".relo", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            BinaryWriter writer = new(stream);
            writer.Write(checksum);
            writer.Flush();
        }
        using (Stream stream = new FileStream(patchBasePath + ".imp", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            BinaryWriter writer = new(stream);
            writer.Write(checksum);
            writer.Flush();
        }
        using MemoryStream assetEntryStream = new();
        AssetData data = new();

        List<Asset> assets = new();
        assets.AddRange(Assets);
        foreach (Asset asset in other.Assets)
        {
            int index = assets.FindIndex(x => x.TypeId == asset.TypeId && x.InstanceId == asset.InstanceId);
            if (index != -1)
            {
                assets.RemoveAt(index);
                assets.Insert(index, asset);
            }
            else
            {
                assets.Add(asset);
            }
        }

        for (int idx = 0; idx < assets.Count; ++idx)
        {
            if (!SortAssetsByReference(ref idx, assets))
            {
                Console.WriteLine("Error sorting assets.");
                return false;
            }
        }

        foreach (Asset asset in assets)
        {
            MergeAsset(data, asset, assetEntryStream, patchBasePath);
        }
        byte[] assetBuffer = assetEntryStream.GetBuffer();
        data.ReferenceManifestBuffer.AddReference(manifestBasePath[manifestBasePath.IndexOf(Path.Combine("maps", "official"), StringComparison.OrdinalIgnoreCase)..] + ".manifest", true);
        foreach (string referencedManfiest in _externalManifests)
        {
            data.ReferenceManifestBuffer.AddReference(referencedManfiest, false);
        }
        using Stream fileStream = new FileStream(patchBasePath + ".manifest", FileMode.Create, FileAccess.Write, FileShare.None);
        new ManifestHeader
        {
            IsBigEndian = _header.IsBigEndian,
            IsLinked = true,
            Version = 5,
            StreamChecksum = checksum,
            AllTypesHash = AllTypesHash,
            AssetCount = data.AssetCount,
            TotalInstanceDataSize = data.InstanceDataSize,
            MaxInstanceChunkSize = data.MaxInstanceChunkSize,
            MaxRelocationChunkSize = data.MaxRelocationChunkSize,
            MaxImportsChunkSize = data.MaxImportsChunkSize,
            AssetReferenceBufferSize = data.AssetReferenceBuffer.Length,
            ExternalManifestNameBufferSize = data.ReferenceManifestBuffer.Length,
            AssetNameBufferSize = data.NameBuffer.Length,
            SourceFileNameBufferSize = data.SourceFileNameBuffer.Length
        }.SaveToStream(fileStream, false);
        fileStream.Write(assetBuffer.AsSpan()[..(int)assetEntryStream.Length]);
        data.AssetReferenceBuffer.SaveToStream(fileStream, false);
        data.ReferenceManifestBuffer.SaveToStream(fileStream);
        data.NameBuffer.SaveToStream(fileStream);
        data.SourceFileNameBuffer.SaveToStream(fileStream);
        return true;
    }

    public override string ToString()
    {
        return Path.Combine(_directory, _name + ".manifest");
    }
}
