using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    private Queue<string> systemMsgQueue = new Queue<string>();
    private WaitForSeconds ws = new WaitForSeconds(0.2f);

    public RectTransform systemPanelTrm;
    public Text systemText;

    private Vector2 systemPanelStartPos;

    private void Awake()
    {
        instance = this;
        systemPanelStartPos = systemPanelTrm.anchoredPosition;
    }

    private void Start()
    {
        StartCoroutine(SystemMsgCo());
    }

    public void SystemMsgPopup(string msg)
    {
        systemMsgQueue.Enqueue(msg);
    }

    private IEnumerator SystemMsgCo()
    {
        while(true)
        {
            yield return ws;
            if(systemMsgQueue.Count>0)
            {
                string s = systemMsgQueue.Dequeue();
                systemText.text = s;

                systemPanelTrm.DOAnchorPos(new Vector2(0, 250), 0.8f).SetEase(Ease.OutBack);
                yield return new WaitForSeconds(3);

                systemPanelTrm.DOAnchorPos(systemPanelStartPos, 0.8f).SetEase(Ease.InBack);
                yield return new WaitForSeconds(1);
            }
        }
    }
}
