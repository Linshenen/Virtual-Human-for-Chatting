using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class AzureSpeech : MonoBehaviour
{
   public AudioSource audioSource;

    // Minimax TTS API 配置
    private string API_Endpoint = "https://api.minimaxi.com/v1/t2a_v2";
    private string API_Key;
    private string Model = "speech-2.5-turbo-preview"; // 尝试使用最新预览版
    
    // 备用配置
    private string[] BackupModels = {
        "speech-2.5-turbo-preview",
        "speech-02-turbo",
        "speech-01-turbo"
    };
    private int currentModelIndex = 0;
    
    // 备用端点
    private string[] BackupEndpoints = {
        "https://api.minimax.io/v1/t2a_v2",
        "https://api.minimax.io/v1/t2a"
    };
    private int currentEndpointIndex = 0;

    private const int SampleRate = 24000;

    private object threadLocker = new object();
    private string message;

    // 主线程
    private SynchronizationContext mainThreadSynContext;
    private Animator animator;
    [HideInInspector]public AudioClip m_AudioClip;
    [SerializeField] private ExpressionController expressionController;

    // Audio2Face (暂时保留，但需要重新实现)
    private BlendShapeEntity blendShapeEntity = new BlendShapeEntity();
    [HideInInspector] public Queue<float[]> blendShapeQueue = new Queue<float[]>();


    // 事件
    [HideInInspector] public static event Action OnSpeechStart;
    [HideInInspector] public static event Action OnSpeechEnd;

    void Start(){
        // 注册 OnGameSettingChanged 事件
        GameSettingsEvent.OnGameSettingChanged += SetAzureSettings;

        m_AudioClip = null;

        // 记录主线程
        mainThreadSynContext = SynchronizationContext.Current;
        // 初始化动画控制器组件
        animator = GameObject.FindGameObjectWithTag("Model").GetComponent<Animator>();

        // 设置 API Key
        SetSpeechConfig();
        
        // 检查音频源
        CheckAudioSource();
        
    }
    
    private void CheckAudioSource()
    {
        if (audioSource == null)
        {
            Debug.LogError("AudioSource 未分配！请确保在 Inspector 中分配了 AudioSource 组件");
        }
        else
        {
            Debug.Log($"AudioSource 检查通过: volume={audioSource.volume}, enabled={audioSource.enabled}, mute={audioSource.mute}");
            
            // 确保 AudioSource 设置正确
            if (audioSource.volume <= 0)
            {
                Debug.LogWarning("AudioSource volume 为 0，设置为 1");
                audioSource.volume = 1.0f;
            }
            
            if (audioSource.mute)
            {
                Debug.LogWarning("AudioSource 被静音，取消静音");
                audioSource.mute = false;
            }
        }
    }
    
    public void SetAzureSettings(){
        SetSpeechConfig();
    }

    public void SetSpeechConfig(){
        API_Key = GameSettingsEntity.Instance.AzureAPI;
        
        if (string.IsNullOrEmpty(API_Key)) {
            Debug.LogWarning("AzureSpeech: Minimax API Key 未设置，语音合成将使用模拟模式");
        }
    }


    public void SsmlGeneration(TTSEntity _input)
    {
        // #if UNITY_EDITOR    // 调试时
        //     var ssmlFile = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf("/")) + "/Audios/" + _input.SsmlFile;
        // #else
            var ssmlFile = Application.persistentDataPath + "/" + _input.SsmlFile;
        // #endif
        
        if (System.IO.File.Exists(ssmlFile))
        {
            System.IO.File.Delete(ssmlFile);
        }

        try
        {
            // 构建用于 Minimax TTS 的文本内容
            StringBuilder textContent = new StringBuilder();
            for (int i = 0; i < _input.Content.Count; i++)
            {
                // 添加文本内容
                textContent.Append(_input.Content[i]);
                
                // 添加停顿（将 SSML break 转换为 Minimax 的停顿标记）
                if (!string.IsNullOrEmpty(_input.Break[i]))
                {
                    float pauseDuration = GetPauseDuration(_input.Break[i]);
                    if (pauseDuration > 0)
                    {
                        textContent.Append($"<#{pauseDuration:F2}#>");
                    }
                }
            }

            System.IO.File.WriteAllText(ssmlFile, textContent.ToString());
        }
        catch (IOException) { }
    }

    private float GetPauseDuration(string breakStrength)
    {
        // 将 SSML break strength 转换为秒数
        switch (breakStrength.ToLower())
        {
            case "x-weak": return 0.5f;
            case "weak": return 1.0f;
            case "medium": return 1.5f;
            case "strong": return 2.0f;
            case "x-strong": return 3.0f;
            default: return 1.0f;
        }
    }

    private string MapAzureVoiceToMinimax(string azureVoiceName)
    {
        // 将 Azure 语音名称映射到 Minimax 语音 ID
        // 这里需要根据实际使用的语音进行映射
        // 默认返回一个中文语音
        
        if (azureVoiceName.Contains("Xiaoxiao") || azureVoiceName.Contains("温柔"))
        {
            return "Chinese (Mandarin)_Soft_Girl"; // 温柔女声
        }
        else if (azureVoiceName.Contains("Xiaoyi") || azureVoiceName.Contains("可爱"))
        {
            return "Chinese (Mandarin)_BashfulGirl"; // 可爱女声
        }
        else
        {
            // 默认返回一个中文语音
            return "Chinese (Mandarin)_Soft_Girl"; // 温柔女声
        }
    }

    // 合成语音并播放
    public void TurnTextToSpeech(TTSEntity _input)
    {
        if(audioSource.isPlaying){
            audioSource.Stop();
        }

        lock (threadLocker)
        {
            // 可以在这里添加其他需要线程安全的逻辑
        }

        // 生成ssml文件并读取
        SsmlGeneration(_input);
        // #if UNITY_EDITOR    // 调试时
        //     var ssmlFile = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf("/")) + "/Audios/" + _input.SsmlFile;
        // #else
            var ssmlFile = Application.persistentDataPath + "/" + _input.SsmlFile;
        // #endif
        string ssml = System.IO.File.ReadAllText(ssmlFile, Encoding.UTF8);

        // 控制表情
        expressionController.setCurrentExpression(_input.Style[0]);

        // 生成语音
        TurnTextToSpeechFromSSML(ssml);
    }

    // 从文本合成语音并播放
    public void TurnTextToSpeechFromSSML(string text)
    {
        StartCoroutine(TurnTextToSpeechCoroutine(text));
    }

    private IEnumerator TurnTextToSpeechCoroutine(string text)
    {
        string newMessage = null;
        var startTime = DateTime.Now;

        // 调试：检查当前的 API Key
        Debug.Log($"AzureSpeech: 当前 API Key = {(string.IsNullOrEmpty(API_Key) ? "未设置" : API_Key.Substring(0, System.Math.Min(10, API_Key.Length)) + "...")}");
        Debug.Log($"GameSettingsEntity.AzureAPI = {(string.IsNullOrEmpty(GameSettingsEntity.Instance.AzureAPI) ? "未设置" : GameSettingsEntity.Instance.AzureAPI.Substring(0, System.Math.Min(10, GameSettingsEntity.Instance.AzureAPI.Length)) + "...")}");

        // 检查 API Key 是否配置
        if (string.IsNullOrEmpty(API_Key))
        {
            Debug.LogError("Minimax API Key 未配置！请在 GameSettings.json 文件中配置 AzureAPI 字段。");
            Debug.LogError("请访问 https://platform.minimax.io/ 获取 API Key");
            
            // 使用默认的文本转语音（模拟）
            yield return StartCoroutine(SimulateTextToSpeech(text));
            yield break;
        }
        
        // 验证 API Key 格式
        if (!ValidateApiKey(API_Key))
        {
            Debug.LogError("API Key 格式验证失败，使用模拟模式");
            yield return StartCoroutine(SimulateTextToSpeech(text));
            yield break;
        }

        // 构建 Minimax TTS 请求
        var requestData = new MinimaxTTSRequest
        {
            model = Model,
            text = text,
            stream = false,
            language_boost = "Chinese",
            output_format = "hex",
            voice_setting = new VoiceSetting
            {
                voice_id = MapAzureVoiceToMinimax(GameSettingsEntity.Instance.Speaker),
                speed = 1.0f,
                vol = 1.0f,
                pitch = 0
            },
            audio_setting = new AudioSetting
            {
                sample_rate = 24000,
                bitrate = 128000,
                format = "mp3",
                channel = 1
            }
        };

        string jsonPayload = JsonConvert.SerializeObject(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        using (UnityWebRequest www = new UnityWebRequest(API_Endpoint, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", $"Bearer {API_Key}");

            Debug.Log($"发送请求到: {API_Endpoint}");
            Debug.Log($"请求头 Authorization: Bearer {API_Key.Substring(0, Math.Min(10, API_Key.Length))}...");
            Debug.Log($"请求体: {jsonPayload}");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"收到响应: {www.downloadHandler.text}");
                var response = JsonConvert.DeserializeObject<MinimaxTTSResponse>(www.downloadHandler.text);
                
                if (response.base_resp.status_code == 0 && response.data != null && !string.IsNullOrEmpty(response.data.audio))
                {
                    Debug.Log($"音频数据长度: {response.data.audio.Length}");
                    Debug.Log($"音频状态: {response.data.status}");
                    
                    // 将十六进制音频数据转换为字节数组
                    byte[] audioBytes = HexStringToByteArray(response.data.audio);
                    Debug.Log($"转换后的音频字节数: {audioBytes.Length}");
                    
                    // 创建 AudioClip
                    bool audioLoaded = false;
                    StartCoroutine(CreateAudioClipFromMP3Coroutine(audioBytes, (audioClip) => {
                        if (audioClip != null)
                        {
                            Debug.Log($"音频剪辑创建成功: {audioClip.length} 秒");
                            m_AudioClip = audioClip;
                            var endTime = DateTime.Now;
                            var latency = endTime.Subtract(startTime).TotalMilliseconds;
                            newMessage = $"Speech synthesis succeeded!\nLatency: {latency} ms.";
                            
                            // 开始播放
                            Debug.Log("开始播放音频...");
                            StartCoroutine(OnPlayStartIE());
                        }
                        else
                        {
                            Debug.LogError("Failed to create audio clip from MP3 data");
                        }
                        audioLoaded = true;
                    }));
                    
                    // 等待音频加载完成
                    while (!audioLoaded)
                    {
                        yield return null;
                    }
                }
                else if (response.base_resp.status_code == 2049) // Invalid API Key
                {
                    Debug.LogError($"API Key 无效 (错误码 2049): {response.base_resp.status_msg}");
                    Debug.LogError("请确认：");
                    Debug.LogError("1. API Key 是从 https://platform.minimax.io/ 获取的");
                    Debug.LogError("2. API Key 格式正确（JWT 格式，以 eyJ 开头）");
                    Debug.LogError("3. API Key 有 TTS 权限");
                    Debug.LogError("4. 账户状态正常");
                    
                    // 尝试使用下一个模型
                    if (currentModelIndex < BackupModels.Length - 1)
                    {
                        currentModelIndex++;
                        Model = BackupModels[currentModelIndex];
                        Debug.Log($"尝试使用备用模型: {Model}");
                        
                        // 重新发送请求
                        yield return StartCoroutine(TurnTextToSpeechCoroutine(text));
                    }
                    else if (currentEndpointIndex < BackupEndpoints.Length - 1)
                    {
                        // 尝试使用备用端点
                        currentEndpointIndex++;
                        API_Endpoint = BackupEndpoints[currentEndpointIndex];
                        currentModelIndex = 0; // 重置模型索引
                        Model = BackupModels[currentModelIndex];
                        Debug.Log($"尝试使用备用端点: {API_Endpoint}");
                        
                        // 重新发送请求
                        yield return StartCoroutine(TurnTextToSpeechCoroutine(text));
                    }
                    else
                    {
                        Debug.LogError("所有模型和端点都尝试失败，使用模拟语音");
                        // 使用模拟语音
                        yield return StartCoroutine(SimulateTextToSpeech(text));
                    }
                }
                else
                {
                    Debug.LogError($"TTS API Error: Status={response.base_resp.status_code}, Message={response.base_resp.status_msg}");
                    Debug.LogError($"完整响应: {www.downloadHandler.text}");
                    
                    // 对于其他错误，也使用模拟语音
                    yield return StartCoroutine(SimulateTextToSpeech(text));
                }
            }
            else
            {
                Debug.LogError($"HTTP Error: {www.error}");
                Debug.LogError($"HTTP Status: {www.responseCode}");
                
                // HTTP 错误也使用模拟语音
                yield return StartCoroutine(SimulateTextToSpeech(text));
            }
        }

        lock (threadLocker)
        {
            if (newMessage != null)
            {
                message = newMessage;
            }

            // 可以在这里添加其他需要线程安全的逻辑
        }
    }

    // 模拟文本转语音（当 API Key 未配置时使用）
    private IEnumerator SimulateTextToSpeech(string text)
    {
        Debug.LogWarning("使用模拟语音合成，请配置 Minimax API Key 以获得真实语音效果");
        Debug.LogWarning("当前配置的 API Key 可能无效，请检查设置");
        
        // 创建简单的音频数据（静音）
        int sampleCount = SampleRate * 2; // 2秒静音
        float[] samples = new float[sampleCount];
        
        AudioClip audioClip = AudioClip.Create("SimulatedSpeech", sampleCount, 1, SampleRate, false);
        audioClip.SetData(samples, 0);
        
        m_AudioClip = audioClip;
        
        // 开始播放
        StartCoroutine(OnPlayStartIE());
        
        yield return new WaitForSeconds(2.0f); // 模拟2秒语音
        
        // 结束播放
        OnPlayEnd(null);
    }

    // 测试 API Key 是否有效
    [ContextMenu("测试 Minimax API Key")]
    public void TestMinimaxApiKey()
    {
        string currentKey = GameSettingsEntity.Instance.AzureAPI;
        Debug.Log($"当前 Minimax API Key: {(string.IsNullOrEmpty(currentKey) ? "未设置" : currentKey.Substring(0, System.Math.Min(10, currentKey.Length)) + "...")}");
        
        if (string.IsNullOrEmpty(currentKey))
        {
            Debug.LogError("Minimax API Key 未设置！");
            Debug.LogError("请在设置界面中配置 Azure API Key");
            return;
        }
        
        // 检查 API Key 格式
        if (currentKey.Length < 10)
        {
            Debug.LogError("API Key 格式可能不正确，长度太短");
            return;
        }
        
        // 验证 JWT 格式
        if (!currentKey.StartsWith("eyJ"))
        {
            Debug.LogError("API Key 格式不正确，应该为 JWT 格式（以 eyJ 开头）");
            Debug.LogError("请确认 API Key 是从 https://platform.minimax.io/ 获取的");
            return;
        }
        
        // 创建测试文本
        TTSEntity testEntity = new TTSEntity();
        testEntity.Content = new System.Collections.Generic.List<string> { "API Key 测试" };
        testEntity.Style = new System.Collections.Generic.List<string> { "cheerful" };
        testEntity.StyleDegree = new System.Collections.Generic.List<double> { 1.0 };
        testEntity.ProsodyRate = new System.Collections.Generic.List<double> { 1.0 };
        testEntity.ProsodyVolume = new System.Collections.Generic.List<double> { 1.0 };
        testEntity.Break = new System.Collections.Generic.List<string> { "medium" };
        testEntity.SsmlFile = "test_api.ssml";
        
        Debug.Log("开始测试 Minimax API Key...");
        Debug.Log($"使用的模型: {Model}");
        Debug.Log($"使用的语音: {MapAzureVoiceToMinimax(GameSettingsEntity.Instance.Speaker)}");
        Debug.Log("注意：如果 API Key 无效，系统会自动切换到模拟语音模式");
        TurnTextToSpeech(testEntity);
    }

    // 测试音频播放功能
    [ContextMenu("测试音频播放")]
    public void TestAudioPlayback()
    {
        Debug.Log("=== 测试音频播放功能 ===");
        
        if (audioSource == null)
        {
            Debug.LogError("AudioSource 组件未找到！");
            return;
        }
        
        Debug.Log($"AudioSource 状态: enabled={audioSource.enabled}, volume={audioSource.volume}");
        
        // 创建简单的测试音频（正弦波）
        int sampleRate = 44100;
        float frequency = 440f; // A4 音符
        float duration = 1f; // 1秒
        
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        
        for (int i = 0; i < sampleCount; i++)
        {
            float time = (float)i / sampleRate;
            samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * time) * 0.5f;
        }
        
        AudioClip testClip = AudioClip.Create("TestAudio", sampleCount, 1, sampleRate, false);
        testClip.SetData(samples, 0);
        
        m_AudioClip = testClip;
        
        Debug.Log($"测试音频创建成功: {testClip.length} 秒");
        
        // 播放测试音频
        StartCoroutine(OnPlayStartIE());
        
        Debug.Log("测试音频播放已开始");
    }

    // 验证 API Key 格式
    private bool ValidateApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("API Key 为空");
            return false;
        }
        
        if (apiKey.Length < 10)
        {
            Debug.LogError("API Key 长度太短，可能格式不正确");
            return false;
        }
        
        // 检查是否包含特殊字符或空格
        if (apiKey.Contains(" ") || apiKey.Contains("\t") || apiKey.Contains("\n"))
        {
            Debug.LogError("API Key 包含空格或特殊字符");
            return false;
        }
        
        Debug.Log("API Key 格式验证通过");
        return true;
    }

    private byte[] HexStringToByteArray(string hex)
    {
        int length = hex.Length;
        byte[] bytes = new byte[length / 2];
        for (int i = 0; i < length; i += 2)
        {
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }
        return bytes;
    }

    private IEnumerator CreateAudioClipFromMP3Coroutine(byte[] mp3Data, System.Action<AudioClip> onComplete)
    {
        Debug.Log($"CreateAudioClipFromMP3Coroutine: 收到 {mp3Data.Length} 字节音频数据");
        
        // 由于 Unity 不支持直接加载 MP3，这里提供一个简化的实现
        // 在实际项目中，您可能需要使用第三方库（如 NAudio、FFmpeg）来处理 MP3 解码
        
        // 临时解决方案：将 MP3 数据保存到临时文件，然后使用 UnityWebRequest 加载
        string tempFilePath = Path.Combine(Application.persistentDataPath, "temp_audio.mp3");
        
        // 保存 MP3 数据到临时文件
        try
        {
            File.WriteAllBytes(tempFilePath, mp3Data);
            Debug.Log($"音频数据已保存到临时文件: {tempFilePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"保存音频数据到临时文件时出错: {ex.Message}");
            onComplete?.Invoke(null);
            yield break;
        }
        
        // 验证文件是否成功创建
        if (!File.Exists(tempFilePath))
        {
            Debug.LogError("临时音频文件创建失败");
            onComplete?.Invoke(null);
            yield break;
        }
        
        long fileSize = new FileInfo(tempFilePath).Length;
        Debug.Log($"临时音频文件大小: {fileSize} 字节");
        
        // 使用 UnityWebRequest 加载音频文件
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip($"file://{tempFilePath}", AudioType.MPEG))
        {
            Debug.Log("开始加载音频文件...");
            yield return www.SendWebRequest();
            
            try
            {
                if (www.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("音频文件加载成功");
                    AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
                    
                    if (audioClip != null)
                    {
                        Debug.Log($"音频剪辑创建成功: {audioClip.length} 秒, {audioClip.samples} 采样点, {audioClip.frequency} Hz");
                        
                        // 验证音频剪辑
                        if (audioClip.length > 0 && audioClip.samples > 0)
                        {
                            Debug.Log("音频剪辑验证通过");
                            onComplete?.Invoke(audioClip);
                        }
                        else
                        {
                            Debug.LogError("音频剪辑无效：长度或采样点数为0");
                            onComplete?.Invoke(null);
                        }
                    }
                    else
                    {
                        Debug.LogError("DownloadHandlerAudioClip.GetContent 返回 null");
                        onComplete?.Invoke(null);
                    }
                }
                else
                {
                    Debug.LogError($"加载音频文件失败: {www.error}");
                    Debug.LogError($"HTTP 状态码: {www.responseCode}");
                    onComplete?.Invoke(null);
                }
            }
            finally
            {
                // 清理临时文件
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                    Debug.Log("临时音频文件已删除");
                }
            }
        }
    }

    IEnumerator OnPlayStartIE()
    {
        // yield return new WaitForSeconds(0.1f);
        yield return null;
        Debug.Log("OnPlayStartIE: 开始播放音频");
        
        if (audioSource == null)
        {
            Debug.LogError("AudioSource 为 null，无法播放音频");
            yield break;
        }
        
        if (m_AudioClip == null)
        {
            Debug.LogError("音频剪辑为 null，无法播放音频");
            yield break;
        }
        
        Debug.Log($"音频信息: 长度={m_AudioClip.length}秒, 采样率={m_AudioClip.frequency}Hz, 声道数={m_AudioClip.channels}");
        
        OnSpeechStart?.Invoke();  // 调用通知事件
        Audio2Face.f_IsAudioPlaying = true;
        animator.SetBool("isSpeaking", true);
        animator.SetInteger("idleState", -1);
        audioSource.clip = m_AudioClip;
        
        Debug.Log($"设置音频剪辑到 AudioSource: {audioSource.clip.name}");
        
        audioSource.Play();
        
        Debug.Log("音频播放已开始");
        Debug.Log($"AudioSource 状态: isPlaying={audioSource.isPlaying}, time={audioSource.time}");
        
        // 检查播放是否成功
        if (!audioSource.isPlaying)
        {
            Debug.LogError("音频播放失败！AudioSource.isPlaying 为 false");
        }
        else
        {
            Debug.Log("音频播放成功启动");
        }
        
        // 监控音频播放状态
        StartCoroutine(MonitorAudioPlayback());
        
        // 延迟检查播放状态
        yield return new WaitForSeconds(0.1f);
        
        if (audioSource.isPlaying)
        {
            Debug.Log("确认：音频正在播放中");
        }
        else
        {
            Debug.LogError("确认：音频播放已停止");
        }
    }
    
    private IEnumerator MonitorAudioPlayback()
    {
        while (audioSource.isPlaying)
        {
            Debug.Log($"音频播放中: {audioSource.time:F2} / {audioSource.clip.length:F2} 秒");
            yield return new WaitForSeconds(0.5f);
        }
        
        Debug.Log("音频播放结束");
    }
    private void OnPlayEnd(object state){
        StartCoroutine(OnPlayEndIE());
    }
    IEnumerator OnPlayEndIE()
    {
        // yield return new WaitForSeconds(0.1f);
        yield return null;
        OnSpeechEnd?.Invoke();  // 调用通知事件
        Audio2Face.f_IsAudioPlaying = false;
        Debug.Log("Speech end!");
        blendShapeQueue.Clear();
        animator.SetBool("isSpeaking", false);
        animator.SetInteger("idleState", 0);
        expressionController.RestoreDefaultExpression();
    }

    // 语音识别功能暂时不可用，需要重新实现
    [HideInInspector] public bool m_KeyWordResult = false;
    public void KeyWordRecognition(){
        Debug.LogWarning("关键词识别功能暂时不可用，需要重新实现");
    }

    [HideInInspector] public string m_STTResult = "";
    public void TurnSpeechToText()
    {
        Debug.LogWarning("语音转文本功能暂时不可用，需要重新实现");
    }
}

// Minimax TTS 请求和响应类
[System.Serializable]
public class MinimaxTTSRequest
{
    public string model;
    public string text;
    public bool stream;
    public string language_boost;
    public string output_format;
    public VoiceSetting voice_setting;
    public AudioSetting audio_setting;
}

[System.Serializable]
public class VoiceSetting
{
    public string voice_id;
    public float speed;
    public float vol;
    public int pitch;
}

[System.Serializable]
public class AudioSetting
{
    public int sample_rate;
    public int bitrate;
    public string format;
    public int channel;
}

[System.Serializable]
public class MinimaxTTSResponse
{
    public TTSData data;
    public TTSBaseResp base_resp;
    public TTSExtraInfo extra_info;
    public string trace_id;
}

[System.Serializable]
public class TTSData
{
    public string audio;
    public int status;
}

[System.Serializable]
public class TTSBaseResp
{
    public int status_code;
    public string status_msg;
}

[System.Serializable]
public class TTSExtraInfo
{
    public long audio_length;
    public long audio_sample_rate;
    public long audio_size;
    public long bitrate;
    public string audio_format;
    public long audio_channel;
    public float invisible_character_ratio;
    public long usage_characters;
    public long word_count;
}