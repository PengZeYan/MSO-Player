using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using System;

namespace yan.libvlc
{
    /// <summary>
    /// 全景360度视频播放器组件，用于在球体上播放全景视频
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class MediaPlayer360 : MonoBehaviour
    {
        #region 序列化字段

        [SerializeField, Tooltip("媒体URL地址")]
        private string m_Url;

        [SerializeField, Min(0), Tooltip("输出分辨率宽度，≤0进行自动缩放")]
        private int m_Width = 1920;

        [SerializeField, Min(0), Tooltip("输出分辨率高度，≤0进行自动缩放")]
        private int m_Height = 960;

        [SerializeField, Tooltip("是否静音")]
        private bool m_Mute = false;

        [SerializeField, Tooltip("启动时自动播放")]
        private bool m_PlayOnStart = true;

        [SerializeField, Tooltip("是否反转Y轴（上下翻转图像）")]
        private bool m_FlipY = true;
        
        [SerializeField, Tooltip("无视频数据最大等待时间(秒)，超过此时间将自动尝试恢复播放，0表示禁用")]
        private float m_MaxNoDataWaitTime = 5.0f;
        
        [SerializeField, Tooltip("检测视频流状态的时间间隔(秒)")]
        private float m_StatusCheckInterval = 0.5f;

        #endregion

        #region 私有字段

        private Texture2D m_Texture;
        private VlcMediaPlayer m_Player;
        private MeshRenderer m_MeshRenderer;
        private Material m_Material;
        private libvlc_state_t m_CurrentMediaState;
        private byte[] m_TempRowBuffer; // 用于Y轴反转的临时缓冲区
        private bool m_IsInitialized = false;
        private int m_FailedRecoveryAttempts = 0; // 记录恢复播放失败的次数
        private const int MAX_RECOVERY_ATTEMPTS = 3; // 最大恢复尝试次数
        private WaitForSeconds m_StatusCheckWait; // 缓存WaitForSeconds对象

        #endregion

        #region 公共属性与事件

        /// <summary>
        /// 当媒体播放器状态变化时触发的事件
        /// </summary>
        public UnityAction<string> OnMediaPlayerStateEvent;

        /// <summary>
        /// 当媒体播放发生错误时触发的事件
        /// </summary>
        public UnityAction<string> OnMediaPlayerErrorEvent;
        
        /// <summary>
        /// 当播放器自动恢复播放时触发的事件
        /// </summary>
        public UnityAction OnMediaPlayerRecoveryEvent;

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
                return m_Player.IsPlaying();
            }
        }

        #endregion

        #region Unity生命周期方法

        private void Awake()
        {
            // 预先创建并缓存WaitForSeconds对象，避免每次都创建新的
            m_StatusCheckWait = new WaitForSeconds(m_StatusCheckInterval);
        }

        private void Start()
        {
            InitializeMeshRenderer();

            if (m_PlayOnStart) 
                Play();
        }

        private void Update()
        {
            UpdateTexture();
        }
        
        private void OnApplicationFocus(bool focus)
        {
            // 当应用程序重新获得焦点时检查播放状态
            if (focus && m_Player != null && m_IsInitialized)
            {
                // 检查播放器状态，可能需要恢复播放
                if (m_Player.State == libvlc_state_t.libvlc_Paused || 
                    m_Player.State == libvlc_state_t.libvlc_Stopped)
                {
                    Debug.Log("应用程序重新获得焦点，尝试恢复播放");
                    m_Player.Pause(); // 切换播放状态
                }
            }
        }
        
        private void OnApplicationPause(bool pause)
        {
            // 当应用程序暂停时，主动暂停播放，避免资源浪费
            if (pause && m_Player != null && m_Player.IsPlaying())
            {
                Debug.Log("应用程序暂停，暂停播放");
                m_Player.Pause();
            }
        }

        private void OnDestroy()
        {
            CleanupResources();
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
                Debug.LogError("媒体URL不能为空");
                return;
            }

            m_Url = url;
            m_FailedRecoveryAttempts = 0; // 重置恢复尝试计数

            if (autoPlay)
            {
                CheckEditorPlaying();

                if (m_Player == null)
                {
                    Play();
                }
                else
                {
                    m_Player.UpdateUrl(url);
                }
            }
        }

        /// <summary>
        /// 开始播放媒体
        /// </summary>
        public void Play()
        {
            CheckEditorPlaying();

            if (!gameObject.activeSelf)
            {
                Debug.LogWarning("游戏对象未激活，无法播放媒体");
                return;
            }

            if (string.IsNullOrEmpty(m_Url))
            {
                Debug.LogError("媒体URL不能为空");
                return;
            }

            if (m_Player == null)
            {
                CreatePlayer();
            }

            if (!m_Player.IsPlaying())
            {
                m_Player.Pause(); // 通过Pause方法切换播放状态
            }
        }

        /// <summary>
        /// 停止播放媒体
        /// </summary>
        public void Stop()
        {
            CheckEditorPlaying();
            m_Player?.Stop();
        }

        /// <summary>
        /// 暂停或恢复播放
        /// </summary>
        public void Pause()
        {
            CheckEditorPlaying();
            m_Player?.Pause();
        }

        /// <summary>
        /// 刷新当前播放内容
        /// </summary>
        public void Refresh()
        {
            CheckEditorPlaying();
            m_FailedRecoveryAttempts = 0; // 重置恢复尝试计数
            SetUrl(m_Url, true);
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化MeshRenderer组件
        /// </summary>
        private void InitializeMeshRenderer()
        {
            m_MeshRenderer = GetComponent<MeshRenderer>();
            
            if (m_MeshRenderer == null)
            {
                Debug.LogError("无法获取MeshRenderer组件");
                return;
            }

            // 检查是否存在共享材质
            if (m_MeshRenderer.sharedMaterial == null)
            {
                // 如果没有材质，创建一个默认的Skybox/Panoramic材质
                Debug.LogWarning("球体对象没有设置材质，将创建默认全景材质");
                
                // 查找Skybox/Panoramic着色器
                Shader panoramicShader = Shader.Find("Skybox/Panoramic");
                
                if (panoramicShader != null)
                {
                    m_Material = new Material(panoramicShader);
                    m_Material.name = "Default360Material";
                }
                else
                {
                    // 如果找不到全景着色器，使用标准着色器
                    m_Material = new Material(Shader.Find("Standard"));
                    m_Material.name = "FallbackMaterial";
                    Debug.LogWarning("未找到全景着色器，已创建标准材质。为获得最佳效果，请手动设置Skybox/Panoramic材质");
                }
                
                m_MeshRenderer.material = m_Material;
            }
            else
            {
                // 获取并保存材质实例，以便在不影响其他物体的情况下修改它
                m_Material = new Material(m_MeshRenderer.sharedMaterial);
                m_MeshRenderer.material = m_Material;
            }
        }

        /// <summary>
        /// 创建VLC播放器实例并开始监视状态
        /// </summary>
        private void CreatePlayer()
        {
            // 检查分辨率
            if (m_Width > 4096 || m_Height > 4096)
            {
                Debug.LogWarning($"全景视频分辨率过高({m_Width}x{m_Height})，可能导致性能问题，已调整为合理值");
                // 对于全景视频，设置一个合理的上限值
                if (m_Width > 4096) m_Width = 4096;
                if (m_Height > 4096) m_Height = 2048;
            }
            
            Debug.Log($"正在创建360度视频播放器，分辨率: {m_Width}x{m_Height}");
            m_Player = new VlcMediaPlayer(m_Width, m_Height, m_Url, m_Mute);
            
            if (m_Texture == null)
            {
                CreateTexture();
            }
            
            m_IsInitialized = true;
            StartCoroutine(SupervisePlayerState());
            
            // 启动视频数据监控
            if (m_MaxNoDataWaitTime > 0)
            {
                StartCoroutine(MonitorVideoDataStream());
            }
        }

        /// <summary>
        /// 创建纹理并应用到MeshRenderer的材质
        /// </summary>
        private void CreateTexture()
        {
            if ((m_Width <= 0 || m_Height <= 0) && m_Player?.VideoTrack != null)
            {
                m_Width = (int)m_Player.VideoTrack.Value.i_width;
                m_Height = (int)m_Player.VideoTrack.Value.i_height;
            }

            if (m_Width > 0 && m_Height > 0)
            {
                // 尝试使用非破坏性操作，如果已经有合适的纹理则重用它
                if (m_Texture != null && 
                    m_Texture.width == m_Width && 
                    m_Texture.height == m_Height && 
                    m_Texture.format == TextureFormat.RGB24)
                {
                    // 纹理已存在且符合要求，重用它
                    Debug.Log("重用现有纹理以减少内存分配");
                }
                else
                {
                    // 需要创建新纹理时，先释放旧的
                    if (m_Texture != null)
                    {
                        Destroy(m_Texture);
                    }
                    
                    m_Texture = new Texture2D(m_Width, m_Height, TextureFormat.RGB24, false, false);
                    
                    // 对于全景视频，需要设置适当的包裹模式
                    m_Texture.wrapMode = TextureWrapMode.Repeat;
                    m_Texture.filterMode = FilterMode.Bilinear;
                }
                
                // 将纹理设置到球体材质
                if (m_Material != null)
                {
                    m_Material.mainTexture = m_Texture;
                    
                    // 设置基本的纹理属性
                    UpdateTextureScale();
                }
            }
            else
            {
                Debug.LogWarning("无法创建纹理：宽度或高度无效");
            }
        }

        /// <summary>
        /// 更新纹理的基本设置
        /// </summary>
        private void UpdateTextureScale()
        {
            if (m_Material != null)
            {
                // 使用默认纹理设置
                m_Material.mainTextureScale = new Vector2(1, 1);
                m_Material.mainTextureOffset = new Vector2(0, 0);
                
                // 调整材质的属性以优化渲染
                if (m_Material.HasProperty("_Mapping"))
                {
                    // Skybox/Panoramic着色器中的映射模式设置为LatLong映射
                    m_Material.SetFloat("_Mapping", 1); // 1 对应 LatLong映射
                }
                
                if (m_Material.HasProperty("_Layout"))
                {
                    // 设置为全景布局
                    m_Material.SetFloat("_Layout", 0); // 0 对应 全景布局
                }
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
                    if (imageData == null || imageData.Length == 0)
                    {
                        Debug.LogWarning("接收到空的图像数据");
                        return;
                    }
                    
                    // 检查图像数据的大小是否与纹理尺寸匹配
                    int expectedSize = m_Width * m_Height * 3; // RGB24 = 3字节每像素
                    if (imageData.Length < expectedSize)
                    {
                        Debug.LogWarning($"图像数据大小不匹配：期望{expectedSize}字节，实际{imageData.Length}字节");
                        return;
                    }
                    
                    // 仅当需要时才反转Y轴
                    if (m_FlipY)
                    {
                        FlipTextureDataVertically(imageData, m_Width, m_Height);
                    }
                    
                    try
                    {
                        m_Texture.LoadRawTextureData(imageData);
                        m_Texture.Apply(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"更新纹理时发生错误: {ex.Message}");
                        // 重新创建可读的纹理
                        RecreateTexture();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"处理图像数据时发生未处理异常: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 垂直翻转纹理数据（Y轴反转）
        /// </summary>
        /// <param name="imageData">图像字节数据</param>
        /// <param name="width">图像宽度</param>
        /// <param name="height">图像高度</param>
        private void FlipTextureDataVertically(byte[] imageData, int width, int height)
        {
            int bytesPerPixel = 3; // RGB24格式为每像素3字节
            int stride = width * bytesPerPixel;
            
            // 懒加载临时缓冲区，避免重复分配内存
            if (m_TempRowBuffer == null || m_TempRowBuffer.Length < stride)
            {
                m_TempRowBuffer = new byte[stride];
            }
            
            // 优化：只处理一半的高度，提高效率
            int halfHeight = height / 2;
            
            // 避免大量小型复制操作，减少函数调用开销
            for (int y = 0; y < halfHeight; y++)
            {
                int topRowStart = y * stride;
                int bottomRowStart = (height - y - 1) * stride;
                
                // 使用高效的内存块复制
                Buffer.BlockCopy(imageData, topRowStart, m_TempRowBuffer, 0, stride);
                Buffer.BlockCopy(imageData, bottomRowStart, imageData, topRowStart, stride);
                Buffer.BlockCopy(m_TempRowBuffer, 0, imageData, bottomRowStart, stride);
            }
        }

        /// <summary>
        /// 在发生纹理错误后重新创建纹理
        /// </summary>
        private void RecreateTexture()
        {
            try
            {
                // 先销毁旧纹理
                if (m_Texture != null)
                {
                    Destroy(m_Texture);
                    m_Texture = null;
                }
                
                // 创建一个明确可读的纹理
                m_Texture = new Texture2D(m_Width, m_Height, TextureFormat.RGB24, false);
                m_Texture.wrapMode = TextureWrapMode.Repeat;
                m_Texture.filterMode = FilterMode.Bilinear;
                
                // 重新设置到材质
                if (m_Material != null)
                {
                    m_Material.mainTexture = m_Texture;
                    UpdateTextureScale();
                }
                
                Debug.Log("已重新创建纹理以解决可读性问题");
            }
            catch (Exception ex)
            {
                Debug.LogError($"重新创建纹理时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 监视播放器状态的协程
        /// </summary>
        private IEnumerator SupervisePlayerState()
        {
            while (m_Player != null)
            {
                libvlc_state_t state = m_Player.State;

                if (state != m_CurrentMediaState)
                {
                    m_CurrentMediaState = state;
                    OnMediaPlayerStateEvent?.Invoke(StateToString(state));
                    
                    // 检测错误状态并触发错误事件
                    if (state == libvlc_state_t.libvlc_Error)
                    {
                        string errorMessage = $"播放全景视频 {m_Url} 时发生错误";
                        
                        // 获取VLC的具体错误信息
                        string vlcError = m_Player.GetErrorMessage();
                        if (!string.IsNullOrEmpty(vlcError))
                        {
                            errorMessage += $": {vlcError}";
                        }
                        
                        Debug.LogError(errorMessage);
                        OnMediaPlayerErrorEvent?.Invoke(errorMessage);
                        
                        // 尝试自动恢复播放
                        if (m_FailedRecoveryAttempts < MAX_RECOVERY_ATTEMPTS)
                        {
                            Debug.Log($"尝试恢复播放 (尝试 {m_FailedRecoveryAttempts+1}/{MAX_RECOVERY_ATTEMPTS})");
                            StartCoroutine(AttemptRecovery());
                        }
                        else
                        {
                            Debug.LogWarning($"已达到最大恢复尝试次数 ({MAX_RECOVERY_ATTEMPTS})，不再自动恢复");
                        }
                    }
                    else if (state == libvlc_state_t.libvlc_Playing)
                    {
                        // 播放成功时重置恢复计数
                        m_FailedRecoveryAttempts = 0;
                    }
                }
                
                yield return m_StatusCheckWait;
            }
        }
        
        /// <summary>
        /// 监控视频数据流，检测是否长时间没有收到图像数据
        /// </summary>
        private IEnumerator MonitorVideoDataStream()
        {
            // 给播放器一些初始化时间
            yield return new WaitForSeconds(1.0f);
            
            while (m_Player != null && m_IsInitialized)
            {
                // 检查是否播放中且长时间未收到图像
                if (m_Player.IsPlaying() && m_Player.NoImageDataReceivedTime > m_MaxNoDataWaitTime)
                {
                    Debug.LogWarning($"已有 {m_Player.NoImageDataReceivedTime:F1} 秒没有接收到视频数据，尝试恢复播放");
                    
                    if (m_FailedRecoveryAttempts < MAX_RECOVERY_ATTEMPTS)
                    {
                        StartCoroutine(AttemptRecovery());
                    }
                }
                
                yield return m_StatusCheckWait;
            }
        }
        
        /// <summary>
        /// 尝试恢复播放
        /// </summary>
        private IEnumerator AttemptRecovery()
        {
            m_FailedRecoveryAttempts++;
            
            // 通知恢复事件
            OnMediaPlayerRecoveryEvent?.Invoke();
            
            // 停止当前播放
            m_Player?.Stop();
            
            // 短暂等待
            yield return new WaitForSeconds(0.5f);
            
            // 重新创建播放器
            CleanupPlayer();
            CreatePlayer();
        }
        
        /// <summary>
        /// 仅清理播放器资源，保留材质和纹理
        /// </summary>
        private void CleanupPlayer()
        {
            StopAllCoroutines();
            
            if (m_Player != null)
            {
                m_Player.Dispose();
                m_Player = null;
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void CleanupResources()
        {
            m_IsInitialized = false;
            StopAllCoroutines();
            
            if (m_Player != null)
            {
                m_Player.Dispose();
                m_Player = null;
            }
            
            if (m_Texture != null)
            {
                Destroy(m_Texture);
                m_Texture = null;
            }
            
            if (m_Material != null && m_Material != m_MeshRenderer.sharedMaterial)
            {
                Destroy(m_Material);
                m_Material = null;
            }
            
            // 释放临时缓冲区
            m_TempRowBuffer = null;
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