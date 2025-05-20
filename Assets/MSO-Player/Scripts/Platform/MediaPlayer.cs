using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
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

        [SerializeField, Tooltip("媒体URL地址")]
        private string m_Url;

        [SerializeField, Min(0), Tooltip("输出分辨率宽度，≤0进行自动缩放")]
        private int m_Width = 1280;

        [SerializeField, Min(0), Tooltip("输出分辨率高度，≤0进行自动缩放")]
        private int m_Height = 720;

        [SerializeField, Tooltip("是否自动调整rawImage的比例以适应宽高比，只在初始化的时候生效")]
        private bool m_AutoscaleRawImage = true;

        [SerializeField, Tooltip("是否静音")]
        private bool m_Mute = true;

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
        private bool m_IsInitialized = false; // 标记是否完成了初始化
        private bool m_IsReleased = false; // 标记是否已释放回对象池
        private bool m_IsDestroyed = false; // 标记组件是否已被销毁

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

        private void Awake()
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

        private void Update()
        {
            UpdateTexture();
        }

        private void OnDestroy()
        {
            LogInfo($"OnDestroy");
            m_IsDestroyed = true;
            CleanupResources();
        }

        private void OnEnable()
        {
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
                    if (!m_Player.IsPlaying())
                    {
                        // 如果是暂停状态，使用Pause切换回播放状态
                        if (m_CurrentMediaState == libvlc_state_t.libvlc_Paused)
                        {
                            m_Player.Pause(); // 切换播放状态
                        }
                        // 如果是停止或其他状态，通过重新设置Url开始播放
                        else
                        {
                            m_Player.UpdateUrl(m_Url);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 播放器可能已失效，需要重新创建
                    m_Player = null;
                    if (!string.IsNullOrEmpty(m_Url) && !m_IsReleased)
                    {
                        Play();
                    }
                }
            }
        }

        private void OnDisable()
        {
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
                catch (Exception)
                {
                    // 忽略异常
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
                    catch (Exception)
                    {
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
                return;
            }

            if (string.IsNullOrEmpty(m_Url))
            {
                return;
            }

            // 重置释放状态
            m_IsReleased = false;

            if (m_Player == null)
            {
                CreatePlayer();
            }

            bool wasPlaying = false;

            try
            {
                wasPlaying = m_Player.IsPlaying();
            }
            catch (Exception)
            {
                CreatePlayer();
                wasPlaying = false;
            }

            if (!wasPlaying)
            {
                try
                {
                    m_Player.Pause(); // 通过Pause方法切换播放状态

                    // 检查是否开始播放了
                    if (m_Player.IsPlaying() && !wasPlaying)
                    {
                        // 直接触发开始播放事件
                        OnPlayEvent?.Invoke();
                    }
                }
                catch (Exception)
                {
                    // 忽略异常
                }
            }

            try
            {
                if (m_Player.IsPlaying())
                {
                    m_Player.UpdateUrl(m_Url);
                }
            }
            catch (Exception)
            {
                // 忽略异常
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
                    bool wasPlaying = m_Player.IsPlaying();
                    m_Player.Stop();

                    // 如果之前在播放状态，直接触发停止事件
                    if (wasPlaying)
                    {
                        OnStopEvent?.Invoke();
                    }
                }
            }
            catch (Exception)
            {
                // 忽略异常
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
                m_Player?.Pause();
            }
            catch (Exception)
            {
                // 忽略异常
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
            // 移除调试信息输出
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        private void LogError(string message)
        {
            // 移除调试信息输出
        }

        /// <summary>
        /// 初始化RawImage组件
        /// </summary>
        private void InitializeRawImage()
        {
            m_RawImage = GetComponent<RawImage>();

            if (m_RawImage != null)
            {
                m_IsInitialized = true;
            }
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
                catch (Exception)
                {
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
                if ((m_Width <= 0 || m_Height <= 0) && m_Player?.VideoTrack != null)
                {
                    m_Width = (int)m_Player.VideoTrack.Value.i_width;
                    m_Height = (int)m_Player.VideoTrack.Value.i_height;
                }

                if (m_Width > 0 && m_Height > 0)
                {
                    if (m_Texture != null)
                    {
                        Destroy(m_Texture);
                    }

                    m_Texture = new Texture2D(m_Width, m_Height, TextureFormat.RGB24, false, false);
                    m_RawImage.texture = m_Texture;

                    // 修正画面上下颠倒问题：翻转UV坐标
                    m_RawImage.uvRect = new Rect(0, 1, 1, -1);

                    if (m_AutoscaleRawImage)
                    {
                        RectTransform rect = m_RawImage.rectTransform;
                        float ratio = m_Height / (float)m_Width;
                        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rect.rect.width * ratio);
                    }
                }
            }
            catch (Exception)
            {
                // 忽略异常
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
                    m_Texture.LoadRawTextureData(imageData);
                    m_Texture.Apply(false);
                }
            }
            catch
            {
                // 忽略异常
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
                            string errorMessage = $"播放 {m_Url} 时发生错误";

                            // 获取VLC的具体错误信息
                            string vlcError = m_Player.GetErrorMessage();
                            if (!string.IsNullOrEmpty(vlcError))
                            {
                                errorMessage += $": {vlcError}";
                            }

                            OnMediaPlayerErrorEvent?.Invoke(errorMessage);

                            // 错误也触发停止播放事件
                            OnStopEvent?.Invoke();
                        }
                    }
                }
                catch (Exception)
                {
                    errorCount++;

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
                catch (Exception)
                {
                    // 忽略异常
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
                        catch (Exception)
                        {
                            try
                            {
                                m_Player.Dispose();
                            }
                            catch
                            {
                                // 忽略异常
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
                        catch
                        {
                            // 忽略异常
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
                    catch
                    {
                        // 忽略异常
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
