using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public int id;
    public bool connected;
    public bool isMine;

    public string nickname;
    //public int currentRoomID = -1;

    public void Init(int id, bool isMine, string nick)
    {
        this.id = id;
        this.isMine = isMine;
        nickname = nick;
        connected = true;
    }
}
