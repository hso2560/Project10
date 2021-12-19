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
    public InputField chatInput;

    private Vector2 systemPanelStartPos;

    public Transform ChatTextParent;
    public GameObject chatPanel, chatNotice;
    public GameObject chatTxtPref;
    public Scrollbar chatScroll;

    public GameObject resultPanel;
    public Text resultTxt;

    private Queue<bool> UIQueue = new Queue<bool>();

    public CanvasGroup loadingPanel;

    private void Awake()
    {
        if (!loadingPanel.gameObject.activeSelf) loadingPanel.gameObject.SetActive(true);

        instance = this;
        systemPanelStartPos = systemPanelTrm.anchoredPosition;
        chatPanel.SetActive(false);
        chatPanel.transform.localScale = Vector3.zero;

        loadingPanel.DOFade(0, 0.7f).OnComplete(() => loadingPanel.gameObject.SetActive(false));
    }

    private void Start()
    {
        StartCoroutine(SystemMsgCo());
    }

    public void ChatPanelOnOff(bool on)
    {
        if (UIQueue.Count > 1) return;

        UIQueue.Enqueue(false);

        if (on)
        {
            if(chatPanel.activeSelf)
            {
                UIQueue.Dequeue();
                return;
            }

            chatPanel.SetActive(true);
            GameManager.instance.IsStopped = true;
            chatNotice.SetActive(false);
        }

        chatPanel.transform.DOScale(on ? Vector3.one : Vector3.zero, 0.5f).OnComplete(() =>
        {
            if (!on)
            {
                chatPanel.SetActive(false);
                GameManager.instance.IsStopped = false;
            }
            UIQueue.Dequeue();
        });
    }

    public void Chat(string msg)
    {
        Text ct = Instantiate(chatTxtPref, ChatTextParent).GetComponent<Text>();
        ct.text = msg;
        chatScroll.value = 0;
        ChatTextParent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, int.MaxValue);

        if (!chatPanel.activeSelf)
        {
            chatNotice.SetActive(true);
        }
    }

    public void ClearChat()
    {
        if(ChatTextParent.childCount>0)
        {
            for(int i=0; i<ChatTextParent.childCount; i++)
            {
                Destroy(ChatTextParent.GetChild(i).gameObject);
            }
        }
        if(chatPanel.activeSelf)
        {
            chatPanel.SetActive(false);
            GameManager.instance.IsStopped = false;
            chatPanel.transform.localScale = Vector3.zero;
        }
        ChatTextParent.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
    }

    public void GameResult(string result)
    {
        if (resultPanel)
        {
            resultPanel.transform.localScale = Vector3.zero;
            resultPanel.SetActive(true);
            resultPanel.transform.DOScale(Vector3.one, 0.4f);
            resultTxt.text = result;

            switch(result)
            {
                case "½Â¸®":
                    resultTxt.color = Color.yellow;
                    break;
                case "¹«½ÂºÎ":
                    resultTxt.color = Color.white;
                    break;
                case "ÆÐ¹è":
                    resultTxt.color = new Color(1, 0, 1, 1);
                    break;
                default:
                    break;
            }
        }
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            if(chatPanel.activeSelf)
            {
                ChatPanelOnOff(false);
            }
        }
        else if (Input.GetKeyDown(KeyCode.Return))
        {
            if(chatPanel.activeSelf)
               SocketClient.instance.SendChatMsg(chatInput);
        }
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
