using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CommandHead
{
    public static readonly string CONNECTION = "CONNECTION";
    public static readonly string DISCONNECTION = "DISCONNECTION";
    public static readonly string INIT = "INIT";
    public static readonly string ENTER = "ENTER";
    public static readonly string EXIT = "EXIT";
    public static readonly string ROOM_UPDATE = "ROOM UPDATE";
    public static readonly string SYSTEM_MSG = "SYSTEM MSG";
    public static readonly string CHAT = "CHAT";
    public static readonly string GO = "GO";
    public static readonly string COUNT = "COUNT";
    public static readonly string GAME_START = "GAME START";
    public static readonly string GAME_END = "GAME END";
    public static readonly string CREATE = "CREATE";
    public static readonly string HISTORY = "HISTORY";
}

public class SocketClient : MonoBehaviour
{
    public static SocketClient instance;

    public Stack<GameObject> UIStack = new Stack<GameObject>();
    public GameObject loginPanel, lobbyPanel, roomPanel, gamePanel;
    [SerializeField] private InputField ipInput;
    [SerializeField] private InputField portInput;
    [SerializeField] private InputField nickInput;
    public InputField enterInput;
    //public Image myColorImage; //green or red

    [SerializeField] private Text ratingTxt, roomIdTxt, usersInfoTxt;

    private Socket client;
    private IPAddress ipAddr;
    private IPEndPoint endPoint;

    private bool connected;
    private string receivedMsg = "";
    private Queue<string> netBuffer = new Queue<string>();
    private Thread ServerCheckThread;
    private byte[] bytes = new byte[65536];
    private object buffer_lock = new object();

    private bool started;

    //public WinningRate winningRate = new WinningRate();
    //public Room room;

    public Dictionary<int, Player> playerDict = new Dictionary<int, Player>();
    public Player myPlayer;
    public Player otherPlayer = null;
    public Board board;

    public Camera mainCam;
    public GameObject playerPref;

    public int ClientID { get; set; }
    public bool InRoom { get; set; }
    public bool MyTurn { get; set; }
    public bool IsFirst { get; set; }

    public Text[] gameNickTxt;
    public GameObject[] turnMark;
    public Color[] markColor;


    private void Awake()
    {
        Screen.SetResolution(1280, 720, false);
        Application.runInBackground = true;
        instance = this;
        SetUI();
    }

    void SetUI()
    {
        UIStack.Clear();
        gamePanel.SetActive(false);
        roomPanel.SetActive(false);
        lobbyPanel.SetActive(false);
        loginPanel.SetActive(true);
        UIStack.Push(loginPanel);
    }

    private void Start()
    {
        mainCam = Camera.main;
        GameManager.instance.mainCam = mainCam;
    }

    public void StartGame()
    {
        StartServer();
        StartCoroutine(BufferUpdate());
    }

    private void StartServer()
    {
        if(nickInput.text.Trim()=="")
        {
            UIManager.instance.SystemMsgPopup("닉네임 칸이 비어있습니다.");
            return;
        }

        try
        {
            client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            ipAddr = IPAddress.Parse(ipInput.text);
            endPoint = new IPEndPoint(ipAddr, int.Parse(portInput.text));

            client.Connect(endPoint);
            connected = true;
            ServerCheckThread = new Thread(ServerCheck);
            ServerCheckThread.Start();
            ServerSend(nickInput.text, CommandHead.CONNECTION);
        }
        catch(Exception e)
        {
            Debug.Log(e);
        }
    }

    private void ServerCheck()
    {
        while (true)
        {
            try
            {
                client.Receive(bytes, 0, bytes.Length, SocketFlags.None);
                string t = Encoding.UTF8.GetString(bytes);
                t = t.Replace("\0", string.Empty);
                lock (buffer_lock)
                {
                    netBuffer.Enqueue(t);
                }
                Array.Clear(bytes, 0, bytes.Length);
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
            }
        }
    }

    private void ServerSend(string msg, string head = "")
    {
        try
        {
            byte[] sbuff = head != "" ? Encoding.UTF8.GetBytes(head + "#" + msg) : Encoding.UTF8.GetBytes(msg);
            client.Send(sbuff, 0, sbuff.Length, SocketFlags.None);
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }
    }

    public void LayGameCell(int x, int y)
    {
        ServerSend($"{ClientID}#{x}#{y}", CommandHead.GO);
    }

    private IEnumerator BufferUpdate()
    {
        while (connected)
        {
            yield return null;
            BufferSystem();
        }
    }

    private void BufferSystem()
    {
        while (netBuffer.Count != 0)
        {
            receivedMsg = netBuffer.Dequeue();
            int ID;
            if (receivedMsg != "")
            {
                string[] data = receivedMsg.Split('#');
                if (data.Length > 1)
                {
                    switch (data[0])
                    {
                        case "INIT":  //유저의 초기 세팅 (서버로부터 아이디 부여받음)
                            ClientID = int.Parse(data[1]);
                            myPlayer = Instantiate(playerPref, transform).GetComponent<Player>();
                            myPlayer.Init(ClientID, true, nickInput.text);
                            playerDict.Add(ClientID, myPlayer);
                            InsertStack(lobbyPanel);
                            nickInput.text = "";
                            //ratingTxt.text = string.Format("{0}전 {1}승 {2}패 {3}무", winningRate.Sum, winningRate.win, winningRate.lose, winningRate.draw);
                            break;

                        case "CONNECTION":
                            ID = int.Parse(data[1]);
                            if (ID != ClientID)
                            {
                                Player p = Instantiate(playerPref, transform).GetComponent<Player>();
                                p.Init(ID, false, data[2]);
                                playerDict.Add(ID, p);
                            }
                            break;

                        case "CREATE":
                            InRoom = true;
                            roomIdTxt.text = "방 아이디: " + data[1];
                            //myPlayer.currentRoomID = int.Parse(data[1]);
                            UpdateRoom();
                            InsertStack(roomPanel);
                            break;

                        case "ENTER":
                            InRoom = true;
                            ID = int.Parse(data[1]);
                            otherPlayer = ID != ClientID ? playerDict[ID] : playerDict[int.Parse(data[2])];
                            UpdateRoom();
                            if(ID==ClientID)
                            {
                                roomIdTxt.text = enterInput.text;
                                InsertStack(roomPanel);
                                enterInput.text = "";
                            }
                            break;

                        case "EXIT":
                            ID = int.Parse(data[1]);
                            otherPlayer = null;
                            if(ID==ClientID)
                            {
                                InRoom = false;
                                Escape();
                                turnMark[0].SetActive(false);
                                turnMark[1].SetActive(false);
                                if (started)
                                {
                                    started = false;
                                    Escape();
                                    IsFirst = false;
                                    MyTurn = false;
                                }
                                board.Clear();
                                UIManager.instance.ClearChat();
                            }
                            else
                            {
                                UIManager.instance.SystemMsgPopup(playerDict[ID].nickname + "님이 나갔습니다.");
                            }
                            break;

                        case "GAME START":
                            InsertStack(gamePanel);
                            mainCam.cullingMask = -1;

                            gameNickTxt[0].text = myPlayer.nickname;
                            gameNickTxt[1].text = otherPlayer.nickname;

                            bool f = ClientID == int.Parse(data[1]);
                            IsFirst = f;
                            MyTurn = f;

                            //myColorImage.color = f ? Color.green : Color.red;

                            turnMark[0].GetComponent<Image>().color = f ? markColor[0] : markColor[1];
                            turnMark[1].GetComponent<Image>().color = f ? markColor[1] : markColor[0];
                            turnMark[MyTurn ? 0 : 1].SetActive(true);

                            board.GameStart(IsFirst);

                            started = true;
                            break;

                        case "GO":
                            ID = int.Parse(data[1]);
                            board.cells[int.Parse(data[3])][int.Parse(data[2])].PlaceObject(ID);
                            MyTurn = !(ClientID == ID);  // MyTurn = !MyTurn;
                            turnMark[0].SetActive(MyTurn);
                            turnMark[1].SetActive(!MyTurn);
                            break;

                        case "GAME END":
                            ID = int.Parse(data[1]);
                            started = false;
                            UIManager.instance.GameResult(ID==ClientID?"승리":(ID==-1?"무승부":"패배"));
                            break;

                        case "HISTORY":
                            ID = int.Parse(data[1]);
                            Player p2 = Instantiate(playerPref, transform).GetComponent<Player>();
                            p2.Init(ID, false, data[2]);
                            playerDict.Add(ID, p2);
                            break;

                        case "SYSTEM MSG":
                            UIManager.instance.SystemMsgPopup(data[1]);
                            break;

                        case "CHAT":
                            UIManager.instance.Chat($"<b>{playerDict[int.Parse(data[1])].nickname}:</b> {data[2]}");
                            break;

                        case "DISCONNECTION":
                            ID = int.Parse(data[1]);
                            Destroy(playerDict[ID].gameObject);
                            playerDict.Remove(ID);
                            break;

                        default:  //정의하지 않은 헤드가 있음
                            Debug.Log("존재하지 않는 프로토콜 (head) : " + data[0]);
                            break;
                    }
                }

                receivedMsg = "";
            }
        }
    }

    public bool CanPlaceObject()
    {
        if (!connected) return false;
        if (!started || !MyTurn) return false;
        return true;
    }

    public void SendCount(int cnt)
    {
        ServerSend(ClientID.ToString()+"#"+cnt.ToString(), CommandHead.COUNT);
    }

    public void EndGame()
    {
        ServerSend(ClientID.ToString(), CommandHead.GAME_END);
    }

    public void CreateRoom()
    {
        if(InRoom || !connected)
        {
            return;
        }

        ServerSend(ClientID.ToString() , CommandHead.CREATE);
    }

    public void EnterRoom()
    {
        if(!InRoom && connected && enterInput.text.Trim()!="")
        {
            ServerSend(ClientID.ToString()+"#"+enterInput.text, CommandHead.ENTER);
        }
    }

    public void LeaveRoom()
    {
        if(InRoom)
        {
            ServerSend(ClientID.ToString(), CommandHead.EXIT);
        }
    }

    private void UpdateRoom()
    {
        if(InRoom)
        {
            int curCnt = otherPlayer != null ? 2 : 1;
            usersInfoTxt.text = $"현재: {curCnt}/2\n{myPlayer.nickname}\n";
            if(curCnt==2)
            {
                usersInfoTxt.text += otherPlayer.nickname; 
            }
        }
    }

    public void SendChatMsg(InputField chatInput)
    {
        if (connected && chatInput.text.Trim() != "")
        {
            ServerSend(ClientID.ToString() + "#" + chatInput.text, CommandHead.CHAT);
            chatInput.text = "";
        }
    }

    public void StartMainGame()
    {
        ServerSend(ClientID.ToString(), CommandHead.GAME_START);
    }

    public void Disconnect()
    {
        if(connected)
        {
            ServerSend(ClientID.ToString(), CommandHead.DISCONNECTION);

            InRoom = false;
            connected = false;

            IsFirst = false;
            MyTurn = false;

            foreach(int key in playerDict.Keys)
            {
                Destroy(playerDict[key].gameObject);
            }

            playerDict.Clear();
            netBuffer.Clear();

            ServerCheckThread.Abort();
            client.Close();
            endPoint = null;
            client = null;
            StopAllCoroutines();

            /*while(UIStack.Count>0)
            {
                UIStack.Pop().SetActive(false);
                loginPanel.SetActive(true);
                UIStack.Push(loginPanel);
            }*/
            SetUI();
        }
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    public void InsertStack(GameObject o)
    {
        UIStack.Peek().SetActive(false);
        o.SetActive(true);
        UIStack.Push(o);
    }

    public void Escape()
    {
        if (UIStack.Count > 1)
        {
            UIStack.Pop().SetActive(false);
            UIStack.Peek().SetActive(true);
        }
    }
}




#region 주석

/*case "ROOM UPDATE":
                            if(InRoom)
                            {
                                for(int i=1; i<data.Length-1; i++) 
                                {
                                    int _id = int.Parse(data[i]);
                                    playerDict[_id].currentRoomID = int.Parse(data[data.Length - 1]);
                                    if (ClientID != _id)
                                    {
                                        otherPlayer = playerDict[_id];
                                    }
                                }
                                UpdateRoom();
                            }
                            break;*/


#endregion