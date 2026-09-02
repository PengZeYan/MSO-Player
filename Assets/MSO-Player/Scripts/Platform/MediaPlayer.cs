using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;
using yan.libvlc.Core;

namespace yan.libvlc
{
    /// <summary>
    /// Unity媒体播放器组件，负责将LibVLC视频输出到Unity UI
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class MediaPlayer : MonoBehaviour
    {
        #region 序列化字段

        [FormerlySerializedAs("url")]
        [SerializeField, Tooltip("媒体URL地址")]
        private string m_Url;

        [FormerlySerializedAs("width")]
        [SerializeField, Min(1), Tooltip("输出分辨率宽度，必须大于0")]
        private int m_Width = 1280;

        [FormerlySerializedAs("height")]
        [SerializeField, Min(1), Tooltip("输出分辨率高度，必须大于0")]
        private int m_Height = 720;

        [FormerlySerializedAs("autoscaleRawImage")]
        [SerializeField, Tooltip("是否自动调整rawImage的比例以适应宽高比，只在初始化的时候生效")]
        private bool m_AutoscaleRawImage = true;

        [FormerlySerializedAs("mute")]
        [SerializeField, Tooltip("是否静音")]
        private bool m_Mute = true;

        [FormerlySerializedAs("m_PlayOnStart")]
        [SerializeField, Tooltip("启动时自动播放")]
        private bool m_PlayOnAwake = false;

        [SerializeField, Tooltip("是否启用对象池")]
        private bool m_UseObjectPool = true;

        #endregion

        #region 私有字段

        private Texture2D m_Texture;
        private VlcMediaPlayer m_Player;
        private RawImage m_RawImage;
        private libvlc_state_t m_CurrentMediaState;
        private libvlc_state_t m_PreviousMediaState; // 用于跟踪状态变化
        private Coroutine m_StateMonitorCoroutine; // 用于跟踪和停止状态监控协程
        private bool m_IsReleased = false; // 标记是否已释放回对象池
        private bool m_IsDestroyed = false; // 标记组件是否已被销毁
        private bool m_HasBeenDisabled = false;
        private bool m_ShouldResumeOnEnable = false;

        #endregion

        #region 公共属性与事件

        /// <summary>
        /// 当媒体播放器状态变化时触发的事件
        /// </summary>
        public UnityAction<libvlc_state_t, string> OnMediaPlayerStateEvent;

        /// <summary>
        /// 当媒体播放发生错误时触发的事件
        /// </summary>
        public UnityAction<string> OnMediaPlayerErrorEvent;

        /// <summary>
        /// 开始播放时触发的事件
        /// </summary>
        [SerializeField]
        public UnityEvent OnPlayEvent;

        /// <summary>
        /// 停止播放时触发的事件
        /// </summary>
        [SerializeField]
        public UnityEvent OnStopEvent;

        /// <summary>
        /// 获取当前媒体URL
        /// </summary>
        public string Url => m_Url;

        /// <summary>
        /// 获取当前媒体状态
        /// </summary>
        public libvlc_state_t CurrentMediaState => m_CurrentMediaState;

        internal VlcMediaPlayer CorePlayer => m_Player;

        /// <summary>
        /// 检查是否正在播放
        /// </summary>
        public bool IsPlaying
        {
            get
            {
                if (m_Player == null)
                    return false;

                try
                {
                    return m_Player.IsPlaying();
                }
                catch (Exception ex)
                {
                    LogWarning($"检查播放状态失败: {ex.Message}");
                    return false;
                }
            }
        }

        #endregion

        #region Unity生命周期方法

        protected virtual void OnAwake()
        {
            // 初始化事件对象
            if (OnPlayEvent == null)
                OnPlayEvent = new UnityEvent();

            if (OnStopEvent == null)
                OnStopEvent = new UnityEvent();

            // 初始化状态
            m_CurrentMediaState = libvlc_state_t.libvlc_NothingSpecial;
            m_PreviousMediaState = libvlc_state_t.libvlc_NothingSpecial;
            m_IsReleased = false;
            m_IsDestroyed = false;

            InitializeRawImage();

            if (m_PlayOnAwake)
                Play();
        }
        
        private void Awake()
        {
            OnAwake();
        }

        private void Update()
        {
            UpdateTexture();
        }

        protected virtual void OnDestroy()
        {
            m_IsDestroyed = true;
            CleanupResources();
        }

        private void OnEnable()
        {
            // Unity首次启用时由OnAwake中的配置决定是否播放。
            // 只有经历过OnDisable后，才在这里恢复或重建。
            if (!m_HasBeenDisabled)
                return;

            m_HasBeenDisabled = false;
            if (!m_ShouldResumeOnEnable)
                return;

            m_ShouldResumeOnEnable = false;

            // 如果在OnDisable中释放了播放器到对象池，则需要重新创建
            if (m_IsReleased || m_Player == null)
            {
                m_IsReleased = false; // 重置释放标志

                if (!string.IsNullOrEmpty(m_Url))
                {
                    Play();
                }
                return;
            }

            // 界面启用时恢复播放
            if (m_Player != null)
            {
                try
                {
                    if (!m_Player.IsPlaying() && !m_Player.Resume())
                        LogWarning("界面启用后恢复播放失败");
                }
                catch (Exception ex)
                {
                    // 播放器可能已失效，需要重新创建
                    LogWarning($"恢复播放器失败，将重新创建: {ex.Message}");
                    m_Player?.Dispose();
                    m_Player = null;
                    if (!string.IsNullOrEmpty(m_Url) && !m_IsReleased)
                    {
                        Play();
                    }
                }
            }
        }

        protected virtual void OnDisable()
        {
            m_HasBeenDisabled = true;
            m_ShouldResumeOnEnable = m_Player != null && IsPlaying;

            // 当组件被禁用时，释放资源到对象池以便其他地方复用
            if (m_Player != null && !m_IsReleased && !m_IsDestroyed && m_UseObjectPool)
            {
                CleanupResources();
            }
            // 如果不使用对象池或已经释放，则只需暂停播放
            else if (m_Player != null && IsPlaying)
            {
                try
                {
                    m_Player.Pause();
                }
                catch (Exception ex)
                {
                    LogWarning($"组件禁用时暂停播放器失败: {ex.Message}");
                }
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置媒体URL地址并可选择是否自动播放
        /// </summary>
        /// <param name="url">媒体URL</param>
        /// <param name="autoPlay">是否自动播放</param>
        public void SetUrl(string url, bool autoPlay = false)
        {
            if (string.IsNullOrEmpty(url))
            {
                LogError("媒体URL不能为空");
                return;
            }

            m_Url = url;
            if (autoPlay)
            {
                CheckEditorPlaying();

                if (m_Player == null)
                {
                    Play();
                }
                else
                {
                    try
                    {
                        m_Player.UpdateUrl(url);
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"切换媒体失败，将重建播放器: {ex.Message}");
                        m_Player?.Dispose();
                        m_Player = null;
                        Play();
                    }
                }
            }
        }

        /// <summary>
        /// 开始播放
        /// </summary>
        public void Play()
        {
            CheckEditorPlaying();

            if (!gameObject.activeSelf)
            {
                LogWarning("GameObject未激活，忽略播放请求");
                return;
            }

            if (string.IsNullOrEmpty(m_Url))
            {
                LogError("未设置媒体URL，无法播放");
                return;
            }

            // 重置释放状态
            m_IsReleased = false;

            bool createdPlayer = false;
            if (m_Player == null)
            {
                try
                {
                    CreatePlayer();
                    createdPlayer = true;
                }
                catch (Exception ex)
                {
                    HandlePlaybackFailure("创建播放器失败", ex);
                    return;
                }
            }

            if (m_Player == null)
            {
                LogError("无法创建播放器");
                return;
            }

            try
            {
                if (!createdPlayer && !m_Player.IsPlaying() && !m_Player.Play())
                    LogWarning("播放器未能开始播放");
            }
            catch (Exception ex)
            {
                LogWarning($"播放器实例失效，将重新创建: {ex.Message}");
                m_Player.Dispose();
                m_Player = null;
                try
                {
                    CreatePlayer();
                }
                catch (Exception recreateException)
                {
                    HandlePlaybackFailure("重新创建播放器失败", recreateException);
                }
            }
        }

        /// <summary>
        /// 停止播放媒体
        /// </summary>
        public void Stop()
        {
            CheckEditorPlaying();

            try
            {
                if (m_Player != null)
                {
                    m_Player.Stop();
                }
            }
            catch (Exception ex)
            {
                LogWarning($"停止播放失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 暂停或恢复播放
        /// </summary>
        public void Pause()
        {
            CheckEditorPlaying();

            try
            {
                if (m_Player != null && !m_Player.TogglePause())
                    LogWarning("播放器未能切换暂停状态");
            }
            catch (Exception ex)
            {
                LogWarning($"切换暂停状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新当前播放内容
        /// </summary>
        public void Refresh()
        {
            CheckEditorPlaying();
            SetUrl(m_Url, true);
        }

        /// <summary>
        /// 释放当前核心实例，并使用当前组件配置重新创建。
        /// 平台子类在实例级VLC参数变化后使用此方法。
        /// </summary>
        protected void RecreatePlayer()
        {
            RecreatePlayer(IsPlaying);
        }

        /// <summary>
        /// 释放当前核心实例，并按调用方指定的状态决定是否重新播放。
        /// </summary>
        protected void RecreatePlayer(bool resumePlayback)
        {
            bool shouldPlay = resumePlayback && gameObject.activeInHierarchy && !string.IsNullOrEmpty(m_Url);
            CleanupResources();
            m_IsReleased = false;

            if (shouldPlay)
                Play();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 记录信息日志
        /// </summary>
        private void LogInfo(string message)
        {
            // 移除调试信息输出
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        private void LogWarning(string message)
        {
            Debug.LogWarning($"[MediaPlayer] {message}", this);
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        private void LogError(string message)
        {
            Debug.LogError($"[MediaPlayer] {message}", this);
        }

        private void HandlePlaybackFailure(string context, Exception exception)
        {
            string message = $"{context}: {exception.Message}";
            LogError(message);
            OnMediaPlayerErrorEvent?.Invoke(message);

            if (m_Player == null)
                return;

            try
            {
                m_Player.Dispose();
            }
            catch (Exception disposeException)
            {
                LogError($"清理失败播放器时发生错误: {disposeException.Message}");
            }
            finally
            {
                m_Player = null;
            }
        }

        /// <summary>
        /// 初始化RawImage组件
        /// </summary>
        private void InitializeRawImage()
        {
            m_RawImage = GetComponent<RawImage>();

            if (m_RawImage != null)
            {
                return;
            }

            LogError("缺少RawImage组件");
        }

        /// <summary>
        /// 创建VLC播放器实例并开始监视状态
        /// </summary>
        private void CreatePlayer()
        {
            // 检测是否有Android特定播放器组件
            MediaPlayerAndroid androidPlayer = GetComponent<MediaPlayerAndroid>();

            if (androidPlayer != null && Application.platform == RuntimePlatform.Android)
            {
                // 使用Android特定的播放器创建方法
                m_Player = androidPlayer.CreateAndroidPlayer(m_Url, m_Width, m_Height, m_Mute);
                m_IsReleased = false; // 重置释放标志
            }
            else if (m_UseObjectPool)
            {
                // 从对象池获取或创建播放器
                try
                {
                    m_Player = MediaPlayerPool.Instance.GetPlayer(m_Width, m_Height, m_Url, m_Mute);
                    m_IsReleased = false; // 重置释放标志
                }
                catch (Exception ex)
                {
                    LogWarning($"对象池获取失败，改为直接创建: {ex.Message}");
                    m_Player = new VlcMediaPlayer(m_Width, m_Height, m_Url, m_Mute);
                    m_IsReleased = false; // 重置释放标志
                }
            }
            else
            {
                // 直接创建播放器实例，不使用对象池
                m_Player = new VlcMediaPlayer(m_Width, m_Height, m_Url, m_Mute);
                m_IsReleased = false; // 重置释放标志
            }

            m_PreviousMediaState = m_CurrentMediaState;
            m_CurrentMediaState = libvlc_state_t.libvlc_NothingSpecial;

            if (m_Texture == null)
            {
                CreateTexture();
            }

            // 如果之前有协程在运行，先停止
            if (m_StateMonitorCoroutine != null)
            {
                StopCoroutine(m_StateMonitorCoroutine);
            }

            // 启动新的状态监控协程
            m_StateMonitorCoroutine = StartCoroutine(SupervisePlayerState());
        }

        /// <summary>
        /// 创建纹理并应用到RawImage
        /// </summary>
        private void CreateTexture()
        {
            try
            {
                if (m_RawImage == null || m_Player == null)
                    return;

                int textureWidth = m_Player.OutputWidth;
                int textureHeight = m_Player.OutputHeight;

                if (textureWidth > 0 && textureHeight > 0)
                {
                    if (m_Texture != null)
                    {
                        Destroy(m_Texture);
                    }

                    m_Texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGB24, false, false);
                    m_RawImage.texture = m_Texture;

                    // 修正画面上下颠倒问题：翻转UV坐标
                    m_RawImage.uvRect = new Rect(0, 1, 1, -1);

                    if (m_AutoscaleRawImage)
                    {
                        RectTransform rect = m_RawImage.rectTransform;
                        float ratio = textureHeight / (float)textureWidth;
                        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rect.rect.width * ratio);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"创建视频纹理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新纹理数据
        /// </summary>
        private void UpdateTexture()
        {
            if (m_Player == null || m_Texture == null)
            {
                return;
            }

            try
            {
                if (m_Player.CheckForImageUpdate(out byte[] imageData))
                {
                    int expectedSize = m_Texture.width * m_Texture.height * 3;
                    if (imageData == null || imageData.Length != expectedSize)
                    {
                        LogError($"视频帧大小不匹配，期望 {expectedSize} 字节，实际 {imageData?.Length ?? 0} 字节");
                        return;
                    }

                    m_Texture.LoadRawTextureData(imageData);
                    m_Texture.Apply(false);
                }
            }
            catch (Exception ex)
            {
                LogError($"更新视频纹理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 监视播放器状态的协程
        /// </summary>
        private IEnumerator SupervisePlayerState()
        {
            WaitForSeconds wait = new WaitForSeconds(0.5f);
            int errorCount = 0;
            const int MAX_ERROR_COUNT = 5; // 连续错误超过此数量将停止协程

            while (m_Player != null && !m_IsDestroyed)
            {
                try
                {
                    libvlc_state_t state = m_Player.State;
                    errorCount = 0; // 重置错误计数

                    if (state != m_CurrentMediaState)
                    {
                        // 保存前一个状态
                        m_PreviousMediaState = m_CurrentMediaState;

                        // 更新当前状态
                        m_CurrentMediaState = state;
                        OnMediaPlayerStateEvent?.Invoke(state, StateToString(state));

                        // 检测开始播放事件
                        if (state == libvlc_state_t.libvlc_Playing &&
                            m_PreviousMediaState != libvlc_state_t.libvlc_Playing)
                        {
                            OnPlayEvent?.Invoke();
                        }

                        // 检测停止播放事件
                        if ((state == libvlc_state_t.libvlc_Stopped || state == libvlc_state_t.libvlc_Ended) &&
                            (m_PreviousMediaState == libvlc_state_t.libvlc_Playing ||
                             m_PreviousMediaState == libvlc_state_t.libvlc_Paused ||
                             m_PreviousMediaState == libvlc_state_t.libvlc_Buffering))
                        {
                            OnStopEvent?.Invoke();
                        }

                        // 检测错误状态并触发错误事件
                        if (state == libvlc_state_t.libvlc_Error)
                        {
                            string errorMessage = "播放媒体时发生错误";

                            OnMediaPlayerErrorEvent?.Invoke(errorMessage);

                            // 错误也触发停止播放事件
                            OnStopEvent?.Invoke();
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    LogWarning($"读取播放器状态失败 ({errorCount}/{MAX_ERROR_COUNT}): {ex.Message}");

                    if (errorCount >= MAX_ERROR_COUNT)
                    {
                        break;
                    }
                }

                yield return wait;
            }

            m_StateMonitorCoroutine = null;
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void CleanupResources()
        {
            // 如果已经释放，不需要再次执行
            if (m_IsReleased && m_Player == null)
            {
                return;
            }

            // 停止状态监控协程
            if (m_StateMonitorCoroutine != null)
            {
                StopCoroutine(m_StateMonitorCoroutine);
                m_StateMonitorCoroutine = null;
            }

            // 停止所有其他协程
            StopAllCoroutines();

            // 将VLC播放器归还到对象池
            if (m_Player != null && !m_IsReleased)
            {
                // 停止播放
                try
                {
                    if (m_Player.IsPlaying())
                    {
                        m_Player.Stop();
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"释放前停止播放器失败: {ex.Message}");
                }

                // 只处理非Android播放器
                MediaPlayerAndroid androidPlayer = GetComponent<MediaPlayerAndroid>();
                if (!(androidPlayer != null && Application.platform == RuntimePlatform.Android))
                {
                    if (m_UseObjectPool)
                    {
                        // 归还到对象池
                        try
                        {
                            MediaPlayerPool.Instance.ReleasePlayer(m_Player, m_Width, m_Height, m_Mute);
                            m_IsReleased = true;
                        }
                        catch (Exception ex)
                        {
                            LogWarning($"播放器归还对象池失败，将直接释放: {ex.Message}");
                            try
                            {
                                m_Player.Dispose();
                            }
                            catch (Exception disposeException)
                            {
                                LogError($"直接释放播放器失败: {disposeException.Message}");
                            }
                        }
                    }
                    else
                    {
                        // 直接释放
                        try
                        {
                            m_Player.Dispose();
                        }
                        catch (Exception ex)
                        {
                            LogError($"释放播放器失败: {ex.Message}");
                        }
                    }
                }
                else
                {
                    // Android播放器需要直接释放
                    try
                    {
                        m_Player.Dispose();
                    }
                    catch (Exception ex)
                    {
                        LogError($"释放Android播放器失败: {ex.Message}");
                    }
                }

                m_Player = null;
            }

            // 释放纹理资源
            if (m_Texture != null)
            {
                Destroy(m_Texture);
                m_Texture = null;

                // 清除RawImage的引用
                if (m_RawImage != null)
                {
                    m_RawImage.texture = null;
                }
            }
        }

        /// <summary>
        /// 将播放器状态转换为可读字符串
        /// </summary>
        private string StateToString(libvlc_state_t state)
        {
            return state switch
            {
                libvlc_state_t.libvlc_NothingSpecial => "无特殊状态",
                libvlc_state_t.libvlc_Opening => "媒体正在打开...",
                libvlc_state_t.libvlc_Buffering => "媒体正在缓冲...",
                libvlc_state_t.libvlc_Playing => "媒体正在播放",
                libvlc_state_t.libvlc_Paused => "媒体暂停播放",
                libvlc_state_t.libvlc_Stopped => "媒体已停止播放",
                libvlc_state_t.libvlc_Ended => "媒体已播放完毕",
                libvlc_state_t.libvlc_Error => "发生错误，无法继续播放",
                _ => "状态未知",
            };
        }

        /// <summary>
        /// 检查是否在编辑器播放状态
        /// </summary>
        private void CheckEditorPlaying()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                throw new System.Exception("请在播放模式下调用此方法");
            }
#endif
        }

        #endregion
    }
}
