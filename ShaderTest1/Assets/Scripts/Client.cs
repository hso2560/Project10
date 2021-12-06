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
    public static readonly string POSITION = "POSITION";
    public static readonly string HISTORY = "HISTORY";
    public static readonly string ENTER = "ENTER";
    public static readonly string EXIT = "EXIT";
    public static readonly string ENTER_ROOM = "ENTER ROOM";
    public static readonly string EXIT_ROOM = "EXIT ROOM";
    public static readonly string UPDATE_ROOM = "UPDATE ROOM";
    public static readonly string SYSTEM_MSG = "SYSTEM MSG";
    public static readonly string CHAT = "CHAT";
    public static readonly string ATTACK = "ATTACK";
    public static readonly string DAMAGED = "DAMAGED";
    public static readonly string DEAD = "DEAD";
    public static readonly string RESPAWN = "RESPAWN";
}

public class Client : MonoBehaviour
{
    //[SerializeField] private string nickname = "noname";
    //[SerializeField] private string ip = "127.0.0.1";
    //[SerializeField] private int port = 5556;

    public static Client instance;

    [SerializeField] private InputField ipInput;
    [SerializeField] private InputField portInput;
    [SerializeField] private InputField nickInput;

    public Text systemTxt;
    public InputField chatInput;
    public Text chatText;
    public InputField roomIDInput, roomNameInput;
    public Text roomInfoTxt;

    private string receivedMsg = "";
    private byte[] bytes = new byte[65536];
    private Queue<string> netBuffer = new Queue<string>();
    private Socket client;
    private IPAddress ipAddr;
    private IPEndPoint endPoint;

    private Thread ServerCheckThread;
    private object buffer_lock = new object();
    private bool inRoom;

    public int id;
    public bool connected;
    public GameObject playerPref, bulletPref;
    public Dictionary<int, Player> playerDict = new Dictionary<int, Player>();
    public Player myPlayer;
    public Dictionary<long, Bullet> bulletDict = new Dictionary<long, Bullet>();

    private Camera mainCam;
    private CameraMove camMove;
    private Queue<GameObject> bulletQueue = new Queue<GameObject>();

    private void Awake()
    {
        Screen.SetResolution(1280, 720, false);
        Application.runInBackground = true;
        instance = this;
    }

    private void Start()
    {
        mainCam = Camera.main;
        camMove = mainCam.GetComponent<CameraMove>();
        for(int i=0; i<15; i++)
        {
            GameObject o = Instantiate(bulletPref,transform);
            InsertBullet(o);
        }
    }

    public void InsertBullet(GameObject o)
    {
        o.SetActive(false);
        bulletQueue.Enqueue(o);
    }
    public GameObject GetBullet()
    {
        GameObject o = null;
        if (bulletQueue.Count != 0)
        {
            o = bulletQueue.Dequeue();
        }
        else
        {
            o = Instantiate(bulletPref,transform);
        }
        o.SetActive(true);
        return o;
    }

    public void StartGame()
    {
        StartServer();
        StartCoroutine(BufferUpdate());
    }

    private void StartServer()
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
                Debug.Log(e.Message);
            }
        }
    }

    private void ServerSend(string msg, string head="")
    {
        try
        {
            byte[] sbuff = head != "" ? Encoding.UTF8.GetBytes(head + "#" + msg) : Encoding.UTF8.GetBytes(msg);
            client.Send(sbuff, 0, sbuff.Length, SocketFlags.None);
        }
        catch(Exception e)
        {
            Debug.Log(e.ToString());
        }
    }

    private IEnumerator BufferUpdate()
    {
        while (true)
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
                if(data.Length>1)
                {
                    switch(data[0])
                    {
                        case "INIT":  //나의 초기 세팅 
                            id = int.Parse(data[1]);
                            Player p = Instantiate(playerPref, Vector2.zero, Quaternion.identity).GetComponent<Player>();
                            p.SetData(id, 300, Vector2.zero, false, true,nickInput.text);
                            playerDict.Add(id, p);
                            myPlayer = p;
                            camMove.SetInit(myPlayer.transform);
                            nickInput.transform.parent.gameObject.SetActive(false);
                            systemTxt.transform.parent.gameObject.SetActive(true);
                            break;

                        case "ENTER": //다른 유저가 들어왔을 때
                            ID = int.Parse(data[1]);
                            if (ID != id)
                            {
                                Player _p = Instantiate(playerPref, Vector2.zero, Quaternion.identity).GetComponent<Player>();
                                _p.SetData(ID, 1000, Vector2.zero, false, false, data[2]);
                                playerDict.Add(ID, _p);
                            }
                            break;

                        case "HISTORY":  //기존에 있던 유저들 정보를 반영
                            ID = int.Parse(data[1]);
                            Player p2 = Instantiate(playerPref, Vector2.zero, Quaternion.identity).GetComponent<Player>();
                            int _hp = int.Parse(data[4]);
                            p2.SetData(ID, _hp, new Vector2(float.Parse(data[2]),float.Parse(data[3])), _hp<=0, false, data[5]);
                            playerDict.Add(ID, p2);
                            break;

                        case "POSITION":  //위치 동기화
                            ID = int.Parse(data[1]);
                            if(id!= ID)
                            {
                                playerDict[ID].target = new Vector3(float.Parse(data[2]),float.Parse(data[3]));
                            }
                            break;

                        case "DISCONNECTION": //다른 유저가 접속을 끊었을 때
                            Player p3 = playerDict[int.Parse(data[1])];
                            playerDict.Remove(p3.id);
                            Destroy(p3.gameObject);
                            break;

                        case "SYSTEM MSG":  //시스템 메시지
                            systemTxt.text = data[1];
                            break;

                        case "UPDATE ROOM":  //방 상태 업데이트
                            roomInfoTxt.text = $"방장: {data[1]}, 인원: {data[2]}";
                            systemTxt.text = data[3];
                            inRoom = true;
                            break;

                        case "CHAT":  //채팅
                            int idx = receivedMsg.IndexOf('#', 5);
                            string msg = receivedMsg.Substring(idx + 1);
                            chatText.text += chatText.text != "" ? $"\n<color=blue>{playerDict[int.Parse(data[1])].nickname} :</color> {msg}" : $"<color=blue>{playerDict[int.Parse(data[1])].nickname} :</color> {msg}";
                            break;

                        case "ATTACK":
                            ID = int.Parse(data[1]);
                            long bID = long.Parse(data[3]);
                            Bullet b = GetBullet().GetComponent<Bullet>();
                            b.SetInit(playerDict[ID].transform.position, float.Parse(data[2]),ID, bID);
                            bulletDict.Add(bID, b);
                            break;

                        case "DAMAGED":
                            ID = int.Parse(data[1]);
                            bulletDict[long.Parse(data[4])].Inactive();
                            playerDict[ID].Damaged(int.Parse(data[2]),int.Parse(data[3]));
                            break;

                        case "DEAD":
                            Death(int.Parse(data[2]), int.Parse(data[1]));
                            break;

                        case "RESPAWN":
                            Respawn(int.Parse(data[1]));
                            break;

                        default:  //정의하지 않은 헤드가 있음
                            Debug.Log("존재하지 않는 프로토콜 (head) : "+data[0]);
                            break;
                    }
                }

                receivedMsg = "";
            }
        }
    }

    public void SendPosition(float x, float y)
    {
        ServerSend(id.ToString()+"#"+x.ToString()+"#"+y.ToString(), CommandHead.POSITION);
    }

    public void SendChat()
    {
        if(chatInput.text!="")
        {
            ServerSend(id.ToString() + "#" + chatInput.text, CommandHead.CHAT);
            chatInput.text = "";
        }
    }

    public void LeaveRoom()
    {
        if(inRoom)
        {
            ServerSend(id.ToString(), CommandHead.EXIT_ROOM);
            roomInfoTxt.text = "";
            inRoom = false;
        }
    }

    public void EnterRoom()
    {
        if (!inRoom)
        {
            ServerSend($"{id}#1#{roomIDInput.text}", CommandHead.ENTER_ROOM);
        }
    }

    public void CreateRoom()
    {
        if (!inRoom)
        {
            ServerSend($"{id}#0#{roomNameInput.text}#6", CommandHead.ENTER_ROOM);
        }
    }

    public void Death(int killerID, int deadID)
    {
        systemTxt.text = $"'{playerDict[killerID].nickname}'님이 '{playerDict[deadID].nickname}'님을 죽였습니다.";
        playerDict[deadID].dead = true;
        playerDict[deadID].transform.localScale = new Vector3(0.2f, 0.2f);
        if(deadID == id)
        {
            StartCoroutine(DelayFunc(10f, () =>
            {
                if (inRoom)
                {
                    ServerSend(id.ToString(), CommandHead.RESPAWN);
                }
            }));
        }
    }

    public void Respawn(int _id)
    {
        Player p = playerDict[_id];
        p.hp = p.maxHp;
        p.Damaged(0, 0); //그냥 HP UI만 갱신시킴
        p.transform.localScale = Vector3.one;
        p.dead = false;

        systemTxt.text = p.nickname + "이 부활했음";
    }

    private void Update()
    {
        if(connected && inRoom)
        {
            if(myPlayer.connected && !myPlayer.dead && !myPlayer.isAtk)
            {
                if(Input.GetMouseButtonDown(0))
                {
                    //Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
                    Vector3 mPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
                    mPos = mPos - myPlayer.transform.position;
                    mPos.Normalize();
                    float rz = Mathf.Atan2(mPos.y, mPos.x) * Mathf.Rad2Deg;
                    Attack(rz);
                    //Attack(mPos.x, mPos.y);

                    myPlayer.isAtk = true;
                    StartCoroutine(DelayFunc(0.5f,() => myPlayer.isAtk = false));
                }
            }
        }
    }

    /*private void Attack(float x, float y)
    {
        ServerSend($"{id}#{x}#{y}", CommandHead.ATTACK);
    }*/
    private void Attack(float z)
    {
        ServerSend($"{id}#{z}", CommandHead.ATTACK);
    }

    public void Damaged(long serverID ,int attacker, int damagedID, int damage)
    {
        ServerSend($"{damagedID}#{damage}#{attacker}#{serverID}", CommandHead.DAMAGED);
    }

    public void Dead(int killer)
    {
        ServerSend($"{id}#{killer}", CommandHead.DEAD);
    }

    public void Disconnect()
    {
        if (connected)
        {
            ServerSend(id.ToString(), CommandHead.DISCONNECTION);

            connected = false;
            inRoom = false;
            playerDict[id].connected = false;

            camMove.SetInit(null);

            foreach (int key in playerDict.Keys)
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

            nickInput.transform.parent.gameObject.SetActive(true);
            systemTxt.transform.parent.gameObject.SetActive(false);
        }
    }
    private void OnApplicationQuit()
    {
        Disconnect();
    }

    IEnumerator DelayFunc(float t, Action F)
    {
        yield return new WaitForSeconds(t);
        F();
    }
}
