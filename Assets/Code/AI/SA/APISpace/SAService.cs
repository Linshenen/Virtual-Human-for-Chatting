using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

public class SAService
{
    private string API_Key;
    private string API_Endpoint = "https://ark.cn-beijing.volces.com/api/v3/chat/completions";
    private string Model = "ep-20251029155135-mnw5s";
    
    public SAService()
    {
        API_Key = GameSettingsEntity.Instance.APISpaceAPI;
        // 注册 OnGameSettingChanged 事件
        GameSettingsEvent.OnGameSettingChanged += SetSASettings; 
    }

    public void SetSASettings(){
        API_Key = GameSettingsEntity.Instance.APISpaceAPI;
        
        if (string.IsNullOrEmpty(API_Key)) {
            Debug.LogWarning("SAService: LLM API Key 未设置，情感分析将使用默认结果");
        }
    }
    

    public SAEntity sentimentAnalysis(string msg)
    {
        try
        {
            string content = msg.Replace("\t", "").Replace("\r", "");
            
            // 构建 OpenAI 兼容的请求
            string systemPrompt = "你是一个情感分析专家。请分析以下文本的情感，并以纯JSON格式返回结果（不要使用markdown代码块），包含以下字段：positive_prob（积极概率0-1）、negative_prob（消极概率0-1）、sentiments（情感极性概率0-1）、sentences（情感分类数组：0负向，1中性，2正向）、style（情感风格：cheerful、sad、angry、fearful、disgruntled、serious、customerservice）。请确保返回的是有效的JSON对象，不要包含任何其他文本或markdown格式。";
            
            // 使用简单的字符串拼接构建 JSON
            string jsonPayload = "{" +
                "\"model\":\"" + Model + "\"," +
                "\"messages\":[" +
                "{\"role\":\"system\",\"content\":\"" + EscapeJsonString(systemPrompt) + "\"}," +
                "{\"role\":\"user\",\"content\":\"" + EscapeJsonString(content) + "\"}" +
                "]," +
                "\"temperature\":0.1," +
                "\"max_tokens\":200" +
            "}";

            byte[] data = Encoding.UTF8.GetBytes(jsonPayload);
            
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(API_Endpoint);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = data.Length;
            request.Headers.Add("Authorization", "Bearer " + API_Key);
            
            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }
            
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Stream myResponseStream = response.GetResponseStream();
            StreamReader myStreamReader = new StreamReader(myResponseStream, Encoding.UTF8);
            string retString = myStreamReader.ReadToEnd();
            myStreamReader.Close();
            myResponseStream.Close();

            // 解析 LLM 返回的 JSON 响应
            return ParseLLMResponse(retString);
        }
        catch (Exception ex)
        {
            Debug.LogError($"情感分析API调用失败: {ex.Message}");
            // 返回默认值
            return new SAEntity
            {
                Positive_prob = 0.5,
                Negative_prob = 0.5,
                Sentiments = 0.5,
                Sentences = 1,
                Style = "customerservice"
            };
        }
    }
    
    private string EscapeJsonString(string input)
    {
        return input.Replace("\\", "\\\\")
                   .Replace("\"", "\\\"")
                   .Replace("\n", "\\n")
                   .Replace("\r", "\\r")
                   .Replace("\t", "\\t");
    }
    
    private SAEntity ParseLLMResponse(string response)
    {
        try
        {
            Debug.Log($"LLM Response: {response}");
            
            // 尝试解析 JSON 响应
            var responseObj = JsonConvert.DeserializeObject<dynamic>(response);
            
            // 获取 content
            string content = responseObj.choices[0].message.content.ToString();
            Debug.Log($"Extracted content: {content}");
            
            // 清理 content 中的转义字符
            content = content.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\r", "\r").Replace("\\t", "\t");
            
            // 提取 JSON 内容（处理 markdown 代码块）
            string jsonContent = ExtractJsonFromContent(content);
            
            // 尝试解析 content 中的 JSON
            var sentimentData = JsonConvert.DeserializeObject<dynamic>(jsonContent);
            
            SAEntity sAEntity = new SAEntity();
            sAEntity.Positive_prob = (double)sentimentData.positive_prob;
            sAEntity.Negative_prob = (double)sentimentData.negative_prob;
            sAEntity.Sentiments = (double)sentimentData.sentiments;
            
            // 处理 sentences 字段，如果是数组则取平均值，如果是单个值则直接使用
            if (sentimentData.sentences is Newtonsoft.Json.Linq.JArray)
            {
                var sentencesArray = sentimentData.sentences.ToObject<int[]>();
                if (sentencesArray.Length > 0)
                {
                    int sum = 0;
                    foreach (int value in sentencesArray)
                    {
                        sum += value;
                    }
                    sAEntity.Sentences = (int)Math.Round((double)sum / sentencesArray.Length);
                }
                else
                {
                    sAEntity.Sentences = 1;
                }
            }
            else
            {
                sAEntity.Sentences = (int)sentimentData.sentences;
            }
            
            sAEntity.Style = sentimentData.style.ToString();
            
            return sAEntity;
        }
        catch (Exception ex)
        {
            Debug.LogError($"解析LLM响应失败: {ex.Message}");
            Debug.LogError($"原始响应: {response}");
            // 返回默认值
            return new SAEntity
            {
                Positive_prob = 0.5,
                Negative_prob = 0.5,
                Sentiments = 0.5,
                Sentences = 1,
                Style = "customerservice"
            };
        }
    }
    
    private string ExtractJsonFromContent(string content)
    {
        // 检查是否包含 markdown 代码块
        if (content.Contains("```json"))
        {
            // 提取 ```json 和 ``` 之间的内容
            int startIndex = content.IndexOf("```json") + 7;
            int endIndex = content.LastIndexOf("```");
            
            if (startIndex >= 0 && endIndex > startIndex)
            {
                return content.Substring(startIndex, endIndex - startIndex).Trim();
            }
        }
        else if (content.Contains("```"))
        {
            // 提取 ``` 和 ``` 之间的内容
            int startIndex = content.IndexOf("```") + 3;
            int endIndex = content.LastIndexOf("```");
            
            if (startIndex >= 0 && endIndex > startIndex)
            {
                return content.Substring(startIndex, endIndex - startIndex).Trim();
            }
        }
        
        // 如果没有代码块，直接返回原内容
        return content;
    }
}