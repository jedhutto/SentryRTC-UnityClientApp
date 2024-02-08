using System;
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
    //private InputAction movement;
    //private InputAction cameraLook;
    private short negativeAdjustment = 127;
    private float lastPulseWidthX = -2;
    private short lastLeftTrack = -1;
    private short lastRightTrack = -1;
    private short lastDriveMessage = 0;

    InputAction movementAction;
    InputAction lookAction;
    InputActionMap inputMap;

    static readonly string KB = "Keyboard And Mouse";
    Dictionary<InputAction, Action<InputAction.CallbackContext>> setupActionHandlers = new Dictionary<InputAction, Action<InputAction.CallbackContext>>();
    Dictionary<string, string> actingBindings = new Dictionary<string, string>();

    public InputActionAsset GetActions() => input;
    bool setupComplete = false;
    void Start()
    {
        

        //InvokeRepeating("ReadDriveInput", 0, 1.0f/60.0f);
        //InvokeRepeating("ReadCameraLookInput", 0, 1.0f/60.0f);
    }

    void Update()
    {
        if (setupComplete)
        {
            ReadDriveInput();
            ReadCameraLookInput();
        }
    }

    void OnDestroy()
    {
        //we must unregister handlers or DARKNESS CONSUMES US ALL
        foreach (var action in setupActionHandlers.Keys)
        {
            Action<InputAction.CallbackContext> handler = setupActionHandlers[action];
            action.started -= handler;
            action.performed -= handler;
            action.canceled -= handler;
        }
    }

    public void Setup()
    {
        inputMap = input.FindActionMap("Tracks");

        inputMap.Enable();

        movementAction = inputMap.FindAction("Movement");
        lookAction = inputMap.FindAction("CameraLook");
        setupComplete = true;
    }

    void SetupAction(ref InputAction action, string actionName, Action<InputAction.CallbackContext> handler)
    {
        action = input.FindAction(actionName);
        if (action != null)
        {
            //register handlers
            action.started += handler;
            action.performed += handler;
            action.canceled += handler;
            setupActionHandlers.Add(action, handler);

            //get binding keys and store in dictionary
            int bindingIndex = action.GetBindingIndex(InputBinding.MaskByGroup(KB));
            if (bindingIndex > -1)
            {
                string displayString = action.GetBindingDisplayString(bindingIndex).ToUpper();
                actingBindings.Add(actionName, displayString);
            }
        }
    }

    private void ReadCameraLookInput()
    {
        //var vector = cameraLook.ReadValue<UnityEngine.Vector2>();
        var vector = lookAction.ReadValue<UnityEngine.Vector2>();
        var pulseWidthX = 500 + (2000 - ((vector.x + 1) * 1000));
        leftTrackText.text = ((short)pulseWidthX).ToString();
        if (connected && pulseWidthX != lastPulseWidthX)
        {
            lastPulseWidthX = pulseWidthX;
            var cameraLookSignal = new ServoSignal((short)(pulseWidthX), (short)(0));
            RTCDataChannel.Send(cameraLookSignal.GetBytes());
        }
    }

    private void ReadDriveInput()
    {
        //var vector = movement.ReadValue<UnityEngine.Vector2>();
        var vector = movementAction.ReadValue<UnityEngine.Vector2>();
        var radian = Mathf.Atan2(vector.x, vector.y);
        var magnitudeFactor = Mathf.Max( Mathf.Abs(vector.x), Mathf.Abs(vector.y));
        //if((short)(CalculateDriveValue(radian) * magnitudeFactor * 127) > 0)
        //    leftTrackText.text = ((short)(CalculateDriveValue(radian * -1) * magnitudeFactor * 127)).ToString();
        //if ((short)(CalculateDriveValue(radian * -1) * magnitudeFactor * 127) > 0)
        //    rightTrackText.text = ((short)(CalculateDriveValue(radian) * magnitudeFactor * 127)).ToString();
        leftTrack = (short)(CalculateDriveValue(radian * -1) * magnitudeFactor * 127);
        rightTrack = (short)(CalculateDriveValue(radian) * magnitudeFactor * 127);

        if (connected && (leftTrack != lastLeftTrack || rightTrack != lastRightTrack || lastDriveMessage > 30))
        {
            lastDriveMessage = 0;
            lastLeftTrack = leftTrack;
            lastRightTrack = rightTrack;
            var movementSignal = new MovementSignal((short)(leftTrack + negativeAdjustment), (short)(rightTrack + negativeAdjustment));
            RTCDataChannel.Send(movementSignal.GetBytes());
        }
        else
        {
            lastDriveMessage++;
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

    public string GetActionBinding(string actionName)
    {
        if (actingBindings.TryGetValue(actionName, out string value))
            return value;
        else
        {
            string lookup = FindActionString(actionName);
            if (lookup != null)
            {
                actingBindings.Add(actionName, lookup);
                return lookup;
            }
            else
                return null;
        }
    }

    string FindActionString(string actionName)
    {
        InputAction action = input.FindAction(actionName);
        int bindingIndex = action.GetBindingIndex(InputBinding.MaskByGroup(KB));
        if (bindingIndex > -1)
        {
            return action.GetBindingDisplayString(bindingIndex).ToUpper();
        }
        return null;
    }
}
