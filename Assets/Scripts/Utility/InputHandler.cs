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
    public float lookAngle = 0;
    public int selectedServo = 1;
    public short[] currentServoValues = { 1500,1250,1250,1250,1250};
    public float[] armSegmentLength = { 49,49,20};
    public float[] armSegmentAngleRange = { 90f,90f,90f};
    public float[] armSegmentAngleOffset = { 0f,0f,-90f};
    public short[] pulseWidthMaxDeltaValues = { 10,15,20,20,20};
    public short[] servoDirectionCorrection = { -1,1,-1,1,1};
    //private InputAction movement;
    //private InputAction cameraLook;
    private short negativeAdjustment = 127;
    private float lastPulseWidthX = -2;
    private short lastLeftTrack = -1;
    private short lastRightTrack = -1;
    private short lastDriveMessage = 0;

    InputAction movementAction;
    InputAction lookAction;
    InputAction servoSelectUpAction;
    InputAction servoSelectDownAction;
    InputActionMap inputMap;

    static readonly string KB = "Keyboard And Mouse";
    Dictionary<InputAction, Action<InputAction.CallbackContext>> setupActionHandlers = new Dictionary<InputAction, Action<InputAction.CallbackContext>>();
    Dictionary<string, string> actingBindings = new Dictionary<string, string>();

    public InputActionAsset GetActions() => input;
    bool setupComplete = false;
    void Start()
    {
        InvokeRepeating(nameof(ReadDriveInput), 0, 1.0f / 60.0f);
        InvokeRepeating(nameof(ReadServoInput), 0, 1.0f / 60.0f);

        //InvokeRepeating("ReadDriveInput", 0, 1.0f/60.0f);
        //InvokeRepeating("ReadCameraLookInput", 0, 1.0f/60.0f);
    }

    //void Update()
    //{
    //    if (setupComplete)
    //    {
    //        ReadDriveInput();
    //        ReadServoInput();
    //    }
    //}

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
        servoSelectUpAction = inputMap.FindAction("ServoSelectUp");
        servoSelectDownAction = inputMap.FindAction("ServoSelectDown");

        SetupAction(ref servoSelectUpAction, "ServoSelectUp", OnServoSelectUp);
        SetupAction(ref servoSelectDownAction, "ServoSelectDown", OnServoSelectDown);

        setupComplete = true;
    }

    void SetupAction(ref InputAction action, string actionName, Action<InputAction.CallbackContext> handler)
    {
        action = input.FindAction(actionName);
        if (action != null)
        {
            //register handlers
            action.performed += handler;
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

    private void OnServoSelectUp(InputAction.CallbackContext context)
    {
        if (selectedServo < 4)
        {
            selectedServo++;
            Debug.Log("Selected Servo: " + selectedServo);
        }
    }

    private void OnServoSelectDown(InputAction.CallbackContext context)
    {
        if (selectedServo > 1 )
        {
            selectedServo--;
            Debug.Log("Selected Servo: " + selectedServo);
        }
    }

    private void ReadServoInput()
    {
        if (!setupComplete) return;
        var vector = lookAction.ReadValue<UnityEngine.Vector2>();
        short xPulseWidthDelta = (short)((vector.x * servoDirectionCorrection[0]) * pulseWidthMaxDeltaValues[0]);
        short yPulseWidthDelta = (short)((vector.y * servoDirectionCorrection[selectedServo]) * pulseWidthMaxDeltaValues[selectedServo]);
        if (connected && (xPulseWidthDelta != 0 || yPulseWidthDelta != 0))
        {
            //slewing servo
            if (xPulseWidthDelta < 0 && currentServoValues[0] > 500)
            {
                currentServoValues[0] += xPulseWidthDelta;
                if (currentServoValues[0] < 500)
                    currentServoValues[0] = 500;
            }
            else if (xPulseWidthDelta > 0 && currentServoValues[0] < 2500)
            {
                currentServoValues[0] += xPulseWidthDelta;
                if (currentServoValues[0] > 2500)
                    currentServoValues[0] = 2500;
            }

            lookAngle = (currentServoValues[0] - 1500) / 1000.0f;

            //arm servos
            if (yPulseWidthDelta < 0 && currentServoValues[selectedServo] > 500)
            {
                currentServoValues[selectedServo] += yPulseWidthDelta;
                if (currentServoValues[selectedServo] < 500)
                    currentServoValues[selectedServo] = 500;
            }
            else if (yPulseWidthDelta > 0 && currentServoValues[selectedServo] < 2500)
            {
                currentServoValues[selectedServo] += yPulseWidthDelta;
                if (currentServoValues[selectedServo] > 2500)
                    currentServoValues[selectedServo] = 2500;
            }




            var servoSignal = new ServoSignal(currentServoValues);
            RTCDataChannel.Send(servoSignal.GetBytes());
            Debug.Log("y: " + vector.y);
            Debug.Log("x: " + vector.x);
            Debug.Log(currentServoValues[0] + " - " + currentServoValues[1] + " - " + currentServoValues[2] + " - " + currentServoValues[3] + " - " + currentServoValues[4]);
            Debug.Log("");
        }
    }

    private void ReadCameraLookInput()
    {
        //var vector = cameraLook.ReadValue<UnityEngine.Vector2>();
        var vector = lookAction.ReadValue<UnityEngine.Vector2>();
        var pulseWidth0 = 500 + (2000 - ((vector.x + 1) * 1000));
        leftTrackText.text = ((short)pulseWidth0).ToString();
        if (connected && pulseWidth0 != lastPulseWidthX)
        {
            lookAngle = vector.x;
            Debug.Log("Look Angle: " + lookAngle);
            lastPulseWidthX = pulseWidth0;
            var cameraLookSignal = new ServoSignal((short)pulseWidth0, 1250, 1250, 1250, 1250);
            RTCDataChannel.Send(cameraLookSignal.GetBytes());
        }
    }

    private void ReadDriveInput()
    {
        if (!setupComplete) return;
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
