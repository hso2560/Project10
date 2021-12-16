using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Board : MonoBehaviour
{
    public List<Cell> cellList;

    public List<List<Cell>> cells = new List<List<Cell>>();  // y좌표는 밑으로 갈 수록 커짐

    public int testX, testY;

    public Vector3 stoneLocalScale;
    public Material fGreenMat, sRedMat;
    public GameObject cellPrefab;
    private bool first;

    private void Awake()
    {
        int index = 0;
        for(int i=0; i<8; i++)
        {
            List<Cell> list = new List<Cell>();
            for(int j=0; j<8; j++)
            {
                cellList[index].SetInit(j, i);
                list.Add(cellList[index]);
                index++;
            }
            cells.Add(list);
        }
    }

    private void Start()
    {
        SocketClient.instance.mainCam.cullingMask = ~(1 << 6);
    }

    public void GameStart(bool first)
    {
        int mi = SocketClient.instance.myPlayer.id;
        int oi = SocketClient.instance.otherPlayer.id;
        this.first = first;

        cells[3][3].SetInit(first ? mi : oi, true);
        cells[4][4].SetInit(first ? mi : oi, true);
        cells[3][4].SetInit(first ? oi : mi, true);
        cells[4][3].SetInit(first ? oi : mi, true);
    }

    public void SetColor(MeshRenderer _mesh, int id, int x, int y)
    {
        Material mat = SocketClient.instance.ClientID == id ? (first ? fGreenMat : sRedMat) : (first ? sRedMat : fGreenMat);
        _mesh.material = mat;

        List<Cell> changeObjs = new List<Cell>();
        int i, j, k, maxCnt, cnt = 0;

        for (i = 0; i < 8; i++)  //가로
        {
            if (Mathf.Abs(x - i) < 2) continue;

            if (cells[y][i].userID == id)
            {
                k = x > i ? 1 : -1;
                maxCnt = Mathf.Abs(x - i) - 1;

                for (j = i + k; j != x; j += k)
                {
                    if (cells[y][j].userID == id || cells[y][j].userID == -1) break;
                    else cnt++;
                }
                if (cnt == maxCnt)
                {
                    for (j = i + k; j != x; j += k)
                    {
                        if(!changeObjs.Contains(cells[y][j]))
                            changeObjs.Add(cells[y][j]);
                    }
                }
            }
        }
        cnt = 0;
        for (i = 0; i < 8; i++)  //세로
        {
            if (Mathf.Abs(y - i) < 2) continue;

            if (cells[i][x].userID == id)
            {
                k = y > i ? 1 : -1;
                maxCnt = Mathf.Abs(y - i) - 1;

                for (j = i + k; j != y; j += k)
                {
                    if (cells[j][x].userID == id || cells[j][x].userID == -1) break;
                    else cnt++;
                }
                if (cnt == maxCnt)
                {
                    for (j = i + k; j != y; j += k)
                    {
                        if (!changeObjs.Contains(cells[j][x]))
                            changeObjs.Add(cells[j][x]);
                    }
                }
            }
        }
        cnt = 0;

        //대각선
        for (i = 0; i < 8; i++)
        {
            for (j = 0; j < 8; j++)
            {
                if (cells[i][j].userID == id && Util.CheckDiagonal(j, x, i, y))
                {
                    Vector2Int[] arr = Util.GetDiagonalCoords(j, x, i, y);
                    maxCnt = arr.Length;

                    for (k = 0; k < maxCnt; k++)
                    {
                        if (cells[arr[k].y][arr[k].x].userID == id || cells[arr[k].y][arr[k].x].userID == -1) break;
                        else cnt++;
                    }

                    if (cnt == maxCnt)
                    {
                        for (k = 0; k < maxCnt; k++)
                        {
                            if (!changeObjs.Contains(cells[arr[k].y][arr[k].x]))
                                changeObjs.Add(cells[arr[k].y][arr[k].x]);
                        }
                    }
                }
            }
        }

        changeObjs.ForEach(x => x.SetColor(mat, id));

        SendCellCount();
    }

    private void SendCellCount()
    {
        int id = SocketClient.instance.ClientID;
        int count = 0;

        cellList.ForEach(x =>
        {
            if (x.userID == id)
            {
                count++;
            }
        });

        SocketClient.instance.SendCount(count);
    }

    public bool CanLay(int id, int x, int y)
    {
        int i, j, k, maxCnt, cnt = 0;

        for(i=0; i<8; i++)  //가로축 검사
        {
            if (Mathf.Abs(x-i)<2) continue;

            if (cells[y][i].userID == id)
            {
                k = x > i ? 1 : -1;
                maxCnt = Mathf.Abs(x - i) - 1;
                //Debug.Log("3: " + i + " " + k + " "+maxCnt);

                for (j=i+k; j!=x; j += k)
                {
                    //Debug.Log("4: " + j);
                    if (cells[y][j].userID == id || cells[y][j].userID == -1) break;
                    else cnt++;
                }
                if (cnt == maxCnt)
                {
                    return true;
                }
            }
        }
        cnt = 0;
        for (i = 0; i < 8; i++)  //세로축 검사
        {
            if (Mathf.Abs(y - i) < 2) continue;

            if (cells[i][x].userID == id)
            {
                k = y > i ? 1 : -1;
                maxCnt = Mathf.Abs(y - i) - 1;

                for (j = i + k; j != y; j += k)
                {
                    if (cells[j][x].userID == id || cells[j][x].userID == -1) break;
                    else cnt++;
                }
                if (cnt == maxCnt)
                {
                    return true;
                }
            }
        }
        cnt = 0;

        //대각선 검사
        for (i = 0; i < 8; i++)
        {
            for(j=0; j<8; j++)
            {
                if(cells[i][j].userID == id && Util.CheckDiagonal(j,x,i,y))
                {
                    Vector2Int[] arr = Util.GetDiagonalCoords(j, x, i, y);
                    maxCnt = arr.Length;

                    for(k=0; k<maxCnt; k++)
                    {
                        if (cells[arr[k].y][arr[k].x].userID == id || cells[arr[k].y][arr[k].x].userID == -1) break;
                        else cnt++;
                    }

                    if (cnt == maxCnt)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public void Clear()
    {
        SocketClient.instance.mainCam.cullingMask = ~(1 << 6);
        cellList.ForEach(x => x.Clear());
    }

    /*private void Update()
    {
        if(Input.GetKeyDown(KeyCode.T))
        {
            Cell c = cells[testY][testX];
            Debug.Log($"x: {c.x}, y: {c.y}");
        }
    }*/
}
