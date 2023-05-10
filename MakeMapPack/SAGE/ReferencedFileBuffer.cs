using System.Runtime.InteropServices;

namespace MakeMapPack.SAGE;

internal sealed class ReferencedFileBuffer
{
    private readonly List<byte> _data = new();
    private readonly Dictionary<int, int> _positions = new();

    public int Length => _data.Count;

    public ReferencedFileBuffer()
    {
    }

    public unsafe int AddReference(string name, bool isPatch)
    {
        int nameHash = name.GetHashCode();
        if (!_positions.TryGetValue(nameHash, out int position))
        {
            IntPtr hName = Marshal.StringToHGlobalAnsi(name);
            position = _data.Count;
            byte* pName = (byte*)hName;
            if (isPatch)
            {
                _data.Add(2);
            }
            else
            {
                _data.Add(1);
            }
            while (*pName != IntPtr.Zero)
            {
                _data.Add(*pName++);
            }
            _data.Add(0);
            Marshal.FreeHGlobal(hName);
            _positions.Add(nameHash, position);
        }
        return position;
    }

    public void SaveToStream(Stream output)
    {
        output.Write(CollectionsMarshal.AsSpan(_data));
    }
}
