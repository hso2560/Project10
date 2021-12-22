using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class GameManager : MonoBehaviour  
{
    //초록색: 선공  (3,3), (4,4)  ,   빨간색: 후공  (4,3), (3,4)

    public static GameManager instance;

    public Text timeText;

    [HideInInspector] public Camera mainCam;
    public Board board;

    RaycastHit hit;
    Ray ray;

    [SerializeField] private float limitTime = 30f;
    private float currentTime = 0f;

    private bool myTurn;

    public bool IsStopped
    {
        get;
        set;
    }

    private void Awake()
    {
        instance = this;
        IsStopped = false;
    }

    private void Update()
    {
        PlaceObject();
        FlowTime();
    }

    private void FlowTime()
    {
        if (myTurn)
        {
            currentTime -= Time.deltaTime;
            timeText.text = ((int)currentTime).ToString();
            if (currentTime <= 0)
            {
                SocketClient.instance.TimeOver();
                myTurn = false;
            }
        }
        else
        {
            timeText.text = "30";
        }
    }

    public void ChangeTurn(bool myTurn)
    {
        this.myTurn = myTurn;
        if(myTurn)
           currentTime = limitTime;
    }

    private void PlaceObject()
    {
        if(Input.GetMouseButtonDown(0) && !IsStopped)
        {
            if(SocketClient.instance.CanPlaceObject())
            {
                ray = mainCam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out hit ,10000f, LayerMask.GetMask("Cell")))
                {
                    Cell cell = hit.transform.GetComponent<Cell>();
                    if(CanPlaceObject(cell))
                    {
                        SocketClient.instance.LayGameCell(cell.x,cell.y);
                    }
                }
            }
        }
    }

    private bool CanPlaceObject(Cell c)
    {
        if (c.isPress) return false;
        if (!board.CanLay(SocketClient.instance.ClientID, c.x, c.y)) return false;  

        return true;
    }

    public void OnClickQuitBtn()
    {
        Application.Quit();
    }
}
