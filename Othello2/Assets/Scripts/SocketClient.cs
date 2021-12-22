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
    public static readonly string MATCHING = "MATCHING";
    public static readonly string CANCEL_MATCHING = "CANCEL MATCHING";
    //public static readonly string MATCHING_COMPLETE = "MATCHING COMPLETE";
}

public class SocketClient : MonoBehaviour
{
    public static SocketClient instance;

    public Stack<GameObject> UIStack = new Stack<GameObject>();
    public GameObject loginPanel, lobbyPanel, roomPanel, gamePanel, matchingPanel;
    [SerializeField] private InputField ipInput;
    [SerializeField] private InputField portInput;
    [SerializeField] private InputField nickInput;
    public InputField enterInput;
    //public Image myColorImage; //green or red

    [SerializeField] private Text ratingTxt, roomIdTxt, usersInfoTxt;

    private TcpClient client;
    private NetworkStream stream;
    private Thread ctThread;

    private bool connected, matching;
    private string receivedMsg = "";
    private Queue<string> netBuffer = new Queue<string>();
    
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
        ClientID = -1;
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
            UIManager.instance.SystemMsgPopup("�г��� ĭ�� ����ֽ��ϴ�.");
            return;
        }

        try
        {
            if(!connected)
            {
                client = new TcpClient();
                client.Connect(IPAddress.Parse(ipInput.text), int.Parse(portInput.text));
                stream = client.GetStream();
                ctThread = new Thread(ServerCheck);
                ctThread.Start();

                connected = true;
            }
        }
        catch(Exception e)
        {
            Debug.Log(e);
        }
    }

    private void ServerCheck()
    {
        byte[] inStream = new byte[16384];
        string data;

        while (true)
        {
            try
            {
                stream = client.GetStream();

                int numBytesRead;
                if (stream.DataAvailable)
                {
                    data = "";
                    while (stream.DataAvailable)
                    {
                        numBytesRead = stream.Read(inStream, 0, inStream.Length);
                        data += Encoding.UTF8.GetString(inStream, 0, numBytesRead);
                    }
                    netBuffer.Enqueue(data);
                    Array.Clear(inStream, 0, inStream.Length);
                }
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
            }
        }
    }

    private void ServerSend(string msg, string head = "")
    {
        if (connected && stream != null)
        {
            try
            {
                byte[] sbuff = head != "" ? Encoding.UTF8.GetBytes(head + "#" + msg) : Encoding.UTF8.GetBytes(msg);
                stream.Write(sbuff, 0, sbuff.Length);
                stream.Flush();
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
            }
        }
    }

    public void LayGameCell(int x, int y)
    {
        ServerSend($"{ClientID}#{x}#{y}", CommandHead.GO);
    }
    
    public void TimeOver()
    {
        if(MyTurn)
           ServerSend(ClientID.ToString(), CommandHead.GO);
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
                        case "INIT":  //������ �ʱ� ���� (�����κ��� ���̵� �ο�����)
                            ClientID = int.Parse(data[1]);
                            myPlayer = Instantiate(playerPref, transform).GetComponent<Player>();
                            myPlayer.Init(ClientID, true, nickInput.text);
                            playerDict.Add(ClientID, myPlayer);
                            InsertStack(lobbyPanel);
                            
                            ServerSend(ClientID.ToString() + "#" + nickInput.text, CommandHead.INIT);
                            nickInput.text = "";
                            
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
                            roomIdTxt.text = "�� ���̵�: " + data[1];
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
                                if (roomPanel.activeSelf)
                                {
                                    Escape();
                                }
                                board.Clear();
                                UIManager.instance.ClearChat();
                            }
                            else
                            {
                                if(otherPlayer == playerDict[ID] && started)
                                {
                                    EndGame();
                                }
                                UIManager.instance.SystemMsgPopup(playerDict[ID].nickname + "���� �������ϴ�.");
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

                            GameManager.instance.ChangeTurn(MyTurn);

                            break;

                        case "GO":
                            ID = int.Parse(data[1]);
                            if (data.Length > 3)
                            {
                                board.cells[int.Parse(data[3])][int.Parse(data[2])].PlaceObject(ID);
                                MyTurn = !(ClientID == ID);  // MyTurn = !MyTurn;
                            }
                            else
                            {
                                MyTurn = !MyTurn;
                            }
                            turnMark[0].SetActive(MyTurn);
                            turnMark[1].SetActive(!MyTurn);
                            GameManager.instance.ChangeTurn(MyTurn);
                            break;

                        case "GAME END":
                            ID = int.Parse(data[1]);
                            started = false;
                            UIManager.instance.GameResult(ID==ClientID?"�¸�":(ID==-1?"���º�":"�й�"));
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

                        case "CANCEL MATCHING":
                            if(matching)
                            {
                                matching = false;
                                Escape();
                            }
                            break;

                        case "DISCONNECTION":
                            ID = int.Parse(data[1]);
                            Destroy(playerDict[ID].gameObject);
                            playerDict.Remove(ID);
                            break;

                        default:  //�������� ���� ��尡 ����
                            Debug.Log("�������� �ʴ� �������� (head) : " + data[0]);
                            break;
                    }
                }
                else
                {
                    Debug.Log("�������� ���̰� ª��");
                }

                receivedMsg = "";
            }
            else
            {
                Debug.Log("�޽����� �������");
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

    public void StartMatching()
    {
        if (connected && !InRoom && !matching)
        {
            ServerSend(ClientID.ToString(), CommandHead.MATCHING);
            matching = true;
            InsertStack(matchingPanel);
        }
    }

    public void CancelMatching()
    {
        if(matching)
        {
            ServerSend(ClientID.ToString(), CommandHead.CANCEL_MATCHING);
        }
    }

    private void UpdateRoom()
    {
        if(InRoom)
        {
            int curCnt = otherPlayer != null ? 2 : 1;
            usersInfoTxt.text = $"����: {curCnt}/2\n{myPlayer.nickname}\n";
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

            ctThread.Abort();
            ClientID = -1;
            stream.Close();
            client.Close();
            client = null;
            StopAllCoroutines();

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