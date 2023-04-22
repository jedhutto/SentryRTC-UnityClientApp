using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using TMPro;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

public class InputHandler : MonoBehaviour
{
    #pragma warning disable 0649
    [SerializeField] public TMP_Text leftTrackText;
    [SerializeField] public TMP_Text rightTrackText;
    [SerializeField] public InputActionAsset input;
#pragma warning restore 0649
    public short leftTrack;
    public short rightTrack;
    public RTCDataChannel RTCDataChannel;
    public bool connected = false;
    private InputAction movement;
    private short negativeAdjustment = 127;

    void Start()
    {
        movement = input.FindAction("Movement");
        InvokeRepeating("ReadInput", 0, 1.0f/60.0f);
    }

    private void ReadInput()
    {
        var vector = movement.ReadValue<UnityEngine.Vector2>();
        var radian = Mathf.Atan2(vector.x, vector.y);
        var magnitudeFactor = Mathf.Max( Mathf.Abs(vector.x), Mathf.Abs(vector.y));
        if((short)(CalculateDriveValue(radian) * magnitudeFactor * 127) > 0)
            leftTrackText.text = ((short)(CalculateDriveValue(radian * -1) * magnitudeFactor * 127)).ToString();
        if ((short)(CalculateDriveValue(radian * -1) * magnitudeFactor * 127) > 0)
            rightTrackText.text = ((short)(CalculateDriveValue(radian) * magnitudeFactor * 127)).ToString();
        leftTrack = (short)(CalculateDriveValue(radian * -1) * magnitudeFactor * 127);
        rightTrack = (short)(CalculateDriveValue(radian) * magnitudeFactor * 127);

        if (connected)
        {
            var movementSignal = new MovementSignal((short)(leftTrack + negativeAdjustment), (short)(rightTrack + negativeAdjustment));
            RTCDataChannel.Send(movementSignal.GetBytes());
        }
    }

    private static float CalculateDriveValue(float radian)
    {
        float vect;
        if (Mathf.Atan2(1, 0) >= radian && radian >= Mathf.Atan2(0, 1))
        {
            vect = 1;
        }
        else if ((Mathf.Atan2(1, 0) > radian && radian > Mathf.Atan2(-1, 0)) || (Mathf.Atan2(0, -1) > radian && radian > Mathf.Atan2(1, 0)))
        {
            if(radian < 0)
            {
                vect = (radian / Mathf.Atan2(-1, 0)) * -2 + 1;
            }
            else
            {
                vect = ((Mathf.Atan2(0, -1) - radian) / Mathf.Atan2(0, -1)) * 4 - 1;
            }
        }
        else
        {
            vect = -1;
        }
        return vect;
    }
}
