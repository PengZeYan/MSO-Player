using System.Collections;
using UnityEngine;
using UnityEngine.Events;

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
        private int m_Width = 3840;

        [SerializeField, Min(0), Tooltip("输出分辨率高度，≤0进行自动缩放")]
        private int m_Height = 1920;

        [SerializeField, Tooltip("是否静音")]
        private bool m_Mute = false;

        [SerializeField, Tooltip("启动时自动播放")]
        private bool m_PlayOnStart = true;

        [Header("纹理调整")]
        [SerializeField, Tooltip("视频水平翻转")]
        private bool m_FlipHorizontal = false;

        [SerializeField, Tooltip("视频垂直翻转")]
        private bool m_FlipVertical = false;
        
        [SerializeField, Tooltip("纹理旋转角度")]
        private TextureRotation m_TextureRotation = TextureRotation.None;

        #endregion

        #region 枚举定义
        
        /// <summary>
        /// 纹理旋转方向
        /// </summary>
        public enum TextureRotation
        {
            /// <summary>不旋转</summary>
            None = 0,
            /// <summary>顺时针旋转90度</summary>
            CW_90 = 90,
            /// <summary>旋转180度</summary>
            CW_180 = 180,
            /// <summary>逆时针旋转90度(顺时针旋转270度)</summary>
            CCW_90 = 270
        }
        
        #endregion

        #region 私有字段

        private Texture2D m_Texture;
        private VlcMediaPlayer m_Player;
        private MeshRenderer m_MeshRenderer;
        private Material m_Material;
        private libvlc_state_t m_CurrentMediaState;

        #endregion

        #region 公共属性与事件

        /// <summary>
        /// 当媒体播放器状态变化时触发的事件
        /// </summary>
        public UnityAction<string> OnMediaPlayerStateEvent;

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
            SetUrl(m_Url, true);
        }

        /// <summary>
        /// 设置水平翻转
        /// </summary>
        /// <param name="flip">是否水平翻转</param>
        public void SetHorizontalFlip(bool flip)
        {
            if (m_FlipHorizontal != flip)
            {
                m_FlipHorizontal = flip;
                UpdateTextureScale();
            }
        }

        /// <summary>
        /// 设置垂直翻转
        /// </summary>
        /// <param name="flip">是否垂直翻转</param>
        public void SetVerticalFlip(bool flip)
        {
            if (m_FlipVertical != flip)
            {
                m_FlipVertical = flip;
                UpdateTextureScale();
            }
        }

        /// <summary>
        /// 设置纹理旋转角度
        /// </summary>
        /// <param name="rotation">旋转角度枚举</param>
        public void SetTextureRotation(TextureRotation rotation)
        {
            if (m_TextureRotation != rotation)
            {
                m_TextureRotation = rotation;
                UpdateTextureScale();
            }
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
            m_Player = new VlcMediaPlayer(m_Width, m_Height, m_Url, m_Mute);
            
            if (m_Texture == null)
            {
                CreateTexture();
            }
            
            StartCoroutine(SupervisePlayerState());
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
                m_Texture = new Texture2D(m_Width, m_Height, TextureFormat.RGB24, false, false);
                
                // 对于全景视频，需要设置适当的包裹模式
                m_Texture.wrapMode = TextureWrapMode.Repeat;
                m_Texture.filterMode = FilterMode.Bilinear;
                
                // 将纹理设置到球体材质
                if (m_Material != null)
                {
                    m_Material.mainTexture = m_Texture;
                    
                    // 根据翻转设置更新纹理缩放
                    UpdateTextureScale();
                }
            }
            else
            {
                Debug.LogWarning("无法创建纹理：宽度或高度无效");
            }
        }

        /// <summary>
        /// 更新纹理的缩放，用于处理视频翻转
        /// </summary>
        private void UpdateTextureScale()
        {
            if (m_Material != null)
            {
                // 处理水平和垂直翻转
                float scaleX = m_FlipHorizontal ? -1 : 1;
                float scaleY = m_FlipVertical ? -1 : 1;
                float offsetX = m_FlipHorizontal ? 1 : 0;
                float offsetY = m_FlipVertical ? 1 : 0;
                
                // 根据旋转角度调整纹理坐标
                switch (m_TextureRotation)
                {
                    case TextureRotation.None:
                        // 不做变化
                        m_Material.mainTextureScale = new Vector2(scaleX, scaleY);
                        m_Material.mainTextureOffset = new Vector2(offsetX, offsetY);
                        break;

                    case TextureRotation.CW_90:
                        // 顺时针旋转90度
                        m_Material.mainTextureScale = new Vector2(scaleY, -scaleX);
                        m_Material.mainTextureOffset = new Vector2(offsetY, 1 - offsetX);
                        break;

                    case TextureRotation.CW_180:
                        // 旋转180度
                        m_Material.mainTextureScale = new Vector2(-scaleX, -scaleY);
                        m_Material.mainTextureOffset = new Vector2(1 - offsetX, 1 - offsetY);
                        break;

                    case TextureRotation.CCW_90:
                        // 逆时针旋转90度
                        m_Material.mainTextureScale = new Vector2(-scaleY, scaleX);
                        m_Material.mainTextureOffset = new Vector2(1 - offsetY, offsetX);
                        break;
                }
                
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

            if (m_Player.CheckForImageUpdate(out byte[] imageData))
            {
                m_Texture.LoadRawTextureData(imageData);
                m_Texture.Apply(false);
            }
        }

        /// <summary>
        /// 监视播放器状态的协程
        /// </summary>
        private IEnumerator SupervisePlayerState()
        {
            WaitForSeconds wait = new WaitForSeconds(0.5f);
            
            while (m_Player != null)
            {
                libvlc_state_t state = m_Player.State;

                if (state != m_CurrentMediaState)
                {
                    m_CurrentMediaState = state;
                    OnMediaPlayerStateEvent?.Invoke(StateToString(state));
                }
                
                yield return wait;
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void CleanupResources()
        {
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