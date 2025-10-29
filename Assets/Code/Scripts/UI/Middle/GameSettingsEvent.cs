using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameSettingsEvent : MonoBehaviour
{
    [SerializeField] private List<GameObject> gameObjects;
    private GameSettingsEntity gameSettingsEntity;
    [HideInInspector]public static event Action OnGameSettingChanged;

    private void Start() {
        gameSettingsEntity = GameSettingsEntity.Instance;
        
        // 确保设置 UI 正确初始化
        InitGameSettings();
        
        // 检查 API Key 配置
        CheckApiKeyConfiguration();
    }
    
    private void CheckApiKeyConfiguration() {
        string configFilePath = Path.Combine(Application.persistentDataPath, "GameSettings.json");
        Debug.Log("=== API Key 配置检查 ===");
        Debug.Log($"ChatGPT API Key: {(string.IsNullOrEmpty(gameSettingsEntity.ChatGPTAPI) ? "未设置" : "已设置")}");
        Debug.Log($"Azure API Key (Minimax): {(string.IsNullOrEmpty(gameSettingsEntity.AzureAPI) ? "未设置" : "已设置")}");
        Debug.Log($"APISpace API Key: {(string.IsNullOrEmpty(gameSettingsEntity.APISpaceAPI) ? "未设置" : "已设置")}");
        
        if (string.IsNullOrEmpty(gameSettingsEntity.AzureAPI)) {
            Debug.LogWarning("⚠️  Minimax API Key 未设置！语音合成功能将使用模拟模式");
        }
        
        if (string.IsNullOrEmpty(gameSettingsEntity.APISpaceAPI)) {
            Debug.LogWarning("⚠️  LLM API Key 未设置！情感分析功能将使用默认结果");
        }
        
        Debug.Log("========================");
    }
    public void SaveGameSettings(){
        bool isGameSettingChanged = false;
        if(gameObjects[0].GetComponent<Toggle>().isOn){
            if(gameSettingsEntity.LookTargetMode != 0){
                gameSettingsEntity.LookTargetMode = 0;
                isGameSettingChanged = true;
            }
        }else{
            if (gameSettingsEntity.LookTargetMode != 1)
            {
                gameSettingsEntity.LookTargetMode = 1;
                isGameSettingChanged = true;
            }
        }
        
        if(gameObjects[1].GetComponent<TMP_Dropdown>().value == 0){
            if(!gameSettingsEntity.Speaker.Equals("zh-CN-XiaoxiaoNeural")){
                gameSettingsEntity.Speaker = "zh-CN-XiaoxiaoNeural";
                isGameSettingChanged = true;
            }
        }
        else if(gameObjects[1].GetComponent<TMP_Dropdown>().value == 1){
            if (!gameSettingsEntity.Speaker.Equals("zh-CN-XiaoyiNeural"))
            {
                gameSettingsEntity.Speaker = "zh-CN-XiaoyiNeural";
                isGameSettingChanged = true;
            }
        }
        if(!gameSettingsEntity.ChatGPTAPI.Equals(gameObjects[2].GetComponent<TMP_InputField>().text)){
            gameSettingsEntity.ChatGPTAPI = gameObjects[2].GetComponent<TMP_InputField>().text;
            isGameSettingChanged = true;
        }
        if (!gameSettingsEntity.AzureAPI.Equals(gameObjects[3].GetComponent<TMP_InputField>().text))
        {
            gameSettingsEntity.AzureAPI = gameObjects[3].GetComponent<TMP_InputField>().text;
            isGameSettingChanged = true;
            Debug.Log($"AzureAPI (Minimax) 已更新为: {gameSettingsEntity.AzureAPI.Substring(0, Math.Min(10, gameSettingsEntity.AzureAPI.Length))}...");
        }
        if (!gameSettingsEntity.APISpaceAPI.Equals(gameObjects[4].GetComponent<TMP_InputField>().text))
        {
            gameSettingsEntity.APISpaceAPI = gameObjects[4].GetComponent<TMP_InputField>().text;
            isGameSettingChanged = true;
        }
        if (!gameSettingsEntity.Persona.Equals(gameObjects[5].GetComponent<TMP_InputField>().text))
        {
            gameSettingsEntity.Persona = gameObjects[5].GetComponent<TMP_InputField>().text;
            isGameSettingChanged = true;
        }

        if(isGameSettingChanged){
            // 将 GameSettingsEntity 数据保存到 JSON 做持久化
            string js = JsonConvert.SerializeObject(gameSettingsEntity);
            string fileUrl = Path.Combine(Application.persistentDataPath, "GameSettings.json");
            Debug.Log($"保存设置到文件: {fileUrl}");
            Debug.Log($"新的 API Key 配置:");
            Debug.Log($"  ChatGPT API: {(string.IsNullOrEmpty(gameSettingsEntity.ChatGPTAPI) ? "未设置" : "已设置")}");
            Debug.Log($"  Azure API (Minimax): {(string.IsNullOrEmpty(gameSettingsEntity.AzureAPI) ? "未设置" : "已设置")}");
            Debug.Log($"  APISpace API: {(string.IsNullOrEmpty(gameSettingsEntity.APISpaceAPI) ? "未设置" : "已设置")}");
            
            using (StreamWriter sw = new StreamWriter(fileUrl))
            {
                sw.Write(js);
                sw.Close();
                sw.Dispose();
            }
            
            Debug.Log("触发 OnGameSettingChanged 事件...");
            OnGameSettingChanged?.Invoke();
            Debug.Log("设置已保存并触发更新事件");
            Debug.Log("事件已触发，各组件应该重新加载 API Key");
            
            // 立即检查配置
            CheckApiKeyConfiguration();
        }
    }

    public void InitGameSettings(){
        gameSettingsEntity = GameSettingsEntity.Instance;
        if (gameSettingsEntity.LookTargetMode == 0)
        {
            gameObjects[0].GetComponent<Toggle>().isOn = true;
        }
        else
        {
            gameObjects[0].GetComponent<Toggle>().isOn = false;
        }
        if(gameSettingsEntity.Speaker == "zh-CN-XiaoxiaoNeural"){
            gameObjects[1].GetComponent<TMP_Dropdown>().value = 0;
        }else if(gameSettingsEntity.Speaker == "zh-CN-XiaoyiNeural"){
            gameObjects[1].GetComponent<TMP_Dropdown>().value = 1;
        }
        gameObjects[2].GetComponent<TMP_InputField>().text = gameSettingsEntity.ChatGPTAPI;
        gameObjects[3].GetComponent<TMP_InputField>().text = gameSettingsEntity.AzureAPI;
        gameObjects[4].GetComponent<TMP_InputField>().text = gameSettingsEntity.APISpaceAPI;
        gameObjects[5].GetComponent<TMP_InputField>().text = gameSettingsEntity.Persona;
        
        // 调试输出当前 API Key 配置状态
        Debug.Log("=== 当前 API Key 配置 ===");
        Debug.Log($"ChatGPT API Key: {(string.IsNullOrEmpty(gameSettingsEntity.ChatGPTAPI) ? "未设置" : "已设置")}");
        Debug.Log($"Azure API Key (Minimax): {(string.IsNullOrEmpty(gameSettingsEntity.AzureAPI) ? "未设置" : "已设置")}");
        Debug.Log($"APISpace API Key: {(string.IsNullOrEmpty(gameSettingsEntity.APISpaceAPI) ? "未设置" : "已设置")}");
        Debug.Log("=========================");
    }

}
