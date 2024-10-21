using System;

public class ServoSignal : Signal
{
    public short pulseWidth0;
    public short pulseWidth1;
    public short pulseWidth2;
    public short pulseWidth3;
    public short pulseWidth4;

    public ServoSignal(short p0, short p1, short p2, short p3, short p4)
    {
        id = (ushort)SignalType.CameraLook;
        pulseWidth0 = p0;
        pulseWidth1 = p1;
        pulseWidth2 = p2;
        pulseWidth3 = p3;
        pulseWidth4 = p4;
    }
    public ServoSignal(short[] servoArray)
    {
        id = (ushort)SignalType.CameraLook;
        pulseWidth0 = servoArray[0];
        pulseWidth1 = servoArray[1];
        pulseWidth2 = servoArray[2];
        pulseWidth3 = servoArray[3];
        pulseWidth4 = servoArray[4];
    }
     
    public byte[] GetBytes()
    {
        byte[] bytes = new byte[sizeof(short) * 6];
        BitConverter.GetBytes(id        ).CopyTo(bytes, sizeof(short) * 0);
        BitConverter.GetBytes(pulseWidth0).CopyTo(bytes, sizeof(short) * 1);
        BitConverter.GetBytes(pulseWidth1).CopyTo(bytes, sizeof(short) * 2);
        BitConverter.GetBytes(pulseWidth2).CopyTo(bytes, sizeof(short) * 3);
        BitConverter.GetBytes(pulseWidth3).CopyTo(bytes, sizeof(short) * 4);
        BitConverter.GetBytes(pulseWidth4).CopyTo(bytes, sizeof(short) * 5);

        return bytes;
    }
}
