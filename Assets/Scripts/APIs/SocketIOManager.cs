using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using DG.Tweening;
using System.Linq;
using Newtonsoft.Json;
using Best.SocketIO;
using Best.SocketIO.Events;
using Newtonsoft.Json.Linq;
using System.Runtime.Serialization;
using Best.HTTP.Shared;

public class SocketIOManager : MonoBehaviour
{
    [SerializeField]
    private SlotBehaviour slotManager;

    [SerializeField]
    private UIManager uiManager;

    internal GameData initialData = null;
    internal UiData initUIData = null;
    internal Root initPaydata = null;
    internal Root resultData = null;
    internal Player playerdata = null;
    [SerializeField]
    internal List<string> bonusdata = null;
    //WebSocket currentSocket = null;
    internal bool isResultdone = false;
    // protected string nameSpace="game"; //BackendChanges
    private Socket gameSocket; //BackendChanges

    private SocketManager manager;

    [SerializeField]
    internal JSHandler _jsManager;

    protected string SocketURI = null;
    // protected string TestSocketURI = "https://game-crm-rtp-backend.onrender.com/";

    [SerializeField]
    protected string TestSocketURI = "http://localhost:5001/";

    [SerializeField]
    private string testToken;

    protected string gameID = "SL-LS";
    [SerializeField] internal JSFunctCalls JSManager;
    //protected string gameID = "";

    internal bool isLoaded = false;

    internal bool SetInit = false;

    private const int maxReconnectionAttempts = 6;
    private readonly TimeSpan reconnectionDelay = TimeSpan.FromSeconds(10);

    protected string nameSpace = "playground"; //BackendChanges

    private bool isConnected = false; //Back2 Start
    private bool hasEverConnected = false;
    private const int MaxReconnectAttempts = 5;
    private const float ReconnectDelaySeconds = 2f;

    private float lastPongTime = 0f;
    private float pingInterval = 2f;
    private float pongTimeout = 3f;
    private bool waitingForPong = false;
    private int missedPongs = 0;
    private const int MaxMissedPongs = 5;
    private Coroutine PingRoutine; //Back2 end

    // protected string nameSpace = "game";
    private void Start()
    {
        // Debug.unityLogger.logEnabled = false;
        OpenSocket();
    }

    void ReceiveAuthToken(string jsonData)
    {
        Debug.Log("Received data: " + jsonData);
        // Do something with the authToken
        var data = JsonUtility.FromJson<AuthTokenData>(jsonData);
        SocketURI = data.socketURL;
        myAuth = data.cookie;
        nameSpace = data.nameSpace;
    }

    string myAuth = null;



    private void Awake()
    {
        isLoaded = false;
    }

    private void OpenSocket()
    {
        SocketOptions options = new SocketOptions(); //Back2 Start
        options.AutoConnect = false;
        options.Reconnection = false;
        options.Timeout = TimeSpan.FromSeconds(3); //Back2 end
        options.ConnectWith = Best.SocketIO.Transports.TransportTypes.WebSocket;

#if UNITY_WEBGL && !UNITY_EDITOR
            JSManager.SendCustomMessage("authToken");
            StartCoroutine(WaitForAuthToken(options));
#else
        Func<SocketManager, Socket, object> authFunction = (manager, socket) =>
        {
            return new
            {
                token = testToken
            };
        };
        options.Auth = authFunction;
        // Proceed with connecting to the server
        SetupSocketManager(options);
#endif
    }

    private IEnumerator WaitForAuthToken(SocketOptions options)
    {
        // Wait until myAuth is not null
        while (myAuth == null)
        {
            Debug.Log("My Auth is null");
            yield return null;
        }
        while (SocketURI == null)
        {
            Debug.Log("My Socket is null");
            yield return null;
        }

        Debug.Log("My Auth is not null");
        // Once myAuth is set, configure the authFunction
        Func<SocketManager, Socket, object> authFunction = (manager, socket) =>
        {
            return new
            {
                token = myAuth
            };
        };
        options.Auth = authFunction;

        Debug.Log("Auth function configured with token: " + myAuth);

        // Proceed with connecting to the server
        SetupSocketManager(options);
    }

    private void SetupSocketManager(SocketOptions options)
    {
#if UNITY_EDITOR
        // Create and setup SocketManager for Testing
        this.manager = new SocketManager(new Uri(TestSocketURI), options);
#else
        // Create and setup SocketManager
        this.manager = new SocketManager(new Uri(SocketURI), options);
#endif
        if (string.IsNullOrEmpty(nameSpace) | string.IsNullOrWhiteSpace(nameSpace))
        {
            gameSocket = this.manager.Socket;
        }
        else
        {
            Debug.Log("Namespace used :" + nameSpace);
            gameSocket = this.manager.GetSocket("/" + nameSpace);
        }
        // Set subscriptions
        gameSocket.On<ConnectResponse>(SocketIOEventTypes.Connect, OnConnected);
        gameSocket.On(SocketIOEventTypes.Disconnect, OnDisconnected); //Back2 Start
        gameSocket.On<Error>(SocketIOEventTypes.Error, OnError);
        gameSocket.On<string>("game:init", OnListenEvent);
        gameSocket.On<string>("result", OnResult);
        //gameSocket.On<string>("gamble:result", OnGameResult);
        //gameSocket.On<string>("bonus:result", OnBonusResult);
        gameSocket.On<bool>("socketState", OnSocketState);
        gameSocket.On<string>("internalError", OnSocketError);
        gameSocket.On<string>("alert", OnSocketAlert);
        gameSocket.On<string>("pong", OnPongReceived); //Back2 Start
        gameSocket.On<string>("AnotherDevice", OnSocketOtherDevice); //BackendChanges Finish
        manager.Open();
    }

    void OnBonusResult(string data)
    {
        // Handle the game result here
        Debug.Log("Bonus Result: " + data);

        ParseResponse(data);

    }
    // Connected event handler implementation
    void OnConnected(ConnectResponse resp) //Back2 Start
    {
        Debug.Log("✅ Connected to server.");

        if (hasEverConnected)
        {
            uiManager.CheckAndClosePopups();
        }

        isConnected = true;
        hasEverConnected = true;
        waitingForPong = false;
        missedPongs = 0;
        lastPongTime = Time.time;
        SendPing();
    } //Back2 end
    private void OnError(Error err)
    {
        Debug.LogError("Socket Error Message: " + err);
#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("error");
#endif
    }
    private void OnDisconnected() //Back2 Start
    {
        Debug.LogWarning("⚠️ Disconnected from server.");
        uiManager.DisconnectionPopup();
        isConnected = false;
        ResetPingRoutine();
    } //Back2 end
    private void OnPongReceived(string data) //Back2 Start
    {
        Debug.Log("✅ Received pong from server.");
        waitingForPong = false;
        missedPongs = 0;
        lastPongTime = Time.time;
        Debug.Log($"⏱️ Updated last pong time: {lastPongTime}");
        Debug.Log($"📦 Pong payload: {data}");
    } //Back2 end

    private void OnError(string response)
    {
        Debug.LogError("Error: " + response);
    }

    private void OnListenEvent(string data)
    {
        Debug.Log("Received some_event with data: " + data);
        ParseResponse(data);
    }
    void OnResult(string data)
    {
        print(data);
        ParseResponse(data);
    }
    private void OnSocketState(bool state)
    {
        if (state)
        {
            Debug.Log("my state is " + state);
            //InitRequest("AUTH");
        }
    }

    public void CloseGame()
    {
        Debug.Log("Unity: Closing Game");
        StartCoroutine(CloseSocket());
    }
    private void OnSocketError(string data)
    {
        Debug.Log("Received error with data: " + data);
    }
    private void OnSocketAlert(string data)
    {
        Debug.Log("Received alert with data: " + data);
    }

    private void OnSocketOtherDevice(string data)
    {
        Debug.Log("Received Device Error with data: " + data);
        uiManager.ADfunction();
    }

    private void SendPing() //Back2 Start
    {
        ResetPingRoutine();
        PingRoutine = StartCoroutine(PingCheck());
    }
    void ResetPingRoutine()
    {
        if (PingRoutine != null)
        {
            StopCoroutine(PingRoutine);
        }
        PingRoutine = null;
    }

    private void AliveRequest()
    {
        SendDataWithNamespace("YES I AM ALIVE");
    }
    private IEnumerator PingCheck()
    {
        while (true)
        {
            Debug.Log($"🟡 PingCheck | waitingForPong: {waitingForPong}, missedPongs: {missedPongs}, timeSinceLastPong: {Time.time - lastPongTime}");

            if (missedPongs == 0)
            {
                uiManager.CheckAndClosePopups();
            }

            // If waiting for pong, and timeout passed
            if (waitingForPong)
            {
                if (missedPongs == 2)
                {
                    uiManager.ReconnectionPopup();
                }
                missedPongs++;
                Debug.LogWarning($"⚠️ Pong missed #{missedPongs}/{MaxMissedPongs}");

                if (missedPongs >= MaxMissedPongs)
                {
                    Debug.LogError("❌ Unable to connect to server — 5 consecutive pongs missed.");
                    isConnected = false;
                    uiManager.DisconnectionPopup();
                    yield break;
                }
            }

            // Send next ping
            waitingForPong = true;
            lastPongTime = Time.time;
            Debug.Log("📤 Sending ping...");
            SendDataWithNamespace("ping");
            yield return new WaitForSeconds(pingInterval);
        }
    } //Back2 end

    private void SendDataWithNamespace(string eventName, string json = null)
    {
        // Send the message
        if (gameSocket != null && gameSocket.IsOpen)
        {
            if (json != null)
            {
                gameSocket.Emit(eventName, json);
                Debug.Log("JSON data sent: " + json);
            }
            else
            {
                gameSocket.Emit(eventName);
            }
        }
        else
        {
            Debug.LogWarning("Socket is not connected.");
        }
    }
    public void ExtractUrlAndToken(string fullUrl)
    {
        Uri uri = new Uri(fullUrl);
        string query = uri.Query; // Gets the query part, e.g., "?url=http://localhost:5000&token=e5ffa84216be4972a85fff1d266d36d0"

        Dictionary<string, string> queryParams = new Dictionary<string, string>();
        string[] pairs = query.TrimStart('?').Split('&');

        foreach (string pair in pairs)
        {
            string[] kv = pair.Split('=');
            if (kv.Length == 2)
            {
                queryParams[kv[0]] = Uri.UnescapeDataString(kv[1]);
            }
        }

        if (queryParams.TryGetValue("url", out string extractedUrl) &&
            queryParams.TryGetValue("token", out string token))
        {
            Debug.Log("Extracted URL: " + extractedUrl);
            Debug.Log("Extracted Token: " + token);
            testToken = token;
            SocketURI = extractedUrl;
        }
        else
        {
            Debug.LogError("URL or token not found in query parameters.");
        }
    }

    private void PopulateSlotSocket(List<string> LineIds)
    {
        slotManager.shuffleInitialMatrix();
        for (int i = 0; i < LineIds.Count; i++)
        {
            slotManager.FetchLines(LineIds[i], i);
        }

        slotManager.SetInitialUI();

        isLoaded = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        JSManager.SendCustomMessage("OnEnter");
#endif
        uiManager.RaycastBlocker.SetActive(false);
    }

    internal IEnumerator CloseSocket() //Back2 Start
    {
        uiManager.RaycastBlocker.SetActive(true);
        ResetPingRoutine();

        Debug.Log("Closing Socket");

        manager?.Close();
        manager = null;

        Debug.Log("Waiting for socket to close");

        yield return new WaitForSeconds(0.5f);

        Debug.Log("Socket Closed");

#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("OnExit"); //Telling the react platform user wants to quit and go back to homepage
#endif
    } //Back2 end

    internal void closeSocketReactnativeCall()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        JSManager.SendCustomMessage("OnExit");
#endif
    }
    private void CloseSocketMesssage(string eventName)
    {
        SendDataWithNamespace("EXIT");
    }

    private void ParseResponse(string jsonObject)
    {
        Root myData = JsonConvert.DeserializeObject<Root>(jsonObject);

        string id = myData.id;

        switch (id)
        {
            case "initData":
                {
                    initialData = myData.gameData;
                    initUIData = myData.uiData;
                    playerdata = myData.player;
                    initPaydata = myData;


                    if (!SetInit)
                    {
                        //Debug.Log(jsonObject);
                        List<string> LinesString = ConvertListListIntToListString(initialData.lines);
                        //List<string> InitialReels = ConvertListOfListsToStrings(InitialData.Reel);
                        //InitialReels = RemoveQuotes(InitialReels);
                        PopulateSlotSocket(LinesString);
                        SetInit = true;
                    }
                    else
                    {
                        RefreshUI();
                    }
                    break;
                }
            case "ResultData":
                {
                    // Debug.Log(jsonObject);
                    // myData.message.GameData.FinalResultReel = ConvertListOfListsToStrings(myData.message.GameData.ResultReel);
                    // myData.message.GameData.FinalsymbolsToEmit = TransformAndRemoveRecurring(myData.message.GameData.symbolsToEmit);
                    // resultData = myData.message.GameData;
                    resultData = myData;
                    playerdata = myData.player;
                    isResultdone = true;
                    break;
                }
            case "ExitUser":
                {
                    if (gameSocket != null) //BackendChanges
                    {
                        Debug.Log("Dispose my Socket");
                        this.manager.Close();
                    }
                    //  Application.ExternalCall("window.parent.postMessage", "onExit", "*");
#if UNITY_WEBGL && !UNITY_EDITOR
                        JSManager.SendCustomMessage("OnExit");
#endif
                    break;

                }
        }
    }

    private void RefreshUI()
    {
        uiManager.InitialiseUIData(initUIData.paylines);
    }

    private void PopulateSlotSocket(List<string> slotPop, List<string> LineIds)
    {
        slotManager.shuffleInitialMatrix();
        for (int i = 0; i < LineIds.Count; i++)
        {
            slotManager.FetchLines(LineIds[i], i);
        }

        slotManager.SetInitialUI();

        isLoaded = true;
        // Application.ExternalCall("window.parent.postMessage", "OnEnter", "*");
#if UNITY_WEBGL && !UNITY_EDITOR
        JSManager.SendCustomMessage("OnEnter");
#endif
    }

    internal void AccumulateResult(double currBet)
    {
        isResultdone = false;
        MessageData message = new MessageData();
        message.payload = new SentDeta();
        message.type = "SPIN";
        Debug.Log(slotManager.BetCounter);
        message.payload.betIndex = slotManager.BetCounter;
        // Serialize message data to JSON
        string json = JsonUtility.ToJson(message);
        SendDataWithNamespace("request", json);
    }

    private List<string> RemoveQuotes(List<string> stringList)
    {
        for (int i = 0; i < stringList.Count; i++)
        {
            stringList[i] = stringList[i].Replace("\"", ""); // Remove inverted commas
        }
        return stringList;
    }

    private List<string> ConvertListListIntToListString(List<List<int>> listOfLists)
    {
        List<string> resultList = new List<string>();

        foreach (List<int> innerList in listOfLists)
        {
            // Convert each integer in the inner list to string
            List<string> stringList = new List<string>();
            foreach (int number in innerList)
            {
                stringList.Add(number.ToString());
            }

            // Join the string representation of integers with ","
            string joinedString = string.Join(",", stringList.ToArray()).Trim();
            resultList.Add(joinedString);
        }

        return resultList;
    }

    private List<string> ConvertListOfListsToStrings(List<List<string>> inputList)
    {
        List<string> outputList = new List<string>();

        foreach (List<string> row in inputList)
        {
            string concatenatedString = string.Join(",", row);
            outputList.Add(concatenatedString);
        }

        return outputList;
    }

    private List<string> TransformAndRemoveRecurring(List<List<string>> originalList)
    {
        // Flattened list
        List<string> flattenedList = new List<string>();
        foreach (List<string> sublist in originalList)
        {
            flattenedList.AddRange(sublist);
        }

        // Remove recurring elements
        HashSet<string> uniqueElements = new HashSet<string>(flattenedList);

        // Transformed list
        List<string> transformedList = new List<string>();
        foreach (string element in uniqueElements)
        {
            transformedList.Add(element.Replace(",", ""));
        }

        return transformedList;
    }
}


[Serializable]
public class SentDeta
{
    public int betIndex;
    public string Event;
    public double lastWinning;
    public int index;
}
[Serializable]
public class MessageData
{
    public string type;

    public SentDeta payload;

}


public class Features
{
    public bool isJackpot { get; set; }
    public int jackpotMultiplier { get; set; }
    public AnyPayout anyPayout { get; set; }
}

public class AnyPayout
{
    public double seven { get; set; }
    public double orange { get; set; }
    public double gray { get; set; }
    public double blue { get; set; }
    public double bar { get; set; }
}
public class GameData
{
    public List<List<int>> lines { get; set; }
    public List<double> bets { get; set; }
}

public class LargeWheelFeature
{
    public List<int> featureValues { get; set; }
    public List<double> featureProbs { get; set; }
}

public class MediumWheelFeature
{
    public List<int> featureValues { get; set; }
    public List<double> featureProbs { get; set; }
}

public class Paylines
{
    public List<Symbol> symbols { get; set; }
}

public class Player
{
    public double balance { get; set; }
}

public class Root
{
    public string id { get; set; }
    public GameData gameData { get; set; }
    public Features features { get; set; }
    public UiData uiData { get; set; }
    public Player player { get; set; }

    // resultdata

    public bool success { get; set; }
    public List<List<string>> matrix { get; set; }
    public Payload payload { get; set; }

}
public class Bats
{
}



public class FreeSpin
{
}

public class LineWin
{
    public int lineIndex { get; set; }
    public List<string> positions { get; set; }
}

public class Payload
{
    public double winAmount { get; set; }
    public List<LineWin> lineWins { get; set; }
}




public class SmallWheelFeature
{
    public List<int> featureValues { get; set; }
    public List<double> featureProbs { get; set; }
}
public class ReelsInstance
{
    [JsonProperty("0")]
    public int _0 { get; set; }

    [JsonProperty("1")]
    public int _1 { get; set; }

    [JsonProperty("2")]
    public int _2 { get; set; }
}
public class Symbol
{
    public int id { get; set; }
    public string name { get; set; }
    public ReelsInstance reelsInstance { get; set; }
    public double payout { get; set; }
    public List<object> multiplier { get; set; }
}

public class UiData
{
    public Paylines paylines { get; set; }
}

[Serializable]
public class AuthTokenData
{
    public string cookie;
    public string socketURL;
    public string nameSpace; //BackendChanges
}
// [Serializable]
// public class BetData
// {
//     public double currentBet;
//     public double currentLines;
//     public double spins;
// }

// [Serializable]
// public class AuthData
// {
//     public string GameID;
//     //public double TotalLines;
// }

// [Serializable]
// public class MessageData
// {
//     public BetData data;
//     public string id;
// }

// [Serializable]
// public class ExitData
// {
//     public string id;
// }

// [Serializable]
// public class InitData
// {
//     public AuthData Data;
//     public string id;
// }

// [Serializable]
// public class AbtLogo
// {
//     public string logoSprite { get; set; }
//     public string link { get; set; }
// }

// [Serializable]
// public class GameData
// {
//     public List<List<string>> Reel { get; set; }
//     public List<List<int>> linesApiData { get; set; }
//     public List<Paytable> paytable { get; set; }
//     public int jackpotMultiplier { get; set; }
//     public List<double> Bets { get; set; }
//     public bool canSwitchLines { get; set; }
//     public List<int> LinesCount { get; set; }
//     public List<int> autoSpin { get; set; }
//     public List<List<string>> ResultReel { get; set; }
//     public List<int> linesToEmit { get; set; }
//     public List<List<string>> symbolsToEmit { get; set; }
//     public double WinAmout { get; set; }
//     public FreeSpins freeSpins { get; set; }
//     public List<string> FinalsymbolsToEmit { get; set; }
//     public List<string> FinalResultReel { get; set; }
//     public double jackpot { get; set; }
//     public bool isBonus { get; set; }
//     public double BonusStopIndex { get; set; }
// }

// [Serializable]
// public class FreeSpins
// {
//     public int count { get; set; }
//     public bool isNewAdded { get; set; }
// }

// [Serializable]
// public class Paytable
// {
//     public List<string> combination { get; set; }
//     public float payout { get; set; }
// }

// [Serializable]
// public class Message
// {
//     public GameData GameData { get; set; }
//     public UIData UIData { get; set; }
//     public PlayerData PlayerData { get; set; }
//     public List<string> BonusData { get; set; }
// }

// [Serializable]
// public class Root
// {
//     public string id { get; set; }
//     public Message message { get; set; }
// }

// [Serializable]
// public class UIData
// {
//     public Paylines paylines { get; set; }
//     public List<string> spclSymbolTxt { get; set; }
//     public AbtLogo AbtLogo { get; set; }
//     public string ToULink { get; set; }
//     public string PopLink { get; set; }
// }

// [Serializable]
// public class Paylines
// {
//     public List<Symbol> symbols { get; set; }
// }

// [Serializable]
// public class Symbol
// {
//     public int ID { get; set; }
//     public string Name { get; set; }
//     [JsonProperty("multiplier")]
//     public object MultiplierObject { get; set; }

//     // This property will hold the properly deserialized list of lists of integers
//     [JsonIgnore]
//     public List<List<int>> Multiplier { get; private set; }

//     // Custom deserialization method to handle the conversion
//     [OnDeserialized]
//     internal void OnDeserializedMethod(StreamingContext context)
//     {
//         // Handle the case where multiplier is an object (empty in JSON)
//         if (MultiplierObject is JObject)
//         {
//             Multiplier = new List<List<int>>();
//         }
//         else
//         {
//             // Deserialize normally assuming it's an array of arrays
//             Multiplier = JsonConvert.DeserializeObject<List<List<int>>>(MultiplierObject.ToString());
//         }
//     }
//     public object defaultAmount { get; set; }
//     public object symbolsCount { get; set; }
//     public object increaseValue { get; set; }
//     public object description { get; set; }
//     public int freeSpin { get; set; }
// }
// [Serializable]
// public class PlayerData
// {
//     public double Balance { get; set; }
//     public double haveWon { get; set; }
//     public double currentWining { get; set; }
// }
// [Serializable]
// public class AuthTokenData
// {
//     public string cookie;
//     public string socketURL;
//     public string nameSpace; //BackendChanges
// }


