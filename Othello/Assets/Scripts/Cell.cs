using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cell : MonoBehaviour
{
    public int x, y;
    public bool isPress;
    public int userID;

    [SerializeField] private bool defaultStone;

    public Vector3 cellPos;

    public GameObject stone;

    private Board board;

    private void Awake()
    {
        board = transform.parent.GetComponent<Board>();
    }

    public void SetInit(int x,int y)
    {
        this.x = x;
        this.y = y;
    }

    public void PlaceObject(int id)
    {
        userID = id;
        isPress = true;

        if(!stone)
        {
            stone = Instantiate(board.cellPrefab, transform);
            stone.transform.localPosition = cellPos;
            stone.transform.localRotation = Quaternion.Euler(0, 0, 0);
            stone.transform.localScale = board.stoneLocalScale;
            board.SetColor(stone.GetComponent<MeshRenderer>(), id, x, y);
        }
        else
        {
            board.SetColor(stone.GetComponent<MeshRenderer>(), id, x, y);
        }
    }

    public void SetColor(Material m, int id)
    {
        stone.GetComponent<MeshRenderer>().material = m;
        userID = id;
    }

    public void SetInit(int id, bool start=false)
    {
        userID = id;
        isPress = true;
        if (start && !stone)
        {
            stone = transform.GetChild(0).gameObject;
        }
    }

    public void Clear()
    {
        isPress = false;
        if(stone && !defaultStone)
        {
            Destroy(stone);
            stone = null;
        }
        userID = -1;
    }
}
