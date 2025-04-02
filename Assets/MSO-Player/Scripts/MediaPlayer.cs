using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static yan.libvlc.VlcMediaPlayer;

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
        private bool m_PlayOnStart = false;

        #endregion

        #region 私有字段

        private Texture2D m_Texture;
        private VlcMediaPlayer m_Player;
        private RawImage m_RawImage;
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
            InitializeRawImage();

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
            try
            {
                if (m_Player != null)
                {
                    m_Player.Stop();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"停止播放时发生错误: {ex.Message}");
            }
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

        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化RawImage组件
        /// </summary>
        private void InitializeRawImage()
        {
            m_RawImage = GetComponent<RawImage>();
            
            if (m_RawImage == null)
            {
                Debug.LogError("无法获取RawImage组件");
                return;
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
        /// 创建纹理并应用到RawImage
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
                m_RawImage.texture = m_Texture;

                if (m_AutoscaleRawImage)
                {
                    RectTransform rect = m_RawImage.rectTransform;
                    float ratio = m_Height / (float)m_Width;
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rect.rect.width * ratio);
                }
            }
            else
            {
                Debug.LogWarning("无法创建纹理：宽度或高度无效");
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
            try
            {
                StopAllCoroutines();
                
                if (m_Player != null)
                {
                    try
                    {
                        m_Player.Stop();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"停止播放时发生错误: {ex.Message}");
                    }
                    
                    try
                    {
                        m_Player.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"释放播放器资源时发生错误: {ex.Message}");
                    }
                    
                    m_Player = null;
                }
                
                if (m_Texture != null)
                {
                    try
                    {
                        Destroy(m_Texture);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"销毁纹理时发生错误: {ex.Message}");
                    }
                    
                    m_Texture = null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"清理资源时发生错误: {ex.Message}");
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
