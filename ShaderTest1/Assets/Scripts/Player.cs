using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    public int id;
    public bool connected;
    public string nickname;
    public int maxHp = 1000;
    public int hp;

    public bool isMine;
    public bool dead;

    public bool isAtk;

    public float speed = 5.5f;

    public Text nickTxt;
    public Image hpFill;
    private SpriteRenderer spr;

    private WaitForSeconds ws = new WaitForSeconds(0.06f);

    public Vector3 target = Vector3.zero;

    private void Awake()
    {
        spr = GetComponent<SpriteRenderer>();
    }

    public void SetData(int id, int hp, Vector2 pos,bool dead ,bool isMine, string nick)
    {
        this.id = id;
        transform.position = pos;
        this.isMine = isMine;
        nickname = nick;
        connected = true;

        if (isMine)
        {
            this.hp = maxHp;
            this.dead = false;
            StartCoroutine(SyncPosition());
        }
        else
        {
            this.hp = hp;
            this.dead = dead;
        }

        hpFill.fillAmount = (float)this.hp / maxHp;
        nickTxt.text = nickname;

        spr.color = isMine ? Color.yellow : Color.red;
    }

    private void Update()
    {
        Move();
    }

    private void Move()
    {
        if (!connected) return;

        if (isMine && !dead)
        {
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");
            Vector3 dir = new Vector2(x, y).normalized;

            transform.position += dir * speed * Time.deltaTime;
        }
        
        if(!isMine)
        {
            transform.position = Vector3.Lerp(transform.position, target, speed * Time.deltaTime);
        }
    }

    public void Damaged(int damage, int attacker)
    {
        hp -= damage;
        if (hp <= 0)
        {
            hp = 0;
        }
        hpFill.fillAmount = (float)this.hp / maxHp;

        if (hp == 0 && isMine)
        {
            Client.instance.Dead(attacker);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isMine) return;

        if (collision.CompareTag("Bullet"))
        {
            Bullet b = collision.GetComponent<Bullet>();
            if(connected && !dead && b.playerID != id)
            {
                Client.instance.Damaged(b.serverID, b.playerID, id, b.damage);
            }
        }
    }

    private IEnumerator SyncPosition()
    {
        while (connected)
        {
            yield return ws;
            if (!dead)
            {
                Client.instance.SendPosition(transform.position.x, transform.position.y);
            }
        }
    }
}
