public class Signal
{
    public ushort id;

    public enum SignalType :  ushort
    {
        Movement = 0,
        DataStream = 1,
        Interact = 2,
        MovementConfig = 3,
        CameraConfig = 4,
        Message = 5
    }
}
