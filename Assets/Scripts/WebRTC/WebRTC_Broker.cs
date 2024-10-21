using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.WebRTC;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
//using UnityEditor.PackageManager.Requests;
using TMPro;
//using UnityEditor.PackageManager;
using System.Net;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Drawing;
using static UnityEngine.EventSystems.EventTrigger;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Color = UnityEngine.Color;

//public struct LidarDataCoordinate
//{
//    float x;
//    float y;
//    bool isEnd;
//}

//public class LidarDataSignal : Signal
//{
//    public LidarDataCoordinate[] LidarData = new LidarDataCoordinate[8192];
//}

public class WebRTC_Broker : MonoBehaviour
{
#pragma warning disable 0649
    [SerializeField] public TMP_Text debug;
    [SerializeField] private Button callButton;
    [SerializeField] private Button disconnectButton;
    //[SerializeField] private Button sendButton;
    [SerializeField] private TextMeshProUGUI textSend;
    [SerializeField] private RawImage frame;
    [SerializeField] public TMP_InputField textReceive;
    [SerializeField] public Text text;
    [SerializeField] public InputHandler inputHandler;
    public RawImage graph;
    public RawImage graphBackground;
    public Texture2D graphBackgroundTexture;
    public Texture2D graphTextureTemplate;
    public Texture2D graphTexture;
    public float rotationSpeed = 200.0f;
    public Texture2D armTexture; 
    public Texture2D armTextureBackground; 
    public Texture2D armImageTemplate;
    public RawImage armImage;
    public RawImage armImageBackground;
#pragma warning restore 0649

    private RTCPeerConnection caller;
    private RTCDataChannel dataChannel;
    private DelegateOnIceConnectionChange callerOnIceConnectionChange;
    private MediaStream receiveAudioStream, receiveVideoStream;
    private DelegateOnIceCandidate callerOnIceCandidate;
    private DelegateOnMessage onDataChannelMessage;
    private DelegateOnOpen onDataChannelOpen;
    private DelegateOnClose onDataChannelClose;
    private DelegateOnDataChannel onDataChannel;
    private VideoStreamTrack videoStreamTrack;
    private TableStorageRequestHandler tableStorageRequestHandler;
    private LidarHandler lidarHandler;
    private DelegateOnTrack onTrack;
    private List<RTCIceCandidate> rtcIceCandidates;
    private RTCSessionDescription testDesc;
    private MovementSignal message;
    private TableEntry candidateResponse;
    private string candidateList;
    private void Awake()
    {
        WebRTC.Initialize();
        callButton.onClick.AddListener(() => { StartCoroutine(Call()); });
        //sendButton.onClick.AddListener(() => { dataChannel.Send(textSend.text); });
        //message = new MovementSignal(127,127);
        //sendButton.onClick.AddListener(() => 
        //{
        //    //var temp = BitConverter.GetBytes(message.id);
        //    dataChannel.Send(message.GetBytes()) ; 
        //});
        disconnectButton.onClick.AddListener(() => { Disconnect(); });
    }

    public static byte[] ObjectToByteArray(Signal obj)
    {
        BinaryFormatter bf = new BinaryFormatter();
        using (var ms = new MemoryStream())
        {
            bf.Serialize(ms, obj);
            return ms.ToArray();
        }
    }

    private void Update()
    {
        graph.transform.rotation = Quaternion.RotateTowards(graph.transform.rotation, Quaternion.Euler(0, 0, inputHandler.lookAngle * 90), rotationSpeed * Time.deltaTime);

        drawArmPosition();
    }

    private void Start()
    {
        receiveVideoStream = new MediaStream();
        receiveVideoStream.OnAddTrack = e =>
        {
            if (e.Track is VideoStreamTrack video)
            {
                videoStreamTrack = video;
 
                if (videoStreamTrack.Texture)
                {
                    frame.texture = video.Texture;
                }

                videoStreamTrack.OnVideoReceived += tex =>
                {
                    Debug.Log("Video Received");
                    frame.texture = tex;
                };
            }
        };
        rtcIceCandidates = new List<RTCIceCandidate>();
        tableStorageRequestHandler = gameObject.AddComponent<TableStorageRequestHandler>();
        lidarHandler = GetComponent<LidarHandler>();
        callButton.interactable = true;
        disconnectButton.interactable = false;

        callerOnIceConnectionChange = state => { LogState(state); };
        callerOnIceCandidate = candidate => { OnIceCandidate(candidate); };

        onDataChannelMessage = bytes => 
        {
            //Calls lidar handler to copy the data to stage it for drawing
            lidarHandler.SetLidarData(bytes);
        };
        onDataChannelOpen = () =>
        {
            //sendButton.interactable = true;
            disconnectButton.interactable = true;
            inputHandler.RTCDataChannel = dataChannel;
            inputHandler.connected = true;
            inputHandler.Setup();
        };
        onDataChannelClose = () =>
        {
            //sendButton.interactable = false;
            callButton.interactable = true;
            disconnectButton.interactable = false;

            inputHandler.connected = false;
            inputHandler.RTCDataChannel = null;
        };
    }

    // drawArmPosition method is called in Update() to draw the arm position based on the inputHandler's lookAngle for currentServoValues[1], currentServoValues[2], and currentServoValues[3].
    // uses armSegmentLength to determine the length of each segment of the arm. Uses 0,1,2 Index
    // uses armSegmentAngleRange to determine the range of angles that each segment can rotate within. Uses 0,1,2 Index
    // uses armSegmentAngleOffset to determine the offset of the angle of each segment. Uses 0,1,2 Index
    // uses servoDirectionCorrection to determine the direction of the servo rotation. Uses index 1,2,3.
    private void drawArmPosition()
    {
        var baseColor = new Color32(0, 0, 0, 0);
        if (armImageTemplate == null)
        {
            armImageTemplate = new Texture2D(300, 300);
            for (int i = 0; i < armImageTemplate.width; i++)
            {
                for (int j = 0; j < armImageTemplate.height; j++)
                {
                    armImageTemplate.SetPixel(i, j, baseColor);
                }
            }

            armTexture = new Texture2D(armImageTemplate.width, armImageTemplate.height);
            armTextureBackground = new Texture2D(armImageTemplate.width, armImageTemplate.height);
        }

        // Ensure inputHandler is set
        if (inputHandler == null) return;

        // Get the servo angles
        float baseAngle = inputHandler.currentServoValues[1];
        float midAngle = inputHandler.currentServoValues[2];
        float endAngle = inputHandler.currentServoValues[3];

        // Convert pulse width to angles within the specified range
        baseAngle = Mathf.Lerp(0, inputHandler.armSegmentAngleRange[0], (baseAngle - 500) / 2000.0f) 
            + inputHandler.armSegmentAngleOffset[0];
        midAngle =(Mathf.Lerp(0, inputHandler.armSegmentAngleRange[1], (midAngle - 500) / 2000.0f) 
            + inputHandler.armSegmentAngleOffset[0] 
            + inputHandler.armSegmentAngleOffset[1])*-1;
        endAngle = (Mathf.Lerp(0, inputHandler.armSegmentAngleRange[2], (endAngle - 500) / 2000.0f) 
            + inputHandler.armSegmentAngleOffset[0] 
            + inputHandler.armSegmentAngleOffset[1] 
            + inputHandler.armSegmentAngleOffset[2])*1;

        // Calculate the positions of each segment
        Vector2 basePosition = new Vector2(armTexture.width / 2, armTexture.height / 2);
        Vector2 midPosition = basePosition + new Vector2(
            inputHandler.armSegmentLength[0] * Mathf.Cos(baseAngle * Mathf.Deg2Rad),
            inputHandler.armSegmentLength[0] * Mathf.Sin(baseAngle * Mathf.Deg2Rad)
        );
        Vector2 endPosition = midPosition + new Vector2(
            inputHandler.armSegmentLength[1] * Mathf.Cos((baseAngle + midAngle) * Mathf.Deg2Rad),
            inputHandler.armSegmentLength[1] * Mathf.Sin((baseAngle + midAngle) * Mathf.Deg2Rad)
        );
        Vector2 tipPosition = endPosition + new Vector2(
            inputHandler.armSegmentLength[2] * Mathf.Cos((baseAngle + midAngle + endAngle) * Mathf.Deg2Rad),
            inputHandler.armSegmentLength[2] * Mathf.Sin((baseAngle + midAngle + endAngle) * Mathf.Deg2Rad)
        );

        // Clear the texture
        Color32[] resetColorArray = armTexture.GetPixels32();
        for (int i = 0; i < resetColorArray.Length; i++)
        {
            resetColorArray[i] = new Color(0, 0, 0, 0); // Transparent
        }
        armTexture.SetPixels32(resetColorArray);

        // Draw the arm segments
        DrawLine(basePosition - new Vector2(-25,0), basePosition - new Vector2(25, 0), Color.blue, 10);
        DrawLine(basePosition, midPosition, inputHandler.selectedServo == 1 ? Color.green : Color.red, 10);
        DrawLine(midPosition, endPosition, inputHandler.selectedServo == 2 ? Color.green : Color.red, 10);
        DrawLine(endPosition, tipPosition, inputHandler.selectedServo == 3 ? Color.green : Color.red, 10);

        // Apply the changes to the texture
        armTexture.Apply();
        armTextureBackground.Apply();
        armImageBackground.texture = armTextureBackground;
        // Update the RawImage component
        armImage.texture = armTexture;
    }


    private void DrawLine(Vector2 start, Vector2 end, Color color, int lineWidth)
    {
        int x0 = (int)start.x;
        int y0 = (int)start.y;
        int x1 = (int)end.x;
        int y1 = (int)end.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            DrawThickPixel(x0, y0, color, lineWidth);

            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private void DrawThickPixel(int x, int y, Color color, int lineWidth)
    {
        int halfWidth = lineWidth / 2;
        for (int i = -halfWidth; i <= halfWidth; i++)
        {
            for (int j = -halfWidth; j <= halfWidth; j++)
            {
                int drawX = x + i;
                int drawY = y + j;
                if (drawX >= 0 && drawX < armTexture.width && drawY >= 0 && drawY < armTexture.height)
                {
                    armTexture.SetPixel(drawX, drawY, color);
                }
            }
        }
    }

    void Disconnect()
    {
        dataChannel.Close();
    }


    void onMessage()
    {

    }

    IEnumerator Call()
    {
        callButton.interactable = false;
        var configuration = GetSelectedSdpSemantics();
        caller = new RTCPeerConnection(ref configuration);
        caller.OnIceCandidate = callerOnIceCandidate;
        caller.OnIceConnectionChange = callerOnIceConnectionChange;
        candidateResponse = new TableEntry("caller");

        var gfxType = SystemInfo.graphicsDeviceType;
        var format = WebRTC.GetSupportedRenderTextureFormat(gfxType);
        
        // Create a track from the RenderTexture
        var rt = new RenderTexture(380, 480, 0, format);
        var track = new VideoStreamTrack(rt);
        
        caller.AddTrack(track);

        StartCoroutine(WebRTC.Update());

        caller.OnTrack = trackEvent => {
            Debug.Log(trackEvent);
            if (trackEvent.Track.Kind == TrackKind.Video)
            {
                var codecs = RTCRtpReceiver.GetCapabilities(TrackKind.Video).codecs;
                debug.text = "";
                foreach (var codec in codecs)
                {
                    debug.text += codec.mimeType + '\n';
                }
                var h264Codecs = codecs.Where(codec => codec.mimeType == "video/VP8");
                var error = trackEvent.Transceiver.SetCodecPreferences(h264Codecs.ToArray());
                
                if (error != RTCErrorType.None)
                    Debug.LogError("SetCodecPreferences failed");
                receiveVideoStream.AddTrack(trackEvent.Track);
            }
        };

        RTCDataChannelInit conf = new RTCDataChannelInit();
        dataChannel = caller.CreateDataChannel("data", conf);
        dataChannel.OnMessage = onDataChannelMessage;
        dataChannel.OnOpen = onDataChannelOpen;
        dataChannel.OnClose = onDataChannelClose;

        var op = caller.CreateOffer();
        yield return op;

        if (!op.IsError)
        {
            testDesc = op.Desc;
            yield return StartCoroutine(OnCreateOfferSuccess(op.Desc));
        }
        else
        {
            Debug.Log(op.Error);
        }
    }
    IEnumerator OnCreateOfferSuccess(RTCSessionDescription desc) {
        var op = caller.SetLocalDescription(ref desc);
        yield return op;

        CoroutineWithData cd = new CoroutineWithData(this, tableStorageRequestHandler.SendRequest(TableStorageRequestHandler.Verb.PUT, new TableEntry("caller", desc.sdp, "", "calling")).GetEnumerator());
        yield return cd.coroutine;
        (TableEntry, int) putResponse = ((TableEntry, int))cd.result;

        if (putResponse.Item2 == 204 || putResponse.Item2 == 200)
        {
            bool waitingForResponse = true;
            while (waitingForResponse)
            {
                cd = new CoroutineWithData(this, tableStorageRequestHandler.SendRequest(TableStorageRequestHandler.Verb.GET, new TableEntry("answerer")).GetEnumerator());
                yield return cd.coroutine;
                (TableEntry, int) getResponse = ((TableEntry, int))cd.result;
                if (getResponse.Item2 != 404 && getResponse.Item1?.status == "answering")
                {
                    //change state in table storage
                    cd = new CoroutineWithData(this, tableStorageRequestHandler.SendRequest(TableStorageRequestHandler.Verb.PUT, new TableEntry("caller", desc.sdp, candidateList, "connected")).GetEnumerator());
                    yield return cd.coroutine;
                    putResponse = ((TableEntry, int))cd.result;

                    RTCSessionDescription temp2 = new RTCSessionDescription();
                    temp2.sdp = getResponse.Item1.description;
                    temp2.type = RTCSdpType.Answer;
                    op = caller.SetRemoteDescription(ref temp2);
                    yield return op;

                    if (op.IsError)
                    {
                        Debug.LogError($"Error Detail Type: {op.Error.message}");
                    }

                    waitingForResponse = false;
                    var canList = new List<RTCIceCandidateInit>();
                    var stringCanList = getResponse.Item1.candidate.Split('\n');
                    int i = 0;
                    foreach ( var stringCandidate in stringCanList)
                    {
                        var tempCan = new RTCIceCandidateInit();
                        tempCan.candidate = stringCandidate.Substring(2, stringCandidate.Length - 2) + "\r\n";
                        tempCan.sdpMid = i.ToString();
                        tempCan.sdpMLineIndex = i;// ++;
                        canList.Add(tempCan);
                    }
                    
                    foreach(var candidate in canList)
                    {
                        caller.AddIceCandidate(new RTCIceCandidate(candidate));
                    }
                    
                }
                else
                {
                    Debug.Log("Waiting for answer");
                    yield return new WaitForSeconds(1);
                }
            }
        }
    }

    RTCConfiguration GetSelectedSdpSemantics()
    {
        RTCConfiguration config = default;
        config.iceServers = new RTCIceServer[]
        {
            new RTCIceServer { urls = new string[] { "stun:stun.l.google.com:19302" } }
        };
        return config;
    }

    void LogState(RTCIceConnectionState state)
    {
        switch (state)
        {
            case RTCIceConnectionState.New:
                Debug.Log("IceConnectionState: New");
                break;
            case RTCIceConnectionState.Checking:
                Debug.Log("IceConnectionState: Checking");
                break;
            case RTCIceConnectionState.Closed:
                Debug.Log("IceConnectionState: Closed");
                break;
            case RTCIceConnectionState.Completed:
                Debug.Log("IceConnectionState: Completed");
                break;
            case RTCIceConnectionState.Connected:
                Debug.Log("IceConnectionState: Connected");
                break;
            case RTCIceConnectionState.Disconnected:
                Debug.Log("IceConnectionState: Disconnected");
                break;
            case RTCIceConnectionState.Failed:
                Debug.Log("IceConnectionState: Failed");
                break;
            case RTCIceConnectionState.Max:
                Debug.Log("IceConnectionState: Max");
                break;
            default:
                break;
        }
    }


    void OnIceCandidate(RTCIceCandidate candidate)
    {
        StartCoroutine(IOnIceCandidatee(candidate));
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="pc"></param>
    /// <param name="streamEvent"></param>
    IEnumerator IOnIceCandidatee(RTCIceCandidate candidate)
    {
        rtcIceCandidates.Add(candidate);
        if(rtcIceCandidates.Count() == 1)
        {
            yield return new WaitForSeconds(3);
            //var candidateResponse = new TableEntry("caller", testDesc.sdp, "a=" + candidate.Candidate + "\r\n", "calling");
            CoroutineWithData cd;
            candidateList = ""; 
            foreach(var can in rtcIceCandidates)
            {
                if(candidateList == "")
                    candidateList = "a=" + can.Candidate + "\r\n" + '\n';
                else
                    candidateList += "a=" + can.Candidate + "\r\n" + '\n';
            }
            candidateResponse.candidate = candidateList;
            candidateResponse.description = testDesc.sdp;
            candidateResponse.status = "calling";
            yield return cd = new CoroutineWithData(this, tableStorageRequestHandler.SendRequest(TableStorageRequestHandler.Verb.PUT, candidateResponse).GetEnumerator());
            yield return cd.coroutine;
            (TableEntry, int) putResponse = ((TableEntry, int))cd.result;
        }
    }

    private void OnDestroy()
    {
        //sendChannel.Close();
        //receiveChannel.Close();
        //
        //localConnection.Close();
        //remoteConnection.Close();

        WebRTC.Dispose();
    }

}
