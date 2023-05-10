namespace MakeMapPack.SAGE;

internal sealed class UInt32Buffer
{
    private readonly List<uint> _data = new();

    public int Length => _data.Count * 4;

    public UInt32Buffer()
    {
    }

    public int AddValue(uint value)
    {
        int position = _data.Count * 4;
        _data.Add(value);
        return position;
    }

    public void SaveToStream(Stream output, bool isBigEndian)
    {
        BinaryWriter writer = new(output);
        if (isBigEndian)
        {
            for (int idx = 0; idx < _data.Count; ++idx)
            {
                writer.Write(Endian.BigEndian(_data[idx]));
            }
        }
        else
        {
            for (int idx = 0; idx < _data.Count; ++idx)
            {
                writer.Write(_data[idx]);
            }
        }
    }
}
