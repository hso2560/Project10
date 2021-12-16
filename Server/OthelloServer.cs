using System;
using static System.Console;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace OthelloServer
{
    class CommandHead
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
        public static readonly string CREATE = "CREATE";
        public static readonly string HISTORY = "HISTORY";
    }

    class Program
    {
        private IPEndPoint ipep;
        public static Socket server;

        private int port = 5756;
        private object lockObj = new object();

        public static Dictionary<int, UserData> userDic = new Dictionary<int, UserData>();
        public static Dictionary<int, Room> roomDic = new Dictionary<int, Room>();

        public int index = 0; //���� ���̵�
        public int roomIndex = 0; //�� ���̵�

        public EndPoint remote;

        public void StartServer()
        {
            ipep = new IPEndPoint(IPAddress.Any, port);
            server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            server.Bind(ipep);

            WriteLine("Server Start..");

            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            remote = (EndPoint)(sender);
            byte[] _data = new byte[16384];

            while (true)
            {
                lock (lockObj)
                {
                    try
                    {
                        server.ReceiveFrom(_data, ref remote);
                    }
                    catch (Exception e)
                    {
                        WriteLine(e.ToString());
                    }
                    string str = Encoding.UTF8.GetString(_data);
                    Array.Clear(_data, 0, _data.Length);
                    //WriteLine("Server Receive Data : " + rtStr + " => " + str);

                    ProcessCommand(str);
                }
            }
        }

        private void ProcessCommand(string msg)
        {
            string[] data = msg.Split('#');
            int ID;

            if (data.Length > 1)
            {
                switch (data[0])
                {
                    case "CONNECTION":  //������ ������ (����)
                        Program.userDic.Add(index, new UserData(index, data[1], remote));
                        WriteLine(data[1] + "���� �����Ͽ����ϴ�.");
                        index++;
                        break;

                    case "DISCONNECTION":  //������ ������ ����
                        ID = int.Parse(data[1]);
                        WriteLine(userDic[ID].name + "���� �����Ͽ����ϴ�.");
                        if (userDic[ID].isRoom)
                        {
                            roomDic[userDic[ID].currentRoom.roomID].LeaveRoom(userDic[ID]);
                        }
                        Program.userDic.Remove(ID);
                        break;

                    case "CREATE":
                        ID = int.Parse(data[1]);
                        WriteLine(userDic[ID].name + "�� ���� ����");
                        roomDic.Add(roomIndex, new Room(roomIndex, userDic[ID]));
                        roomIndex++;
                        break;

                    case "ENTER":
                        ID = int.Parse(data[1]);
                        int roomId = int.Parse(data[2]);
                        if (roomDic.ContainsKey(roomId))
                        {
                            roomDic[roomId].AddUser(userDic[ID]);
                        }
                        else
                        {
                            SendMsg(userDic[ID].ep, "�� ���̵� �ٽ� Ȯ�����ּ���.", CommandHead.SYSTEM_MSG);
                        }
                        break;

                    case "EXIT":
                        ID = int.Parse(data[1]);
                        if (userDic[ID].isRoom)
                        {
                            roomDic[userDic[ID].currentRoom.roomID].LeaveRoom(userDic[ID]);
                        }
                        break;

                    case "CHAT":  //ê

                        break;

                    case "GIVE UP":  //����

                        break;

                    case "COUNT":
                        userDic[int.Parse(data[1])].currentMyCellCnt = int.Parse(data[2]);
                        break;

                    case "GO":  //���� �� (�⹰ ��ġ)
                        ID = int.Parse(data[1]);
                        int x = int.Parse(data[2]);
                        int y = int.Parse(data[3]);
                        userDic[ID].currentRoom.LayGameCell(ID, x, y);
                        Broadcast(userDic[ID].currentRoom.userList, msg);
                        WriteLine(string.Format("{0}�� {1},{2} �� ��ġ��", userDic[ID].name, x, y));
                        break;

                    case "GAME START":  //���� ����
                        ID = int.Parse(data[1]);
                        userDic[ID].currentRoom.StartGame(ID);
                        break;

                    default:  //�������� ���� ��尡 ������
                        WriteLine("�������� �ʴ� �������� (head) : " + data[0]);
                        break;
                }
            }
        }

        public void EndServer()
        {
            roomDic.Clear();
            userDic.Clear();
            server.Close();
        }

        static void Main(string[] args)
        {
            Program p = new Program();

            p.StartServer();
            p.EndServer();
        }

        public static void SendMsg(EndPoint ep, string msg, string head = "")
        {
            try
            {
                byte[] bytes = head == "" ? Encoding.UTF8.GetBytes(msg) : Encoding.UTF8.GetBytes(head + "#" + msg);
                server.SendTo(bytes, 0, bytes.Length, SocketFlags.None, ep);
            }
            catch (Exception e)
            {
                WriteLine(e.ToString());
            }
        }

        public static void Broadcast(List<UserData> users, string msg, string head = "")
        {
            for (int i = 0; i < users.Count; i++)
            {
                SendMsg(users[i].ep, msg, head);
            }
        }

        public static void Broadcast(string msg, string head = "")
        {
            foreach (UserData user in userDic.Values)
            {
                SendMsg(user.ep, msg, head);
            }
        }
    }

    class UserData
    {
        public int id;
        public string name;
        public bool connected;

        public EndPoint ep;

        public bool isRoom;
        public Room currentRoom;

        public WinningRate winningRateDic = new WinningRate();
        public bool myTurn;
        public bool first;
        public int currentMyCellCnt;

        public UserData() { }

        public UserData(int id, string name, EndPoint ep)
        {
            this.id = id;
            this.name = name;
            connected = true;

            this.ep = ep;

            isRoom = false;
            currentRoom = null;

            Program.SendMsg(this.ep, this.id.ToString(), CommandHead.INIT);
            Program.Broadcast(id.ToString() + "#" + name, CommandHead.CONNECTION);

            foreach (UserData ud in Program.userDic.Values)
            {
                if (ud.id != id)
                {
                    Program.SendMsg(this.ep, $"{ud.id}#{ud.name}", CommandHead.HISTORY);
                }
            }
        }

        public void SetRoom(Room room = null)
        {
            this.currentRoom = room;
            this.isRoom = room != null;
        }
    }

    class Room
    {
        public int roomID;
        public readonly int maxCount = 2;
        public int currentCount;

        public bool isMatchingRoom;
        public UserData owner;
        public List<UserData> userList = new List<UserData>();

        private UserData first, second; // ���� ����, �İ�

        private bool firstOwner = true;
        private bool isGameStart = false;

        private List<List<Board>> board = new List<List<Board>>();

        public Room() { }

        public Room(int roomID, UserData user)
        {
            this.roomID = roomID;
            currentCount = 1;
            userList.Add(user);
            user.SetRoom(this);
            owner = user;

            Program.SendMsg(user.ep, roomID.ToString(), CommandHead.CREATE);
        }

        public void AddUser(UserData user)
        {
            if (isGameStart)
            {
                Program.SendMsg(user.ep, "�̹� ������ ������ ���Դϴ�.", CommandHead.SYSTEM_MSG);
                return;
            }
            if (currentCount >= maxCount)
            {
                Program.SendMsg(user.ep, "�ش� ���� �ο��� �� á���ϴ�.", CommandHead.SYSTEM_MSG);
                return;
            }

            userList.Add(user);
            currentCount++;
            user.SetRoom(this);

            WriteLine(user.name + "�� �濡 ������. �� ���̵�: " + roomID.ToString());
            Program.Broadcast(userList, user.name + "���� �����Ͽ����ϴ�.", CommandHead.SYSTEM_MSG);
            Program.Broadcast(userList, user.id.ToString() + "#" + owner.id.ToString(), CommandHead.ENTER);
            //UpdateRoom();
        }

        /*private void UpdateRoom()
        {
            string msg = "";
            foreach (UserData u in userList)
            {
                msg += u.id + "#";
            }
            msg += roomID.ToString();

            Program.Broadcast(userList, msg, CommandHead.ROOM_UPDATE);
        }*/

        public void LeaveRoom(UserData user)
        {
            currentCount--;
            user.SetRoom();

            Program.Broadcast(userList, user.id.ToString(), CommandHead.EXIT);
            userList.Remove(user);
            WriteLine(user.name + "���� ���� ����");

            if (isGameStart)
            {
                user.currentMyCellCnt = 0;
                user.first = false;
                user.myTurn = false;
            }

            if (currentCount == 0) //�濡 �ƹ��� ���� ==> �� ����
            {
                Program.roomDic.Remove(roomID);
            }
            else
            {
                if (user == owner)
                {
                    owner = userList[0];
                }
            }
        }

        public void StartGame(int id)
        {
            if (isGameStart || id != owner.id)
            {
                return;
            }

            if (currentCount < 2)
            {
                Program.SendMsg(Program.userDic[owner.id].ep, "������ �����ϱ����� �ο��� �����մϴ�.", CommandHead.SYSTEM_MSG);
                return;
            }

            for (int i = 0; i < 8; i++)
            {
                List<Board> list = new List<Board>();
                for (int j = 0; j < 8; j++)
                {
                    list.Add(new Board(j, i));  //i,j�� ������ �� +1�� �� ���� �ְ����� ���߿� �������� �� ������ �׳� 0~7�� ��
                }
                board.Add(list);
            }

            userList[0].currentMyCellCnt = 2;
            userList[1].currentMyCellCnt = 2;

            userList[0].first = firstOwner;
            userList[1].first = !firstOwner;

            userList[0].myTurn = firstOwner;
            userList[1].myTurn = !firstOwner;

            first = firstOwner ? userList[0] : userList[1];
            second = firstOwner ? userList[1] : userList[0];

            board[3][3].Set(first.id);
            board[4][4].Set(first.id);
            board[3][4].Set(second.id);
            board[4][3].Set(second.id);

            isGameStart = true;

            Program.Broadcast(userList, first.id.ToString(), CommandHead.GAME_START); //���� �����ϴ� ����� ���̵� ����
            WriteLine("������ ���۵�. ���۵� �� ���̵�: " + roomID);

            firstOwner = !firstOwner;  //���Ͽ��� ���� �ƴ� ���� ���������ϰԲ� ���ش�. (1��°�� ���� �����ϰ� ���ش�)
        }

        public void LayGameCell(int id, int x, int y) //���� �������� ���� �˻��ؾ��Ұ� ������ ������. 
        {
            board[y][x].Set(id);

            first.myTurn = !first.myTurn;
            second.myTurn = !second.myTurn;
        }

        public void EndGame()
        {
            userList.ForEach(x =>
            {
                x.currentMyCellCnt = 0;
                x.first = false;
                x.myTurn = false;
            });
            isGameStart = false;
            board.Clear();
            first = null;
            second = null;
        }
    }

    class Board
    {
        public int x, y;
        public bool exist;
        public int userID;

        public Board() { }
        public Board(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public void Set(int id)
        {
            userID = id;
            exist = true;
        }
    }

    class WinningRate
    {
        public int win = 0;
        public int lose = 0;
        public int draw = 0;
    }
}
