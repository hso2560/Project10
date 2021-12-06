using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public GameObject gameInteracUI;

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.H))
        {
            gameInteracUI.SetActive(!gameInteracUI.activeSelf);
        }
    }
}
