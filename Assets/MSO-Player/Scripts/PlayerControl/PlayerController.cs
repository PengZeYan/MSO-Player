using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using yan.libvlc;
using yan.libvlc.Core;

/// <summary>
/// 视频播放器控制器，为UI提供完整的视频播放控制接口
/// </summary>
public class PlayerController : MonoBehaviour
{
    #region 序列化字段

    [Header("媒体播放器组件")]
    [SerializeField, Tooltip("媒体播放器组件")]
    private MediaPlayer mediaPlayer;

    [Header("播放控制")]
    [SerializeField, Tooltip("时间显示文本")]
    private Text timeDisplayText;

    [SerializeField, Tooltip("分辨率显示文本")]
    private Text resolutionText;

    [SerializeField, Tooltip("进度条")]
    private Slider progressSlider;

    [SerializeField, Tooltip("音量滑块")]
    private Slider volumeSlider;

    [SerializeField, Tooltip("播放/暂停按钮")]
    private Button playPauseButton;

    [SerializeField, Tooltip("停止按钮")]
    private Button stopButton;

    [SerializeField, Tooltip("播放图标")]
    private Sprite playSprite;

    [SerializeField, Tooltip("暂停图标")]
    private Sprite pauseSprite;

    [SerializeField, Tooltip("启用循环播放")]
    private bool enableLooping = false;

    [SerializeField, Tooltip("缓冲指示器")]
    private GameObject bufferingIndicator;

    [SerializeField, Tooltip("直播指示器")]
    private GameObject liveIndicator;

    [SerializeField]
    private string playUrl;


    #endregion

    #region 私有字段

    private bool isDraggingSlider = false;
    private bool isLiveStream = false;
    private bool isLoopEnabled = false;
    private string currentMediaUrl = string.Empty;
    private Coroutine progressUpdateCoroutine;
    private ExtendedVlcPlayer extendedPlayer;
    private float previousProgress = 0f; // 记录前一帧的进度值
    private float animationSpeed = 4f; // 进度条动画速度系数

    #endregion

    #region Unity生命周期方法

    private void Start()
    {
        if (mediaPlayer == null)
        {
            Debug.LogError("媒体播放器组件未指定");
            return;
        }

        // 初始化循环播放状态
        isLoopEnabled = enableLooping;

        // 初始化UI组件
        InitializeUIComponents();

        // 添加事件监听
        mediaPlayer.OnMediaPlayerStateEvent += OnMediaPlayerStateChanged;
        mediaPlayer.OnMediaPlayerErrorEvent += OnMediaPlayerError;

        // 设置初始音量
        if (volumeSlider != null)
        {
            volumeSlider.value = 0.5f; // 默认音量50%
            SetVolume(0.5f);
        }

        SetMediaUrl(playUrl);
    }

    private void OnDestroy()
    {
        if (mediaPlayer != null)
        {
            mediaPlayer.OnMediaPlayerStateEvent -= OnMediaPlayerStateChanged;
            mediaPlayer.OnMediaPlayerErrorEvent -= OnMediaPlayerError;
        }

        if (progressUpdateCoroutine != null)
        {
            StopCoroutine(progressUpdateCoroutine);
        }
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 设置媒体URL并自动播放
    /// </summary>
    /// <param name="url">媒体URL</param>
    /// <param name="autoPlay">是否自动播放</param>
    
    public void SetMediaUrl(string url, bool autoPlay = true)
    {
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogError("媒体URL不能为空");
            return;
        }

        // 保存当前URL以便循环播放
        currentMediaUrl = url;
        
        // 重置UI状态
        ResetUIState();

        // 初步检测是否为直播流（基于URL协议和后缀的初步判断）
        isLiveStream = IsLikelyLiveStream(url);
        
        // 更新直播相关UI
        UpdateLiveStreamUI(isLiveStream);

        // 设置URL并播放
        mediaPlayer.SetUrl(url, autoPlay);
        
        // 在播放器组件中获取VLC播放器实例
        InitializeExtendedPlayer();
    }
    
    /// <summary>
    /// 无感切换媒体URL
    /// </summary>
    /// <param name="url">新的媒体URL</param>
    public void SmoothSwitchUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogError("媒体URL不能为空");
            return;
        }
        
        // 显示缓冲指示器
        if (bufferingIndicator != null)
        {
            bufferingIndicator.SetActive(true);
        }
        
        // 保存当前URL以便循环播放
        currentMediaUrl = url;
        
        // 初步检测是否为直播流（基于URL协议和后缀的初步判断）
        bool newUrlIsLiveStream = IsLikelyLiveStream(url);
        
        // 如果不存在扩展播放器则初始化
        if (extendedPlayer == null)
        {
            InitializeExtendedPlayer();
            if (extendedPlayer == null)
            {
                // 如果无法初始化扩展播放器，使用常规方法切换
                SetMediaUrl(url, true);
                return;
            }
        }
        
        // 获取扩展播放器使用的内部VLC播放器实例
        System.Reflection.FieldInfo fieldInfo = typeof(MediaPlayer).GetField("m_Player", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
        VlcMediaPlayer vlcPlayer = fieldInfo?.GetValue(mediaPlayer) as VlcMediaPlayer;
        
        if (vlcPlayer != null)
        {
            // 使用平滑切换方法
            vlcPlayer.UpdateUrlSmooth(url, () => {
                // 切换完成后更新UI
                isLiveStream = newUrlIsLiveStream;
                UpdateLiveStreamUI(isLiveStream);
                
                if (bufferingIndicator != null)
                {
                    bufferingIndicator.SetActive(false);
                }
                
                // 启动分辨率和直播状态检查
                StartCoroutine(DelayedResolutionCheck());
                StartCoroutine(DelayedLiveStreamCheck());
                
                // 更新播放/暂停按钮状态
                UpdatePlayPauseButton();
            });
        }
        else
        {
            // 回退到常规方法
            SetMediaUrl(url, true);
        }
    }

    /// <summary>
    /// 播放/暂停切换
    /// </summary>
    public void TogglePlayPause()
    {
        if (mediaPlayer == null) return;

        mediaPlayer.Pause();
        UpdatePlayPauseButton();
    }

    /// <summary>
    /// 停止播放
    /// </summary>
    public void Stop()
    {
        if (mediaPlayer == null) return;
        mediaPlayer.Stop();
        UpdatePlayPauseButton();
        progressSlider.value = 0;
    }

    /// <summary>
    /// 设置音量（0-1）
    /// </summary>
    public void SetVolume(float volume)
    {
        if (extendedPlayer == null) 
        {
            InitializeExtendedPlayer();
            if (extendedPlayer == null) return;
        }

        // 将0-1的值转换为0-100的范围
        int volumeValue = Mathf.Clamp(Mathf.RoundToInt(volume * 100), 0, 100);
        extendedPlayer.SetVolume(volumeValue);
    }

    /// <summary>
    /// 进度条开始拖动
    /// </summary>
    public void OnProgressSliderBeginDrag()
    {
        isDraggingSlider = true;
    }

    /// <summary>
    /// 进度条结束拖动
    /// </summary>
    public void OnProgressSliderEndDrag()
    {
        SeekToPosition(progressSlider.value);
        isDraggingSlider = false;
    }

    /// <summary>
    /// 进度条值变化
    /// </summary>
    public void OnProgressSliderValueChanged()
    {
        if (!isDraggingSlider)
        {
            return;
        }

        // 在拖动过程中实时更新播放位置
        if (progressSlider != null && !isLiveStream)
        {
            // 获取媒体时长
            long duration = GetMediaDuration();
            if (duration > 0)
            {
                // 计算当前时间
                long currentTime = (long)(progressSlider.value * duration);
                // 更新时间显示
                UpdateTimeDisplay(currentTime, duration);
            }
        }
    }

    /// <summary>
    /// 跳转到指定位置
    /// </summary>
    /// <param name="position">位置（0-1）</param>
    public void SeekToPosition(float position)
    {
        if (extendedPlayer == null || isLiveStream) return;

        if (extendedPlayer.IsSeekable())
        {
            // 立即更新前一帧进度值，防止跳转后被误判为回退
            previousProgress = position;
            extendedPlayer.SetPosition(position);
        }
    }

    /// <summary>
    /// 切换静音状态
    /// </summary>
    public void ToggleMute()
    {
        if (extendedPlayer == null) 
        {
            InitializeExtendedPlayer();
            if (extendedPlayer == null) return;
        }

        bool isMuted = extendedPlayer.IsMuted();
        extendedPlayer.SetMute(!isMuted);
        
        // 更新音量滑块
        if (volumeSlider != null)
        {
            volumeSlider.value = isMuted ? extendedPlayer.GetVolume() / 100f : 0f;
        }
    }

    /// <summary>
    /// 设置循环播放状态
    /// </summary>
    /// <param name="loopEnabled">是否启用循环播放</param>
    public void SetLoopEnabled(bool loopEnabled)
    {
        isLoopEnabled = loopEnabled;
        enableLooping = isLoopEnabled; // 同步到序列化字段
    }

   

    /// <summary>
    /// 获取当前播放状态
    /// </summary>
    public bool IsPlaying
    {
        get { return mediaPlayer != null && mediaPlayer.IsPlaying; }
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 初始化UI组件
    /// </summary>
    private void InitializeUIComponents()
    {
        // 添加按钮事件监听
        if (playPauseButton != null)
        {
            playPauseButton.onClick.AddListener(TogglePlayPause);
        }

        if (stopButton != null)
        {
            stopButton.onClick.AddListener(Stop);
        }

        // 添加滑块事件监听
        if (progressSlider != null)
        {
            progressSlider.onValueChanged.AddListener(_ => OnProgressSliderValueChanged());
            
            // 添加拖动事件监听
            var sliderEvents = progressSlider.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (sliderEvents == null)
            {
                sliderEvents = progressSlider.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            }
            
            // 添加拖动开始事件
            var beginDragEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            beginDragEntry.eventID = UnityEngine.EventSystems.EventTriggerType.BeginDrag;
            beginDragEntry.callback.AddListener((_) => OnProgressSliderBeginDrag());
            sliderEvents.triggers.Add(beginDragEntry);
            
            // 添加拖动结束事件
            var endDragEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            endDragEntry.eventID = UnityEngine.EventSystems.EventTriggerType.EndDrag;
            endDragEntry.callback.AddListener((_) => OnProgressSliderEndDrag());
            sliderEvents.triggers.Add(endDragEntry);
            
            // 添加点击事件
            var pointerDownEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerDownEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
            pointerDownEntry.callback.AddListener(OnProgressSliderClick);
            sliderEvents.triggers.Add(pointerDownEntry);
        }

        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }

        // 初始化UI状态
        ResetUIState();

        // 启动进度更新协程
        progressUpdateCoroutine = StartCoroutine(UpdateProgressCoroutine());
    }

    /// <summary>
    /// 重置UI状态
    /// </summary>
    private void ResetUIState()
    {
        if (progressSlider != null)
        {
            progressSlider.value = 0;
            progressSlider.interactable = true;
            progressSlider.gameObject.SetActive(true); // 默认显示进度条
        }

        if (timeDisplayText != null)
        {
            timeDisplayText.text = "00:00 / 00:00";
            timeDisplayText.gameObject.SetActive(true); // 默认显示时间
        }

        if (resolutionText != null)
        {
            resolutionText.text = "Loading...";
        }

        if (bufferingIndicator != null)
        {
            bufferingIndicator.SetActive(false);
        }

        if (liveIndicator != null)
        {
            liveIndicator.SetActive(false);
        }

        UpdatePlayPauseButton();
    }

    /// <summary>
    /// 更新播放/暂停按钮状态
    /// </summary>
    private void UpdatePlayPauseButton()
    {
        bool isPlaying = mediaPlayer != null && mediaPlayer.IsPlaying;

        if (playPauseButton != null && playPauseButton.image != null)
        {
            // 根据播放状态设置不同的图标
            playPauseButton.image.sprite = isPlaying ? pauseSprite : playSprite;
        }
    }

    /// <summary>
    /// 更新时间显示
    /// </summary>
    private void UpdateTimeDisplay(long currentTime, long duration)
    {
        if (timeDisplayText != null)
        {
            string currentTimeStr = FormatTime(currentTime);
            
            if (duration > 0)
            {
                string durationStr = FormatTime(duration);
                timeDisplayText.text = $"{currentTimeStr} / {durationStr}";
            }
            else if (isLiveStream)
            {
                timeDisplayText.text = $"{currentTimeStr} / 直播";
            }
            else
            {
                timeDisplayText.text = $"{currentTimeStr} / 00:00";
            }
        }
    }

    /// <summary>
    /// 格式化时间（毫秒转为mm:ss或hh:mm:ss格式）
    /// </summary>
    private string FormatTime(long milliseconds)
    {
        TimeSpan timeSpan = TimeSpan.FromMilliseconds(milliseconds);
        
        if (timeSpan.Hours > 0)
        {
            return string.Format("{0:D2}:{1:D2}:{2:D2}", 
                timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
        }
        else
        {
            return string.Format("{0:D2}:{1:D2}", 
                timeSpan.Minutes, timeSpan.Seconds);
        }
    }

    /// <summary>
    /// 进度更新协程
    /// </summary>
    private IEnumerator UpdateProgressCoroutine()
    {
        while (true)
        {
            if (mediaPlayer != null && mediaPlayer.IsPlaying && !isDraggingSlider)
            {
                UpdateProgress();
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    /// <summary>
    /// 更新进度信息
    /// </summary>
    private void UpdateProgress()
    {
        if (isLiveStream)
        {
            // 直播流不更新进度条
            if (progressSlider != null)
            {
                progressSlider.value = 1.0f;
            }
            return;
        }

        // 获取当前时间和总时长
        long currentTime = GetCurrentTime();
        long duration = GetMediaDuration();

        if (duration > 0)
        {
            // 计算当前实际进度
            float actualProgress = (float)currentTime / duration;
            
            // 防止进度条往后跳
            if (actualProgress < previousProgress && 
                !isDraggingSlider && 
                Mathf.Abs(actualProgress - previousProgress) < 0.1f) // 允许小幅度回退（如网络波动）
            {
                // 保留前一帧的进度，忽略这次回退
                actualProgress = previousProgress;
            }
            
            // 更新进度条 - 带动画效果
            if (progressSlider != null && !isDraggingSlider)
            {
                // 平滑过渡到新的进度位置
                progressSlider.value = Mathf.Lerp(progressSlider.value, actualProgress, Time.deltaTime * animationSpeed);
            }
            
            // 更新时间显示（使用实际时间，不使用动画平滑后的值）
            UpdateTimeDisplay(currentTime, duration);
            
            // 保存当前进度用于下一帧比较
            previousProgress = isDraggingSlider ? previousProgress : actualProgress;
        }
    }

    /// <summary>
    /// 媒体播放器状态变化事件处理
    /// </summary>
    private void OnMediaPlayerStateChanged(libvlc_state_t state, string stateMessage)
    {
        //Debug.Log($"媒体播放器状态变化: {state} - {stateMessage}");
        
        bool isBuffering = state == libvlc_state_t.libvlc_Buffering;
        
        if (bufferingIndicator != null)
        {
            bufferingIndicator.SetActive(isBuffering);
        }

        UpdatePlayPauseButton();

        // 如果开始播放，尝试获取并显示分辨率
        if (state == libvlc_state_t.libvlc_Playing)
        {
            // 延迟获取分辨率，确保媒体已加载
            StartCoroutine(DelayedResolutionCheck());
        }

        // 如果媒体已结束
        if (state == libvlc_state_t.libvlc_Ended)
        {
            Debug.Log($"媒体播放结束，循环状态: {isLoopEnabled}，媒体URL: {currentMediaUrl}");
            
            if (progressSlider != null)
            {
                progressSlider.value = 1.0f;
            }
            
            // 如果启用了循环播放，则重新播放
            if (isLoopEnabled && !string.IsNullOrEmpty(currentMediaUrl) && !isLiveStream)
            {
                Debug.Log("开始循环播放");
                // 使用延迟调用确保UI状态更新完成后再重新播放
                StartCoroutine(RestartPlayback());
            }
        }
    }

    /// <summary>
    /// 延迟重新播放
    /// </summary>
    private IEnumerator RestartPlayback()
    {
        // 短暂延迟以确保状态完全更新
        yield return new WaitForSeconds(0.1f);
        
        if (mediaPlayer != null && isLoopEnabled)
        {
            mediaPlayer.SetUrl(currentMediaUrl, true);
            Debug.Log("循环播放已重新开始");
        }
    }

    /// <summary>
    /// 媒体播放器错误事件处理
    /// </summary>
    private void OnMediaPlayerError(string errorMessage)
    {
        Debug.LogError("媒体播放错误: " + errorMessage);
    }

    /// <summary>
    /// 初始化扩展播放器
    /// </summary>
    private void InitializeExtendedPlayer()
    {
        // 获取内部VLC播放器实例
        System.Reflection.FieldInfo fieldInfo = typeof(MediaPlayer).GetField("m_Player", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
        VlcMediaPlayer vlcPlayer = fieldInfo?.GetValue(mediaPlayer) as VlcMediaPlayer;
        
        if (vlcPlayer != null)
        {
            extendedPlayer = new ExtendedVlcPlayer(vlcPlayer);
            
            // 延迟检查是否为直播流，让媒体加载一段时间
            StartCoroutine(DelayedLiveStreamCheck());
        }
    }
    
    /// <summary>
    /// 延迟检查是否为直播流
    /// </summary>
    private IEnumerator DelayedLiveStreamCheck()
    {
        // 等待媒体加载和处理一段时间
        yield return new WaitForSeconds(2.0f);
        
        if (extendedPlayer != null)
        {
            // 使用更准确的方法判断是否为直播流
            bool isActuallyLiveStream = extendedPlayer.IsLiveStream();
            
            // 如果实际检测结果与初步判断不同，则更新UI
            if (isActuallyLiveStream != isLiveStream)
            {
                Debug.Log($"直播流状态更新：初步判断={isLiveStream}，实际检测={isActuallyLiveStream}");
                isLiveStream = isActuallyLiveStream;
                
                // 更新直播相关UI
                UpdateLiveStreamUI(isLiveStream);
            }
            
            // 是直播流且可跳转的特殊情况（例如HLS VOD）
            //if (isLiveStream && extendedPlayer.IsSeekable())
            //{
            //    // 对于可跳转的直播流，显示进度条但不显示时间
            //    if (progressSlider != null)
            //    {
            //        progressSlider.gameObject.SetActive(true);
            //        progressSlider.interactable = true;
            //    }
            //}
        }
    }
    
    /// <summary>
    /// 基于URL特征初步判断是否为直播流
    /// </summary>
    private bool IsLikelyLiveStream(string url)
    {
        if (string.IsNullOrEmpty(url))
            return false;
            
        url = url.ToLower();
        
        // 基于协议判断
        bool isLiveProtocol = url.Contains("rtmp://") || 
                              url.Contains("rtsp://") || 
                              url.Contains("rtp://");
                              
        // 基于常见直播格式判断
        bool isLiveFormat = url.Contains(".m3u8") && 
                            (url.Contains("live") || url.Contains("stream"));
                            
        // 其他直播流特征
        bool hasLiveKeywords = url.Contains("/live/") || 
                               url.Contains("livestream") || 
                               url.Contains("channel");
                               
        return isLiveProtocol || isLiveFormat || hasLiveKeywords;
    }

    /// <summary>
    /// 获取媒体当前时间（毫秒）
    /// </summary>
    private long GetCurrentTime()
    {
        if (extendedPlayer == null)
        {
            InitializeExtendedPlayer();
            if (extendedPlayer == null) return 0;
        }
        
        return extendedPlayer.GetTime();
    }

    /// <summary>
    /// 获取媒体总时长（毫秒）
    /// </summary>
    private long GetMediaDuration()
    {
        if (extendedPlayer == null)
        {
            InitializeExtendedPlayer();
            if (extendedPlayer == null) return 0;
        }
        
        return extendedPlayer.GetLength();
    }

    /// <summary>
    /// 延迟检查并显示分辨率信息
    /// </summary>
    private IEnumerator DelayedResolutionCheck()
    {
        // 等待媒体加载和处理一段时间
        yield return new WaitForSeconds(2.0f);
        
        if (extendedPlayer != null && resolutionText != null)
        {
            // 尝试获取分辨率
            string resolution = extendedPlayer.GetResolutionDescription();
            
            // 显示分辨率信息
            resolutionText.text = resolution;
            //Debug.Log($"视频分辨率: {resolution}");
        }
    }

    /// <summary>
    /// 进度条点击事件处理
    /// </summary>
    /// <param name="eventData">事件数据</param>
    private void OnProgressSliderClick(UnityEngine.EventSystems.BaseEventData eventData)
    {
        // 如果是直播流则不处理点击
        if (isLiveStream) return;
        
        // 转换为指针事件数据
        var pointerEventData = eventData as UnityEngine.EventSystems.PointerEventData;
        if (pointerEventData == null) return;
        
        // 计算点击位置对应的进度值
        RectTransform rectTransform = progressSlider.GetComponent<RectTransform>();
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, pointerEventData.position, pointerEventData.pressEventCamera, out localPoint))
        {
            // 计算水平位置的归一化值（0-1）
            float normalizedPosition = Mathf.Clamp01((localPoint.x - rectTransform.rect.min.x) / rectTransform.rect.width);
            
            // 立即跳转到该位置
            SeekToPosition(normalizedPosition);
            
            // 更新UI显示
            long duration = GetMediaDuration();
            if (duration > 0)
            {
                long currentTime = (long)(normalizedPosition * duration);
                UpdateTimeDisplay(currentTime, duration);
            }
            
            // 更新滑块位置
            progressSlider.value = normalizedPosition;
            
            //Debug.Log($"进度条点击位置: {normalizedPosition:F2}");
        }
    }

    /// <summary>
    /// 更新直播流相关UI显示
    /// </summary>
    /// <param name="isLive">是否为直播流</param>
    private void UpdateLiveStreamUI(bool isLive)
    {
        // 更新直播指示器
        if (liveIndicator != null)
        {
            liveIndicator.SetActive(isLive);
        }
        
        // 更新进度条显示和交互状态
        if (progressSlider != null)
        {
            // 直播模式：隐藏进度条
            progressSlider.gameObject.SetActive(!isLive);
            progressSlider.interactable = !isLive;
        }
        
        // 更新时间显示
        if (timeDisplayText != null)
        {
            // 直播模式：隐藏时间显示
            timeDisplayText.gameObject.SetActive(!isLive);
            
            if (isLive)
            {
                timeDisplayText.text = "直播中";
            }
            else
            {
                timeDisplayText.text = "00:00 / 00:00";
            }
        }
    }

    #endregion
} 