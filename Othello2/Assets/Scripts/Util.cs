using System;
using System.Collections.Generic;
using UnityEngine;

public static class Util 
{
    public static bool CheckDiagonal(int x1, int x2, int y1, int y2)
    {
        if (Mathf.Abs(x2 - x1) != Mathf.Abs(y2 - y1)) return false;
        if (Mathf.Abs(x2 - x1) < 2) return false;
        return true;
    }

    public static Vector2Int[] GetDiagonalCoords(int x1, int x2, int y1, int y2)
    {
        int l = Mathf.Abs(x2 - x1) - 1;
        Vector2Int[] arr = new Vector2Int[l];

        int k1 = x2 > x1 ? 1 : -1;
        int k2 = y2 > y1 ? 1 : -1;

        for(int i = 0; i<l; i++)
        {
            arr[i] = new Vector2Int(x1+(k1*(i+1)),y1+ (k2 * (i + 1)) );
        }

        return arr;
    }

}
