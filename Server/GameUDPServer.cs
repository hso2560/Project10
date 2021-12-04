using System;
using static System.Console;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace UDPServer
{
    class Program
    {
        private IPEndPoint ipep;
        public static Socket server;

        private int port = 5556;
        private string ip = "127.0.0.1";
        private object lockObj = new object();

        public static Dictionary<int, UserData> userDic = new Dictionary<int, UserData>();
        public static Dictionary<int, Room> roomDic = new Dictionary<int, Room>();

        public int index = 0; //���� ���̵�
        public int roomIndex = 0; //�� ���̵�

        public string rtStr;
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
                    rtStr = remote.ToString();
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
                        string[] strs = rtStr.Split(':');
                        Program.userDic.Add(index, new UserData(index, data[1], strs[0], strs[1], remote));
                        Broadcast(index.ToString() + "#" + data[1], "ENTER");
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
                        Broadcast(msg);
                        Program.userDic.Remove(ID);
                        break;

                    case "POSITION":  //��ġ ����ȭ
                        ID = int.Parse(data[1]);
                        userDic[ID].x = float.Parse(data[2]);
                        userDic[ID].y = float.Parse(data[3]);
                        Broadcast(msg);
                        break;

                    case "ENTER ROOM":  //�� ����
                        ID = int.Parse(data[1]);
                        int create = int.Parse(data[2]);  //0: �� ����,  1: �� ����
                        if (create == 0)
                        {
                            roomDic.Add(roomIndex, new Room(roomIndex, data[3], int.Parse(data[4]), ID));
                            roomIndex++;
                            WriteLine(userDic[ID].name + "���� ���� ������");
                        }
                        else
                        {
                            if (roomDic.ContainsKey(int.Parse(data[3])))
                            {
                                roomDic[int.Parse(data[3])].AddUser(userDic[ID]);
                                WriteLine(userDic[ID].name + "���� �濡 �����Ͽ����ϴ�. �� �̸�: " + roomDic[int.Parse(data[3])].roomName);
                            }
                        }
                        break;

                    case "EXIT ROOM":  //�� ����
                        ID = int.Parse(data[1]);
                        roomDic[userDic[ID].currentRoom.roomID].LeaveRoom(userDic[ID]);
                        WriteLine(userDic[ID].name + "���� �濡�� ������.");
                        break;

                    case "CHAT":  //ä��
                        ID = int.Parse(data[1]);
                        if (!userDic[ID].isRoom)
                        {
                            Broadcast(msg, "", true);
                        }
                        else
                        {
                            Broadcast(userDic[ID].currentRoom.userList, msg);
                        }
                        WriteLine("ä��: " + userDic[ID].name + ":" + data[2]);
                        break;

                    default:  //�������� ���� ��尡 ������
                        WriteLine("�������� �ʴ� �������� (head) : " + data[0]);
                        break;
                }
            }
        }

        public void EndServer()
        {
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

        public static void Broadcast(string msg, string head = "", bool lobby = false)
        {
            if (!lobby)
            {
                foreach (int key in userDic.Keys)
                {
                    SendMsg(userDic[key].ep, msg, head);
                }
            }
            else
            {
                foreach (int key in userDic.Keys)
                {
                    if (!userDic[key].isRoom)
                        SendMsg(userDic[key].ep, msg, head);
                }
            }
        }

        public static void Broadcast(List<UserData> users, string msg, string head = "")
        {
            for (int i = 0; i < users.Count; i++)
            {
                SendMsg(users[i].ep, msg, head);
            }
        }
    }

    class UserData
    {
        public int id;
        public string name;
        public bool connected;

        public string ip;
        public int port;

        public float x, y;
        public int hp;
        public bool dead;

        public EndPoint ep;

        public bool isRoom;
        public Room currentRoom;

        //private byte[] bytes;
        //private string str="";
        //Thread _thread;

        public UserData() { }

        public UserData(int id, string name, string ip, string port, EndPoint ep)
        {
            this.id = id;
            this.name = name;
            connected = true;

            this.ip = ip;
            this.port = int.Parse(port);
            this.ep = ep;

            hp = 1000;
            isRoom = false;
            currentRoom = null;

            Program.SendMsg(this.ep, id.ToString(), "INIT");

            StringBuilder sb = new StringBuilder();
            foreach (int key in Program.userDic.Keys)
            {
                if (key != id)
                {
                    UserData user = Program.userDic[key];

                    sb.Append(key.ToString());
                    sb.Append("#");
                    sb.Append(user.x.ToString());
                    sb.Append("#");
                    sb.Append(user.y.ToString());
                    sb.Append("#");
                    sb.Append(user.hp.ToString());
                    sb.Append("#");
                    sb.Append(user.name);

                    Program.SendMsg(this.ep, sb.ToString(), "HISTORY");
                    sb.Clear();
                }
            }
            //_thread = new Thread(Work);
            //_thread.Start();
        }

        public void SetRoom(Room room = null)
        {
            this.currentRoom = room;
            this.isRoom = room != null;
        }

        /*private void Work()
        {
            while(connected)
            {
                try
                {
                    
                }
                catch(Exception e)
                {
                    Disconnect();
                    WriteLine(e.ToString());
                }
            }
        }*/

        /*private void Disconnect()
        {
            connected = false;
            _thread.Abort();
        }*/
    }

    class Room
    {
        public int roomID;
        public string roomName;
        public int maxCount;
        public int currentCount;

        public UserData owner;
        public List<UserData> userList = new List<UserData>();

        public Room() { }

        public Room(int roomID, string roomName, int maxCount, int ownerID)
        {
            this.roomID = roomID;
            this.roomName = roomName;
            this.maxCount = maxCount;
            this.owner = Program.userDic[ownerID];

            currentCount = 1;
            userList.Add(Program.userDic[ownerID]);

            owner.SetRoom(this);
            Program.SendMsg(owner.ep, $"{owner.name}#{currentCount}#�� ���� �Ϸ�", "UPDATE ROOM");
        }

        public void AddUser(UserData user)
        {
            if (currentCount >= maxCount)
            {
                Program.SendMsg(user.ep, "�ش� ���� �ִ� �ο� ���� �����ؼ� ������ �� �����ϴ�.", "SYSTEM MSG");
                WriteLine(user.name + "���� " + roomName + "�濡 �������� �õ��߽��ϴ�.");
                return;
            }

            userList.Add(user);
            currentCount++;
            user.SetRoom(this);
            Program.Broadcast(userList, $"{owner.name}#{currentCount}#'{user.name}'���� '{roomName}'�濡 �����Ͽ����ϴ�.", "UPDATE ROOM");
        }

        public void LeaveRoom(UserData user)
        {
            currentCount--;
            user.SetRoom();
            userList.Remove(user);

            Program.SendMsg(user.ep, "�� ����", "SYSTEM MSG");
            if (currentCount == 0) //�濡 �ƹ��� ���� ==> �� ����
            {
                Program.roomDic.Remove(roomID);
                WriteLine(roomName + " �� �ı�");
            }
            else
            {
                if (user == owner) //���� ���� �����̸� �ι�°�� ���� ���� �������� �ٲ���
                {
                    owner = userList[0];
                    Program.SendMsg(owner.ep, "����� �����Դϴ�.", "SYSTEM MSG");
                }
                Program.Broadcast(userList, $"{owner.name}#{currentCount}#'{user.name}'���� �濡�� �������ϴ�.", "UPDATE ROOM");
            }
        }
    }
}




/*UdpClient client = new UdpClient(10611);
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

            byte[] dgram = client.Receive(ref remoteEP);
            client.Send(dgram, dgram.Length, remoteEP);
            client.Close();*/
