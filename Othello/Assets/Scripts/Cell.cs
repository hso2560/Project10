using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cell : MonoBehaviour
{
    public int x, y;
    public bool isPress;
    public int userID;

    public Vector3 cellPos;

    [HideInInspector] public GameObject stone;

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
            board.SetColor(stone.GetComponent<MeshRenderer>(), id);
        }
        else
        {
            board.SetColor(stone.GetComponent<MeshRenderer>(), id);
        }
    }

    public void SetInit(int id)
    {
        userID = id;
        isPress = true;
    }

    public void Clear()
    {
        isPress = false;
        if(stone)
        {
            Destroy(stone);
            stone = null;
        }
        userID = -1;
    }
}
