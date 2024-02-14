

public struct LidarDataCoordinate
{
    float x;
    float y;
    bool isEnd;
}

public class LidarDataSignal : Signal
{
    public LidarDataCoordinate[] LidarData = new LidarDataCoordinate[8192];
    public LidarDataSignal()
    {
        id = (ushort)SignalType.LidarDataArray;
    }
};