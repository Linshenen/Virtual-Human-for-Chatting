using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using Live2D.Cubism.Framework.Expression;

public class ChatScript : MonoBehaviour
{
    // API key
    [SerializeField]private string m_OpenAI_Key;
	
    // 定义Chat API的URL
	// private string m_ApiUrl = "https://ark.cn-beijing.volces.com/api/v3/chat/completions";

    // 配置参数
    // [SerializeField]private GetOpenAI.PostData m_PostDataSetting;

    // 微软Azure语音
    [SerializeField]private AzureSpeech m_AzurePlayer;

    // gpt-3.5-turbo
    [SerializeField]public GptTurboScript m_GptTurboScript;

    // 语言
    [SerializeField]private string m_lan="使用中文回答";

    // 气泡
    [SerializeField]private GameObject bubbleUI;

    // 接收回答的UI文本框
    [SerializeField]private TMP_Text answerText;

    // 模型表情列表
    private ExpressionController expressionController;

    // 回答文本
    private string m_Msg = null;

    private void Start() {
        // 订阅事件
        GameSettingsEvent.OnGameSettingChanged += SetGhatGPTSettings;
        AzureSpeech.OnSpeechStart += OnSpeechStarted; // 改为空操作或用于其他目的

        m_OpenAI_Key = GameSettingsEntity.Instance.ChatGPTAPI;
        expressionController = GameObject.FindGameObjectWithTag("Model").GetComponent<ExpressionController>();
        
    }
    public void SetGhatGPTSettings(){
        m_OpenAI_Key = GameSettingsEntity.Instance.ChatGPTAPI;
    }



    // 发送信息
    public void SendData(string _postData)
    {
        if (_postData.Equals("")){
            return;
        }
        string _msg = m_GptTurboScript.m_Prompt + m_lan + " " + _postData;
        //发送数据
        StartCoroutine(m_GptTurboScript.GetPostData(_msg, m_OpenAI_Key, CallBack));
    }


    // AI回复的信息
    private void CallBack(string _callback){
        m_Msg = _callback.Trim();
        // 语音功能
        StartCoroutine(Speek(m_Msg));
    }


    private IEnumerator Speek(string _msg){
        // 立即开始角色动画
        StartCharacterAnimation();
        
        // 立即显示文字
        ShowTextImmediately(_msg);
        
        // 立即开始TTS，使用默认情感风格
        TTSBuilder tTSBuilder = new BasicTTSBuilder();
        TTSEntity tTSEntity = tTSBuilder.build(_msg);
        tTSEntity.Style[0] = "customerservice"; // 默认风格
        
        // 异步进行情感分析，不阻塞主线程
        StartCoroutine(SentimentAnalysisAsync(_msg, tTSEntity));
        
        // 立即开始TTS，不等待情感分析
        m_AzurePlayer.TurnTextToSpeech(tTSEntity);
        
        yield break; // 协程需要至少一个 yield 语句
    }
    
    private IEnumerator SentimentAnalysisAsync(string msg, TTSEntity ttsEntity)
    {
        // 在后台线程中执行情感分析
        yield return new WaitForEndOfFrame();
        
        SAService sAService = new SAService();
        SAEntity sAEntity = sAService.sentimentAnalysis(msg);
        
        // 如果情感分析完成且TTS还在准备中，可以更新情感风格
        // 注意：这里需要检查TTS是否已经开始播放，避免中途切换风格
        if (sAEntity != null && sAEntity.Style != "customerservice")
        {
            // 更新表情控制器
            if (expressionController != null)
            {
                expressionController.setCurrentExpression(sAEntity.Style);
                Debug.Log($"情感分析完成，更新表情风格为: {sAEntity.Style}");
            }
        }
    }
    
    private void StartCharacterAnimation()
    {
        // 获取角色动画控制器并开始说话动画
        Animator animator = GameObject.FindGameObjectWithTag("Model").GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetBool("isSpeaking", true);
            animator.SetInteger("idleState", -1);
            Debug.Log("角色说话动画已开始");
        }
    }

    private void ShowText(){
        if(m_Msg != null){
            bubbleUI.SetActive(true);   // 开启气泡
            answerText.text = "";
            //开始逐个显示返回的文本
            m_WriteState = true;
            StartCoroutine(SetTextPerWord(m_Msg));
            m_Msg = null;
        }
    }
    
    private void ShowTextImmediately(string msg){
        bubbleUI.SetActive(true);   // 开启气泡
        answerText.text = msg;      // 立即显示完整文本
        m_WriteState = false;       // 停止逐字显示
    }
    
    private void OnSpeechStarted()
    {
        // 文字已经立即显示，这里可以用于其他目的
        Debug.Log("TTS音频开始播放");
    }

    //逐字显示的时间间隔
    private float m_WordWaitTime=0.2f;
    //是否显示完成
    private bool m_WriteState=false;
    private IEnumerator SetTextPerWord(string _msg){
        // yield return new WaitUntil(() => m_AzurePlayer. == false);
        int currentPos=0;
        while(m_WriteState){
            yield return new WaitForSeconds(m_WordWaitTime);
            currentPos++;
            //更新显示的内容
            answerText.text =_msg.Substring(0,currentPos);

            m_WriteState=currentPos<_msg.Length;

        }
        yield return new WaitForSeconds(3f);
        bubbleUI.SetActive(false);
    }



}
