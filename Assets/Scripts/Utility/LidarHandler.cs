using System;
using System.Security.Cryptography;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class LidarHandler : MonoBehaviour
{
    public RawImage graph;
    public RawImage graphBackground;
    public Texture2D graphBackgroundTexture;
    public Texture2D graphTextureTemplate;
    public Texture2D graphTexture;
    public bool isLidarDataReady = false;
    public byte[] rawData;
    public bool dataLock = false;
    public int radarSize = 300;
    private void Update()
    {
        if (isLidarDataReady && !dataLock)
        {
            dataLock = true;
            DrawLidarData();
            isLidarDataReady = false;
            dataLock = false;
        }
    }

    public void SetLidarData(byte[] data)
    {
        while (dataLock)
        {
            //wait for the data to be drawn
        }
        dataLock = true;
        //rawData = new byte[data.Length];
        rawData = data;
        //Array.Copy(data, rawData, data.Length);
        isLidarDataReady = true;
        dataLock = false;
    }

    public void DrawLidarData()
    {
        //NativeArray<byte> nativeArray = new NativeArray<byte>(bytes, Allocator.Temp);
        //NativeArray<LidarDataSignal> floats = nativeArray.Reinterpret<LidarDataSignal>(UnsafeUtility.SizeOf<LidarDataSignal>());

        //move all this logic to LidarHandler.cs and use 
        var baseColor = new UnityEngine.Color(0, 0, 0, 0.0f);
        if (graphTextureTemplate == null)
        {
            graphTextureTemplate = new Texture2D(radarSize, radarSize);
            for (int i = 0; i < graphTextureTemplate.width; i++)
            {
                for (int j = 0; j < graphTextureTemplate.height; j++)
                {
                    graphTextureTemplate.SetPixel(i, j, baseColor);
                }
            }
            //Move this block to a new texture which will be placed behind the transparent lidar texture
            //This way we won't have to redraw the tank every frame, and it will allow us to
            //rotate the lidar data to match the tank's camera angle while keeping the tank in the same position
            graphTexture = new Texture2D(graphTextureTemplate.width, graphTextureTemplate.height);
            graphBackgroundTexture = new Texture2D(graphTextureTemplate.width, graphTextureTemplate.height);
            float centerX = graphBackgroundTexture.width / 2;
            float centerY = graphBackgroundTexture.height / 2;
            DrawPoint((int)centerX + 2*3, (int)centerY + 3*3, UnityEngine.Color.red, graphTextureTemplate);
            DrawPoint((int)centerX + 1*3, (int)centerY + 3*3, UnityEngine.Color.red, graphTextureTemplate);
            DrawPoint((int)centerX + 0*3, (int)centerY + 2*3, UnityEngine.Color.red, graphTextureTemplate);
            DrawPoint((int)centerX - 1*3, (int)centerY + 3*3, UnityEngine.Color.red, graphTextureTemplate);
            DrawPoint((int)centerX - 2*3, (int)centerY + 3*3, UnityEngine.Color.red, graphTextureTemplate);
                                      
            DrawPoint((int)centerX + 2*3, (int)centerY + 2*3, UnityEngine.Color.red, graphTextureTemplate);
            DrawPoint((int)centerX + 2*3, (int)centerY + 1*3, UnityEngine.Color.red, graphTextureTemplate);
            DrawPoint((int)centerX + 1*3, (int)centerY + 0*3, UnityEngine.Color.red, graphTextureTemplate);
            DrawPoint((int)centerX + 2*3, (int)centerY - 1*3, UnityEngine.Color.red, graphTextureTemplate);
            DrawPoint((int)centerX + 2*3, (int)centerY - 2*3, UnityEngine.Color.red, graphTextureTemplate);
                                      
            DrawPoint((int)centerX - 2*3, (int)centerY + 2*3, UnityEngine.Color.red, graphTextureTemplate);
            DrawPoint((int)centerX - 2*3, (int)centerY + 1*3, UnityEngine.Color.red, graphTextureTemplate);
            DrawPoint((int)centerX - 1*3, (int)centerY + 0*3, UnityEngine.Color.red, graphTextureTemplate);
            DrawPoint((int)centerX - 2*3, (int)centerY - 1*3, UnityEngine.Color.red, graphTextureTemplate);
            DrawPoint((int)centerX - 2*3, (int)centerY - 2*3, UnityEngine.Color.red, graphTextureTemplate);
                                      
            DrawPoint((int)centerX + 2*3, (int)centerY - 3*3, UnityEngine.Color.red, graphTextureTemplate);
            DrawPoint((int)centerX + 1*3, (int)centerY - 3*3, UnityEngine.Color.red, graphTextureTemplate);
            DrawPoint((int)centerX + 0*3, (int)centerY - 2*3, UnityEngine.Color.red, graphTextureTemplate);
            DrawPoint((int)centerX - 1*3, (int)centerY - 3*3, UnityEngine.Color.red, graphTextureTemplate);
            DrawPoint((int)centerX - 2*3, (int)centerY - 3*3, UnityEngine.Color.red, graphTextureTemplate);

            graphTexture.SetPixels(graphTextureTemplate.GetPixels());

            graphBackgroundTexture.Apply();
            graphBackground.texture = graphBackgroundTexture;
        }
        else
        {
            graphTexture.SetPixels(graphTextureTemplate.GetPixels());
        }
        var SignalType = BitConverter.ToUInt16(rawData, 0);
        var count = 0;

        for (int i = 4; i + 4 < rawData.Length; i += 4)
        {
            count++;

            var x = BitConverter.ToSingle(rawData, i);
            i += 4;
            var y = BitConverter.ToSingle(rawData, i);
            //i += 4;
            //var end = BitConverter.ToBoolean(rawData, i);

            float pixelX = ((graphTexture.width / 2) + x * .015f);
            float pixelY = ((graphTexture.height / 2) + y * .015f);
            DrawPoint((int)pixelX, (int)pixelY, UnityEngine.Color.green, graphTexture);
        }

        graphTexture.Apply();
        graph.texture = graphTexture;
    }

    private void DrawPoint(int x, int y, UnityEngine.Color color, Texture2D texture)
    {
        if (x < 1 || x > radarSize - 1 || y < 1 || y > radarSize - 1)
            return;

        texture.SetPixel(x-1, y+1, color);
        texture.SetPixel(x-1, y, color);
        texture.SetPixel(x-1, y-1, color);
        texture.SetPixel(x, y + 1, color);
        texture.SetPixel(x, y, color);
        texture.SetPixel(x, y - 1, color);
        texture.SetPixel(x+1, y + 1, color);
        texture.SetPixel(x+1, y, color);
        texture.SetPixel(x+1, y-1, color);
    }
}