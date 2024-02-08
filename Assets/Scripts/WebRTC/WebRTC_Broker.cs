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
        //disconnectButton.onClick.AddListener(() => { Hangup(); });
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
        callButton.interactable = true;
        disconnectButton.interactable = false;

        callerOnIceConnectionChange = state => { LogState(state); };
        callerOnIceCandidate = candidate => { OnIceCandidate(candidate); };

        onDataChannelMessage = bytes => 
        {
            textReceive.text = System.Text.Encoding.UTF8.GetString(bytes);
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
                var h264Codecs = codecs.Where(codec => codec.mimeType == "video/H264");
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
