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

        public int index = 0; //유저 아이디
        public static int roomIndex = 0; //방 아이디

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

                    WriteLine($"매칭 완료.  방장: {first.name}, 상대방: {second.name}");

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
                    case "INIT":  //유저가 접속함 (연결)
                        userDic[int.Parse(data[1])].Init(data[2]);
                        break;

                    case "DISCONNECTION":  //유저가 접속을 끊음
                        u = userDic[int.Parse(data[1])];
                        WriteLine(u.name + "님이 종료하였습니다.");
                        if (u.isRoom)
                        {
                            u.currentRoom.LeaveRoom(u);
                        }
                        Program.userDic[u.id].Disconnect();
                        Program.userDic.Remove(u.id);
                        break;

                    case "GO":  //수를 둠 (기물 배치)
                        u = userDic[int.Parse(data[1])];
                        if (data.Length > 3)
                        {
                            int x = int.Parse(data[2]);
                            int y = int.Parse(data[3]);
                            u.currentRoom.LayGameCell(u.id, x, y);
                            Broadcast(u.currentRoom.userList, msg);
                            WriteLine(string.Format("{0}가 {1},{2} 에 배치함", u.name, x, y));
                        }
                        else
                        {
                            u.currentRoom.TimeOver();
                            Broadcast(u.currentRoom.userList, msg);
                            WriteLine(u.name + "가 배치를 못하고 턴을 넘김");
                        }
                        break;

                    case "COUNT":
                        userDic[int.Parse(data[1])].SetCount(int.Parse(data[2]));
                        break;

                    case "CHAT":  //챗
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
                        WriteLine(userDic[ID].name + "가 방을 만듦");
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
                            SendMsg(u.stream, "방 아이디를 다시 확인해주세요.", CommandHead.SYSTEM_MSG);
                        }
                        break;

                    case "EXIT":
                        u = userDic[int.Parse(data[1])];
                        if (u.isRoom)
                        {
                            u.currentRoom.LeaveRoom(u);
                        }
                        break;

                    case "GAME START":  //게임 시작
                        ID = int.Parse(data[1]);
                        userDic[ID].currentRoom.StartGame(ID);
                        break;

                    case "GAME END":
                        userDic[int.Parse(data[1])].currentRoom.EndGame();
                        break;

                    case "MATCHING":
                        ID = int.Parse(data[1]);
                        matchingUsers.Add(userDic[ID]);
                        WriteLine("매칭 시작 : " + userDic[ID].name);
                        break;

                    case "CANCEL MATCHING":
                        u = userDic[int.Parse(data[1])];
                        matchingUsers.Remove(u);
                        SendMsg(u.stream, "NULL", CommandHead.CANCEL_MATCHING);
                        WriteLine("매칭 취소 : " + u.name);
                        break;

                    default:  //정의하지 않은 헤드가 존재함
                        WriteLine("존재하지 않는 프로토콜 (head) : " + data[0]);
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
                //WriteLine("메시지 전송");
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
            WriteLine("유저 접속 ID: " + id + "  IP: " + client.Client.RemoteEndPoint.ToString());
        }

        public void Init(string name)
        {
            this.name = name;
            WriteLine(name + "님이 접속");

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
            WriteLine($"{name}님의 개수: {currentMyCellCnt}");
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

        private UserData first, second; // 각각 선공, 후공

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
                Program.SendMsg(user.stream, "이미 게임을 시작한 방입니다.", CommandHead.SYSTEM_MSG);
                return;
            }
            if (currentCount >= maxCount)
            {
                Program.SendMsg(user.stream, "해당 방은 인원이 꽉 찼습니다.", CommandHead.SYSTEM_MSG);
                return;
            }

            userList.Add(user);
            currentCount++;
            user.SetRoom(this);

            WriteLine(user.name + "가 방에 참가함. 방 아이디: " + roomID.ToString());
            Program.Broadcast(userList, user.name + "님이 참가하였습니다.", CommandHead.SYSTEM_MSG);
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
            WriteLine(user.name + "님이 방을 나감");

            if (isGameStart)
            {
                user.currentMyCellCnt = 0;
                user.first = false;
                user.myTurn = false;
            }

            if (currentCount == 0) //방에 아무도 없음 ==> 방 폭파
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
                Program.SendMsg(Program.userDic[owner.id].stream, "게임을 시작하기위한 인원이 부족합니다.", CommandHead.SYSTEM_MSG);
                return;
            }

            for (int i = 0; i < 8; i++)
            {
                List<Board> list = new List<Board>();
                for (int j = 0; j < 8; j++)
                {
                    list.Add(new Board(j, i));  //i,j를 대입할 때 +1을 할 수도 있겠지만 나중에 귀찮아질 것 같으니 그냥 0~7로 감
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

            Program.Broadcast(userList, first.id.ToString(), CommandHead.GAME_START); //먼저 시작하는 사람의 아이디를 보냄
            WriteLine("게임이 시작됨. 시작된 방 아이디: " + roomID);

            firstOwner = !firstOwner;  //담턴에는 방장 아닌 놈이 먼저시작하게끔 해준다. (1번째가 먼저 시작하게 해준다)
        }

        public void LayGameCell(int id, int x, int y) //원래 서버에서 조건 검사해야할것 같지만 귀찮음.
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

            int winnerId = -1; //무승부 시엔 -1을 보냄
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
            WriteLine("게임 끝. 승자 : " + Program.userDic[winnerId].name);

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
