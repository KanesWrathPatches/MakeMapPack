using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace MakeMapPack.SAGE;

[StructLayout(LayoutKind.Sequential)]
public struct AssetReference : IEquatable<AssetReference>
{
    public uint TypeId;
    public uint InstanceId;

    public bool Equals(AssetReference other)
    {
        return TypeId == other.TypeId && InstanceId == other.InstanceId;
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is AssetReference other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TypeId, InstanceId);
    }

    public override string ToString()
    {
        return $"[{TypeId:X08}:{InstanceId:X08}]";
    }

    public static bool operator ==(AssetReference left, AssetReference right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(AssetReference left, AssetReference right)
    {
        return !left.Equals(right);
    }
}
