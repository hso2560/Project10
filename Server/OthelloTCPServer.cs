using System;
using static System.Console;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TCPServer1
{
    class CommandHead
    {
        public static readonly string CONNECTION = "CONNECTION";
        public static readonly string DISCONNECTION = "DISCONNECTION";
        public static readonly string INIT = "INIT";
        public static readonly string ENTER = "ENTER";
        public static readonly string EXIT = "EXIT";
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
    }

    class Program
    {
        private int port = 8910;
        private static TcpListener serverSocket;
        private TcpClient clientSocket;

        public static Dictionary<int, UserData> userDic = new Dictionary<int, UserData>();
        public static Dictionary<int, Room> roomDic = new Dictionary<int, Room>();

        public static List<UserData> matchingUsers = new List<UserData>();

        private static Queue<string> processQueue = new Queue<string>();
        private Thread procThr, matchingThr;

        public int index = 0; //���� ���̵�
        public static int roomIndex = 0; //�� ���̵�

        private object lockObj = new object();
        private static object sendLock = new object(), broadLock = new object(), broadLock2 = new object(), broadLock3 = new object();
        private static object processLock = new object();

        private void StartServer()
        {
            try
            {
                serverSocket = new TcpListener(IPAddress.Any, port);
                serverSocket.Start();

                procThr = new Thread(ProcessData);
                matchingThr = new Thread(Matching);
                procThr.Start();
                matchingThr.Start();

                WriteLine("Othello TCP Server Start...");

                while (true)
                {
                    clientSocket = serverSocket.AcceptTcpClient();

                    userDic.Add(index, new UserData(index, clientSocket));
                    index++;
                }
            }
            catch (Exception e)
            {
                WriteLine(e.ToString());
            }
        }

        private void EndServer()
        {
            procThr.Interrupt();
            matchingThr.Interrupt();
            roomDic.Clear();
            userDic.Clear();
            matchingUsers.Clear();
            clientSocket.Close();
            serverSocket.Stop();
        }

        private void ProcessData()
        {
            while (true)
            {
                while (processQueue.Count > 0)
                {
                    ProcessCommand(processQueue.Dequeue());
                }
            }
        }

        public static void MsgEnqueue(string msg)
        {
            lock (processLock)
            {
                processQueue.Enqueue(msg);
            }
        }

        private void Matching()
        {
            while (true)
            {
                if (matchingUsers.Count > 1)
                {
                    UserData first = matchingUsers[0];
                    UserData second = matchingUsers[1];

                    matchingUsers.RemoveAt(0);
                    matchingUsers.RemoveAt(0);

                    Room room = new Room(roomIndex, first);
                    roomDic.Add(roomIndex, room);
                    roomIndex++;

                    Thread.Sleep(80);

                    room.AddUser(second);

                    WriteLine($"��Ī �Ϸ�.  ����: {first.name}, ����: {second.name}");

                    room.StartGame(first.id);
                }
            }
        }

        private void ProcessCommand(string msg)
        {
            string[] data = msg.Split('#');
            int ID;
            UserData u;

            if (data.Length > 1)
            {
                switch (data[0])
                {
                    case "INIT":  //������ ������ (����)
                        userDic[int.Parse(data[1])].Init(data[2]);
                        break;

                    case "DISCONNECTION":  //������ ������ ����
                        u = userDic[int.Parse(data[1])];
                        WriteLine(u.name + "���� �����Ͽ����ϴ�.");
                        if (u.isRoom)
                        {
                            u.currentRoom.LeaveRoom(u);
                        }
                        Program.userDic[u.id].Disconnect();
                        Program.userDic.Remove(u.id);
                        break;

                    case "GO":  //���� �� (�⹰ ��ġ)
                        u = userDic[int.Parse(data[1])];
                        if (data.Length > 3)
                        {
                            int x = int.Parse(data[2]);
                            int y = int.Parse(data[3]);
                            u.currentRoom.LayGameCell(u.id, x, y);
                            Broadcast(u.currentRoom.userList, msg);
                            WriteLine(string.Format("{0}�� {1},{2} �� ��ġ��", u.name, x, y));
                        }
                        else
                        {
                            u.currentRoom.TimeOver();
                            Broadcast(u.currentRoom.userList, msg);
                            WriteLine(u.name + "�� ��ġ�� ���ϰ� ���� �ѱ�");
                        }
                        break;

                    case "COUNT":
                        userDic[int.Parse(data[1])].SetCount(int.Parse(data[2]));
                        break;

                    case "CHAT":  //ê
                        u = userDic[int.Parse(data[1])];
                        WriteLine(u.name + ": " + data[2]);
                        if (u.isRoom)
                        {
                            Broadcast(u.currentRoom.userList, msg);
                        }
                        else
                        {
                            BroadcastToLobby(msg);
                        }
                        break;

                    case "CREATE":
                        ID = int.Parse(data[1]);
                        WriteLine(userDic[ID].name + "�� ���� ����");
                        roomDic.Add(roomIndex, new Room(roomIndex, userDic[ID]));
                        roomIndex++;
                        break;

                    case "ENTER":
                        u = userDic[int.Parse(data[1])];
                        int roomId = int.Parse(data[2]);
                        if (roomDic.ContainsKey(roomId))
                        {
                            roomDic[roomId].AddUser(u);
                        }
                        else
                        {
                            SendMsg(u.stream, "�� ���̵� �ٽ� Ȯ�����ּ���.", CommandHead.SYSTEM_MSG);
                        }
                        break;

                    case "EXIT":
                        u = userDic[int.Parse(data[1])];
                        if (u.isRoom)
                        {
                            u.currentRoom.LeaveRoom(u);
                        }
                        break;

                    case "GAME START":  //���� ����
                        ID = int.Parse(data[1]);
                        userDic[ID].currentRoom.StartGame(ID);
                        break;

                    case "GAME END":
                        userDic[int.Parse(data[1])].currentRoom.EndGame();
                        break;

                    case "MATCHING":
                        ID = int.Parse(data[1]);
                        matchingUsers.Add(userDic[ID]);
                        WriteLine("��Ī ���� : " + userDic[ID].name);
                        break;

                    case "CANCEL MATCHING":
                        u = userDic[int.Parse(data[1])];
                        matchingUsers.Remove(u);
                        SendMsg(u.stream, "NULL", CommandHead.CANCEL_MATCHING);
                        WriteLine("��Ī ��� : " + u.name);
                        break;

                    default:  //�������� ���� ��尡 ������
                        WriteLine("�������� �ʴ� �������� (head) : " + data[0]);
                        break;
                }
            }
        }

        static void Main(string[] args)
        {
            Program p = new Program();

            p.StartServer();
            p.EndServer();
        }

        public static void SendMsg(NetworkStream stream, string msg, string head = "")
        {
            lock (sendLock)
            {
                //WriteLine("�޽��� ����");
                try
                {
                    byte[] data = head == "" ? Encoding.UTF8.GetBytes(msg) : Encoding.UTF8.GetBytes(head + "#" + msg);
                    stream.Write(data, 0, data.Length);
                    stream.Flush();
                }
                catch (Exception e)
                {
                    WriteLine(e.ToString());
                }
            }
        }

        public static void Broadcast(string msg, string head = "")
        {
            lock (broadLock)
            {
                foreach (UserData user in userDic.Values)
                {
                    SendMsg(user.stream, msg, head);
                }
            }
        }

        public static void Broadcast(List<UserData> users, string msg, string head = "")
        {
            lock (broadLock2)
            {
                for (int i = 0; i < users.Count; i++)
                {
                    SendMsg(users[i].stream, msg, head);
                }
            }
        }

        public static void BroadcastToLobby(string msg, string head = "")
        {
            lock (broadLock3)
            {
                foreach (UserData user in userDic.Values)
                {
                    if (!user.isRoom)
                        SendMsg(user.stream, msg, head);
                }
            }
        }
    }

    class UserData
    {
        public bool connected;
        public int id;
        public string name;
        public TcpClient client;
        public NetworkStream stream;
        private Thread ctThread;

        public bool isRoom;
        public Room currentRoom;

        public WinningRate winningRate = new WinningRate();
        public bool myTurn;
        public bool first;
        public int currentMyCellCnt;

        public UserData() { }
        public UserData(int id, TcpClient client)
        {
            this.id = id;
            this.client = client;
            stream = client.GetStream();
            this.name = "init";
            connected = true;

            ctThread = new Thread(ClientWork);
            ctThread.Start();
            Program.SendMsg(stream, id.ToString(), CommandHead.INIT);
            WriteLine("���� ���� ID: " + id + "  IP: " + client.Client.RemoteEndPoint.ToString());
        }

        public void Init(string name)
        {
            this.name = name;
            WriteLine(name + "���� ����");

            foreach (UserData user in Program.userDic.Values)
            {
                if (user.id != id)
                {
                    Program.SendMsg(user.stream, string.Concat(id, "#", name), CommandHead.CONNECTION);
                    Program.SendMsg(stream, string.Concat(user.id, "#", user.name), CommandHead.HISTORY);
                }
            }
        }

        private bool SocketConnected(Socket s)
        {
            try
            {
                bool part1 = s.Poll(1000, SelectMode.SelectRead);
                bool part2 = (s.Available == 0);
                if (part1 && part2)
                    return false;
                else
                    return true;
            }
            catch
            {
                return false;
            }
        }

        private void ClientWork()
        {
            byte[] bytes = new byte[16384];
            string data = "";
            int numByteRead;

            while (connected)
            {
                try
                {
                    if (!SocketConnected(client.Client))
                    {
                        connected = false;
                    }
                    else
                    {
                        if (stream.DataAvailable)
                        {
                            data = "";
                            while (stream.DataAvailable)
                            {
                                numByteRead = stream.Read(bytes, 0, bytes.Length);
                                data = Encoding.UTF8.GetString(bytes, 0, numByteRead);
                            }
                            Program.MsgEnqueue(data);
                            Array.Clear(bytes, 0, bytes.Length);
                        }
                    }
                }
                catch (Exception e)
                {
                    Disconnect();
                    WriteLine("Error:" + e.ToString());
                }
            }
        }

        public void Disconnect()
        {
            connected = false;
            ctThread.Interrupt();
            stream.Close();
            client.Close();
        }

        public void SetRoom(Room room = null)
        {
            this.currentRoom = room;
            this.isRoom = room != null;
        }

        public void SetCount(int cnt)
        {
            currentMyCellCnt = cnt;
            WriteLine($"{name}���� ����: {currentMyCellCnt}");
            if (currentMyCellCnt == 0)
            {
                currentRoom.EndGame();
            }
        }
    }

    class Room
    {
        public int roomID;
        public readonly int maxCount = 2;
        public int currentCount;

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

            Program.SendMsg(user.stream, roomID.ToString(), CommandHead.CREATE);
        }

        public void AddUser(UserData user)
        {
            if (isGameStart)
            {
                Program.SendMsg(user.stream, "�̹� ������ ������ ���Դϴ�.", CommandHead.SYSTEM_MSG);
                return;
            }
            if (currentCount >= maxCount)
            {
                Program.SendMsg(user.stream, "�ش� ���� �ο��� �� á���ϴ�.", CommandHead.SYSTEM_MSG);
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

        public void LeaveRoom(UserData user)
        {
            currentCount--;
            user.SetRoom();

            userList.Remove(user);
            Program.Broadcast(userList, user.id.ToString(), CommandHead.EXIT);
            Program.SendMsg(user.stream, user.id.ToString(), CommandHead.EXIT);
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
                Program.SendMsg(Program.userDic[owner.id].stream, "������ �����ϱ����� �ο��� �����մϴ�.", CommandHead.SYSTEM_MSG);
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

        public void TimeOver()
        {
            first.myTurn = !first.myTurn;
            second.myTurn = !second.myTurn;
        }

        public void EndGame()
        {
            if (!isGameStart) return;

            int winnerId = -1; //���º� �ÿ� -1�� ����
            if (currentCount == 2)
            {
                if (first.currentMyCellCnt > second.currentMyCellCnt)
                {
                    winnerId = first.id;
                    Program.userDic[first.id].winningRate.win++;
                    Program.userDic[second.id].winningRate.lose++;
                }
                else if (first.currentMyCellCnt < second.currentMyCellCnt)
                {
                    winnerId = second.id;
                    Program.userDic[second.id].winningRate.win++;
                    Program.userDic[first.id].winningRate.lose++;
                }
                else
                {
                    Program.userDic[second.id].winningRate.draw++;
                    Program.userDic[first.id].winningRate.draw++;
                }
            }
            else
            {
                winnerId = userList[0].id;
            }

            Program.Broadcast(userList, winnerId.ToString(), CommandHead.GAME_END);
            WriteLine("���� ��. ���� : " + Program.userDic[winnerId].name);

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
