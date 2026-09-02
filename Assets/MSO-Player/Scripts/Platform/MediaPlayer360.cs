using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using yan.libvlc.Core;

namespace yan.libvlc
{
    /// <summary>
    /// 全景360度视频播放器组件，用于在球体上播放全景视频。
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class MediaPlayer360 : MonoBehaviour
    {
        #region 序列化字段

        [SerializeField, Tooltip("媒体URL地址")]
        private string m_Url;

        [SerializeField, Min(1), Tooltip("输出分辨率宽度，必须大于0")]
        private int m_Width = 1920;

        [SerializeField, Min(1), Tooltip("输出分辨率高度，必须大于0")]
        private int m_Height = 960;

        [SerializeField, Tooltip("是否静音")]
        private bool m_Mute;

        [SerializeField, Tooltip("启动时自动播放")]
        private bool m_PlayOnStart = true;

        [SerializeField, Tooltip("是否反转Y轴（上下翻转图像）")]
        private bool m_FlipY = true;

        [SerializeField, Tooltip("使用Shader翻转而非CPU翻转（性能更好）")]
        private bool m_UseShaderFlip = true;

        [SerializeField, Min(0), Tooltip("无视频数据最大等待时间(秒)，超过此时间将自动尝试恢复播放，0表示禁用")]
        private float m_MaxNoDataWaitTime = 5.0f;

        [SerializeField, Min(0.1f), Tooltip("检测视频流状态的时间间隔(秒)")]
        private float m_StatusCheckInterval = 0.5f;

        #endregion

        #region 私有字段

        private const int MAX_RECOVERY_ATTEMPTS = 3;
        private const float HEALTHY_PLAYBACK_RESET_SECONDS = 10f;

        private Texture2D m_Texture;
        private VlcMediaPlayer m_Player;
        private MeshRenderer m_MeshRenderer;
        private Material m_Material;
        private libvlc_state_t m_CurrentMediaState;
        private byte[] m_TempRowBuffer;
        private bool m_IsInitialized;
        private bool m_IsDestroyed;
        private bool m_OwnsMaterial;
        private bool m_WasPlayingBeforeDisable;
        private bool m_WasPlayingBeforeApplicationPause;
        private bool m_IsRecovering;
        private int m_FailedRecoveryAttempts;
        private float m_HealthyPlaybackStartedAt = -1f;
        private Coroutine m_StatusMonitorCoroutine;
        private Coroutine m_RecoveryCoroutine;

        #endregion

        #region 公共属性与事件

        public UnityAction<string> OnMediaPlayerStateEvent;
        public UnityAction<string> OnMediaPlayerErrorEvent;
        public UnityAction OnMediaPlayerRecoveryEvent;

        public string Url => m_Url;
        public libvlc_state_t CurrentMediaState => m_CurrentMediaState;
        public bool IsPlaying => m_Player != null && !m_Player.IsDisposed && m_Player.IsPlaying();

        #endregion

        #region Unity生命周期方法

        private void Start()
        {
            if (m_PlayOnStart)
            {
                Play();
                return;
            }

            try
            {
                InitializeMeshRenderer();
            }
            catch (Exception ex)
            {
                HandlePlayerFailure("初始化360°渲染器失败", ex);
            }
        }

        private void Update()
        {
            UpdateTexture();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                m_WasPlayingBeforeApplicationPause = IsPlaying;
                if (m_WasPlayingBeforeApplicationPause)
                {
                    m_Player.Pause();
                }
            }
            else if (m_WasPlayingBeforeApplicationPause && isActiveAndEnabled)
            {
                m_WasPlayingBeforeApplicationPause = false;
                ResumeCurrentPlayer();
            }
        }

        private void OnEnable()
        {
            if (!m_WasPlayingBeforeDisable)
                return;

            m_WasPlayingBeforeDisable = false;
            ResumeCurrentPlayer();
        }

        private void OnDisable()
        {
            m_WasPlayingBeforeDisable = IsPlaying;
            StopStatusMonitor();
            StopRecovery();

            if (m_WasPlayingBeforeDisable)
            {
                try
                {
                    m_Player.Pause();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"360°播放器禁用时暂停失败: {ex.Message}");
                }
            }
        }

        private void OnDestroy()
        {
            m_IsDestroyed = true;
            CleanupResources();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置媒体URL地址并可选择是否自动播放。
        /// </summary>
        public void SetUrl(string url, bool autoPlay = false)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                Debug.LogError("媒体URL不能为空");
                return;
            }

            m_Url = url;
            m_FailedRecoveryAttempts = 0;
            m_HealthyPlaybackStartedAt = -1f;

            if (!autoPlay)
                return;

            CheckEditorPlaying();

            try
            {
                if (m_Player == null || m_Player.IsDisposed)
                {
                    Play();
                    return;
                }

                StopRecovery();
                m_Player.UpdateUrl(url);
                m_IsInitialized = true;
                CreateOrResizeTexture();
                StartStatusMonitor();
            }
            catch (Exception ex)
            {
                HandlePlayerFailure("切换360°媒体失败", ex);
            }
        }

        /// <summary>
        /// 开始或恢复播放媒体。
        /// </summary>
        public void Play()
        {
            CheckEditorPlaying();

            if (string.IsNullOrWhiteSpace(m_Url))
            {
                Debug.LogError("未设置URL，无法播放");
                return;
            }

            try
            {
                InitializeMeshRenderer();

                if (m_Player == null || m_Player.IsDisposed)
                {
                    DisposeCurrentPlayer();
                    CreatePlayer();
                }
                else if (!m_Player.IsPlaying() && !m_Player.Play())
                {
                    throw new InvalidOperationException("LibVLC未能开始播放");
                }

                CreateOrResizeTexture();
                m_IsInitialized = true;
                StartStatusMonitor();
            }
            catch (Exception ex)
            {
                HandlePlayerFailure("启动360°媒体失败", ex);
            }
        }

        public void Stop()
        {
            CheckEditorPlaying();
            m_Player?.Stop();
        }

        public void Pause()
        {
            CheckEditorPlaying();
            if (m_Player != null && !m_Player.TogglePause())
            {
                Debug.LogWarning("360°播放器未能切换暂停状态");
            }
        }

        public void Refresh()
        {
            CheckEditorPlaying();
            m_FailedRecoveryAttempts = 0;
            SetUrl(m_Url, true);
        }

        #endregion

        #region 初始化与纹理

        private void InitializeMeshRenderer()
        {
            if (m_MeshRenderer == null)
            {
                m_MeshRenderer = GetComponent<MeshRenderer>();
            }

            if (m_MeshRenderer == null)
            {
                throw new MissingComponentException("无法获取MeshRenderer组件");
            }

            if (m_Material != null)
                return;

            Shader shader;
            if (m_MeshRenderer.sharedMaterial == null)
            {
                shader = Shader.Find("Skybox/Panoramic") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
                if (shader == null)
                {
                    throw new InvalidOperationException("未找到可用于360°视频的Shader");
                }

                m_Material = new Material(shader) { name = "Default360Material" };
            }
            else
            {
                m_Material = new Material(m_MeshRenderer.sharedMaterial)
                {
                    name = m_MeshRenderer.sharedMaterial.name + " (360 Player Instance)"
                };
            }

            m_OwnsMaterial = true;
            m_MeshRenderer.material = m_Material;
        }

        private void CreatePlayer()
        {
            int width = m_Width;
            int height = m_Height;

            if (width > 4096 || height > 4096)
            {
                width = Mathf.Min(width, 4096);
                height = Mathf.Min(height, 2048);
                Debug.LogWarning($"全景视频输出分辨率过高，已限制为 {width}x{height}");
            }

            m_Player = new VlcMediaPlayer(width, height, m_Url, m_Mute);
            m_IsInitialized = true;
            m_CurrentMediaState = libvlc_state_t.libvlc_NothingSpecial;
        }

        private void CreateOrResizeTexture()
        {
            if (m_Player == null)
                return;

            int outputWidth = m_Player.OutputWidth;
            int outputHeight = m_Player.OutputHeight;
            if (outputWidth <= 0 || outputHeight <= 0)
            {
                throw new InvalidOperationException("播放器输出分辨率无效");
            }

            if (m_Texture == null ||
                m_Texture.width != outputWidth ||
                m_Texture.height != outputHeight ||
                m_Texture.format != TextureFormat.RGB24)
            {
                if (m_Texture != null)
                {
                    Destroy(m_Texture);
                }

                m_Texture = new Texture2D(outputWidth, outputHeight, TextureFormat.RGB24, false, false)
                {
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear
                };
            }

            if (m_FlipY && !m_UseShaderFlip)
            {
                int rowSize = checked(outputWidth * 3);
                if (m_TempRowBuffer == null || m_TempRowBuffer.Length != rowSize)
                {
                    m_TempRowBuffer = new byte[rowSize];
                }
            }
            else
            {
                m_TempRowBuffer = null;
            }

            m_Material.mainTexture = m_Texture;
            UpdateTextureScale();
        }

        private void UpdateTextureScale()
        {
            if (m_Material == null)
                return;

            if (m_FlipY && m_UseShaderFlip)
            {
                m_Material.mainTextureScale = new Vector2(1, -1);
                m_Material.mainTextureOffset = new Vector2(0, 1);
            }
            else
            {
                m_Material.mainTextureScale = Vector2.one;
                m_Material.mainTextureOffset = Vector2.zero;
            }

            if (m_Material.HasProperty("_Mapping"))
            {
                m_Material.SetFloat("_Mapping", 1);
            }

            if (m_Material.HasProperty("_Layout"))
            {
                m_Material.SetFloat("_Layout", 0);
            }
        }

        private void UpdateTexture()
        {
            if (!m_IsInitialized || m_Player == null || m_Texture == null)
                return;

            try
            {
                if (!m_Player.CheckForImageUpdate(out byte[] imageData))
                    return;

                int expectedSize = checked(m_Texture.width * m_Texture.height * 3);
                if (imageData == null || imageData.Length != expectedSize)
                {
                    Debug.LogWarning($"360°视频帧大小不匹配：期望 {expectedSize} 字节，实际 {imageData?.Length ?? 0} 字节");
                    return;
                }

                if (m_FlipY && !m_UseShaderFlip)
                {
                    FlipTextureDataVertically(imageData, m_Texture.width, m_Texture.height);
                }

                m_Texture.LoadRawTextureData(imageData);
                m_Texture.Apply(false);
            }
            catch (Exception ex)
            {
                Debug.LogError($"更新360°视频纹理失败: {ex.Message}");
            }
        }

        private void FlipTextureDataVertically(byte[] imageData, int width, int height)
        {
            int stride = checked(width * 3);
            if (m_TempRowBuffer == null || m_TempRowBuffer.Length != stride)
            {
                m_TempRowBuffer = new byte[stride];
            }

            for (int y = 0; y < height / 2; y++)
            {
                int topRowStart = y * stride;
                int bottomRowStart = (height - y - 1) * stride;
                Buffer.BlockCopy(imageData, topRowStart, m_TempRowBuffer, 0, stride);
                Buffer.BlockCopy(imageData, bottomRowStart, imageData, topRowStart, stride);
                Buffer.BlockCopy(m_TempRowBuffer, 0, imageData, bottomRowStart, stride);
            }
        }

        #endregion

        #region 状态监控与恢复

        private void StartStatusMonitor()
        {
            StopStatusMonitor();
            if (isActiveAndEnabled && m_IsInitialized && m_Player != null)
            {
                m_StatusMonitorCoroutine = StartCoroutine(MonitorPlayerStatus(m_Player));
            }
        }

        private void StopStatusMonitor()
        {
            if (m_StatusMonitorCoroutine == null)
                return;

            StopCoroutine(m_StatusMonitorCoroutine);
            m_StatusMonitorCoroutine = null;
        }

        private IEnumerator MonitorPlayerStatus(VlcMediaPlayer monitoredPlayer)
        {
            WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.1f, m_StatusCheckInterval));
            libvlc_state_t previousState = libvlc_state_t.libvlc_NothingSpecial;

            while (ReferenceEquals(m_Player, monitoredPlayer) &&
                   !monitoredPlayer.IsDisposed &&
                   m_IsInitialized)
            {
                libvlc_state_t currentState = libvlc_state_t.libvlc_Error;
                bool stateReadFailed = false;
                try
                {
                    currentState = monitoredPlayer.State;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"读取360°播放器状态失败: {ex.Message}");
                    RequestRecovery("状态读取异常");
                    stateReadFailed = true;
                }

                if (stateReadFailed)
                {
                    yield return wait;
                    continue;
                }

                bool stateChanged = currentState != previousState;
                if (stateChanged)
                {
                    m_CurrentMediaState = currentState;
                    OnMediaPlayerStateEvent?.Invoke(StateToString(currentState));
                    previousState = currentState;
                }

                if (currentState == libvlc_state_t.libvlc_Error && stateChanged)
                {
                    string errorMessage = "360°媒体播放发生错误";
                    Debug.LogError(errorMessage);
                    OnMediaPlayerErrorEvent?.Invoke(errorMessage);
                    RequestRecovery("LibVLC错误状态");
                }
                else if (currentState == libvlc_state_t.libvlc_Playing)
                {
                    UpdateHealthyPlaybackWindow(monitoredPlayer);

                    if (m_MaxNoDataWaitTime > 0 &&
                        monitoredPlayer.NoImageDataReceivedTime > m_MaxNoDataWaitTime)
                    {
                        Debug.LogWarning($"360°播放器超过 {m_MaxNoDataWaitTime:F1} 秒未收到视频帧");
                        RequestRecovery("视频帧超时");
                    }
                }
                else
                {
                    m_HealthyPlaybackStartedAt = -1f;
                }

                yield return wait;
            }

            if (ReferenceEquals(m_Player, monitoredPlayer))
            {
                m_StatusMonitorCoroutine = null;
            }
        }

        private void UpdateHealthyPlaybackWindow(VlcMediaPlayer monitoredPlayer)
        {
            bool frameFlowHealthy = m_MaxNoDataWaitTime <= 0 ||
                                    monitoredPlayer.NoImageDataReceivedTime <= m_MaxNoDataWaitTime;
            if (!frameFlowHealthy)
            {
                m_HealthyPlaybackStartedAt = -1f;
                return;
            }

            if (m_HealthyPlaybackStartedAt < 0)
            {
                m_HealthyPlaybackStartedAt = Time.realtimeSinceStartup;
            }
            else if (Time.realtimeSinceStartup - m_HealthyPlaybackStartedAt >= HEALTHY_PLAYBACK_RESET_SECONDS)
            {
                m_FailedRecoveryAttempts = 0;
            }
        }

        private void RequestRecovery(string reason)
        {
            if (m_IsRecovering || m_IsDestroyed || !isActiveAndEnabled || m_Player == null)
                return;

            if (m_FailedRecoveryAttempts >= MAX_RECOVERY_ATTEMPTS)
            {
                Debug.LogWarning($"360°播放器已达到最大恢复次数 ({MAX_RECOVERY_ATTEMPTS})，停止自动恢复");
                return;
            }

            Debug.LogWarning($"360°播放器请求自动恢复：{reason}，第 {m_FailedRecoveryAttempts + 1}/{MAX_RECOVERY_ATTEMPTS} 次");
            m_RecoveryCoroutine = StartCoroutine(AttemptRecovery());
        }

        private IEnumerator AttemptRecovery()
        {
            m_IsRecovering = true;
            m_FailedRecoveryAttempts++;
            m_HealthyPlaybackStartedAt = -1f;
            OnMediaPlayerRecoveryEvent?.Invoke();

            StopStatusMonitor();

            try
            {
                m_Player?.Stop();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"360°播放器恢复前停止失败: {ex.Message}");
            }

            yield return new WaitForSeconds(0.5f);

            if (m_IsDestroyed || !isActiveAndEnabled)
            {
                m_IsRecovering = false;
                m_RecoveryCoroutine = null;
                yield break;
            }

            try
            {
                DisposeCurrentPlayer();
                CreatePlayer();
                CreateOrResizeTexture();
                StartStatusMonitor();
            }
            catch (Exception ex)
            {
                HandlePlayerFailure("自动恢复360°媒体失败", ex);
            }

            m_IsRecovering = false;
            m_RecoveryCoroutine = null;
        }

        private void StopRecovery()
        {
            if (m_RecoveryCoroutine != null)
            {
                StopCoroutine(m_RecoveryCoroutine);
                m_RecoveryCoroutine = null;
            }

            m_IsRecovering = false;
        }

        private void ResumeCurrentPlayer()
        {
            if (m_IsDestroyed || string.IsNullOrWhiteSpace(m_Url))
                return;

            if (m_Player == null || m_Player.IsDisposed)
            {
                Play();
                return;
            }

            try
            {
                if (!m_Player.IsPlaying() && !m_Player.Resume())
                {
                    throw new InvalidOperationException("LibVLC未能恢复播放");
                }

                m_IsInitialized = true;
                StartStatusMonitor();
            }
            catch (Exception ex)
            {
                HandlePlayerFailure("恢复360°媒体失败", ex);
            }
        }

        #endregion

        #region 资源清理与辅助方法

        private void HandlePlayerFailure(string context, Exception ex)
        {
            string message = $"{context}: {ex.Message}";
            Debug.LogError(message);
            OnMediaPlayerErrorEvent?.Invoke(message);
            StopStatusMonitor();
            DisposeCurrentPlayer();
        }

        private void DisposeCurrentPlayer()
        {
            VlcMediaPlayer player = m_Player;
            m_Player = null;
            m_IsInitialized = false;

            if (player == null)
                return;

            try
            {
                player.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"释放360°播放器失败: {ex.Message}");
            }
        }

        private void CleanupResources()
        {
            StopRecovery();
            StopStatusMonitor();
            DisposeCurrentPlayer();

            if (m_Texture != null)
            {
                Destroy(m_Texture);
                m_Texture = null;
            }

            if (m_OwnsMaterial && m_Material != null)
            {
                Destroy(m_Material);
            }

            m_Material = null;
            m_OwnsMaterial = false;
            m_TempRowBuffer = null;
        }

        private static string StateToString(libvlc_state_t state)
        {
            switch (state)
            {
                case libvlc_state_t.libvlc_NothingSpecial: return "无特殊状态";
                case libvlc_state_t.libvlc_Opening: return "媒体正在打开...";
                case libvlc_state_t.libvlc_Buffering: return "媒体正在缓冲...";
                case libvlc_state_t.libvlc_Playing: return "媒体正在播放";
                case libvlc_state_t.libvlc_Paused: return "媒体暂停播放";
                case libvlc_state_t.libvlc_Stopped: return "媒体已停止播放";
                case libvlc_state_t.libvlc_Ended: return "媒体已播放完毕";
                case libvlc_state_t.libvlc_Error: return "发生错误，无法继续播放";
                default: return "状态未知";
            }
        }

        private static void CheckEditorPlaying()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException("请在播放模式下调用此方法");
            }
#endif
        }

        #endregion
    }
}
