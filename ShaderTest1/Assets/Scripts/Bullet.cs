using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public long serverID;
    public int playerID;
    public int damage = 100;
    public float speed = 5.5f;

    //private Vector3 dir;
    private bool moveStart;

    private float elapsed = 0f;
    private float t = 3f;

    /*public void SetInit(Vector3 start, Vector3 dir, int id, long sID)
    {
        playerID = id;
        serverID = sID;
        transform.position = start;
        this.dir = dir;
        moveStart = true;
    }*/

    public void SetInit(Vector3 start, float rz, int id, long sID)
    {
        playerID = id;
        serverID = sID;
        transform.position = start;
        transform.rotation = Quaternion.Euler(0, 0, rz);
        moveStart = true;
    }

    private void Update()
    {
        if (moveStart)
        {
            //transform.Translate(dir * speed * Time.deltaTime);
            transform.Translate(Vector2.right * speed * Time.deltaTime);
            elapsed += Time.deltaTime;

            if (elapsed > t)
            {
                Inactive();
            }
        }
    }

    public void Inactive()
    {
        elapsed = 0f;
        moveStart = false;

        Client.instance.bulletDict.Remove(serverID);
        Client.instance.InsertBullet(gameObject);
    }
}
