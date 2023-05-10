namespace MakeMapPack.SAGE;

internal sealed class Chunk
{
    public byte[] InstanceBuffer { get; private set; }
    public byte[] RelocationBuffer { get; private set; }
    public byte[] ImportsBuffer { get; private set; }

    public Chunk()
    {
        InstanceBuffer = Array.Empty<byte>();
        RelocationBuffer = Array.Empty<byte>();
        ImportsBuffer = Array.Empty<byte>();
    }

    internal void Allocate(int instanceBufferSize, int relocationBufferSize, int importsBufferSize)
    {
        InstanceBuffer = new byte[instanceBufferSize];
        if (relocationBufferSize > 0)
        {
            RelocationBuffer = new byte[relocationBufferSize];
        }
        if (importsBufferSize > 0)
        {
            ImportsBuffer = new byte[importsBufferSize];
        }
    }
}
