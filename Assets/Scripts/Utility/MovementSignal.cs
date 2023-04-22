using System;
using System.Linq;

public class MovementSignal : Signal
{
    public short trackLeft;
    public short trackRight;

    public MovementSignal(short l,short r)
    {
        id = (ushort)SignalType.Movement;
        trackLeft = l;
        trackRight = r;
    }

    public byte[] GetBytes()
    {
        byte[] bytes = new byte[sizeof(short)*3];
        BitConverter.GetBytes(id).CopyTo(bytes, 0);
        BitConverter.GetBytes(trackLeft).CopyTo(bytes, sizeof(short) * 1);
        BitConverter.GetBytes(trackRight).CopyTo(bytes, sizeof(short) * 2);
        return bytes;
    }
}
