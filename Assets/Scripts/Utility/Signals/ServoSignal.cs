using System;

public class ServoSignal : Signal
{
    public short pulseWidth;
    public short channel;

    public ServoSignal(short p, short c)
    {
        id = (ushort)SignalType.CameraLook;
        pulseWidth = p;
        channel = c;
    }
     
    public byte[] GetBytes()
    {
        byte[] bytes = new byte[sizeof(short) * 3];
        BitConverter.GetBytes(id        ).CopyTo(bytes, sizeof(short) * 0);
        BitConverter.GetBytes(pulseWidth).CopyTo(bytes, sizeof(short) * 1);
        BitConverter.GetBytes(channel   ).CopyTo(bytes, sizeof(short) * 2);
        return bytes;
    }
}
