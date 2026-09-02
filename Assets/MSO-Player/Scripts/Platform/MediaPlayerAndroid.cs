using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using yan.libvlc.Core;
using yan.libvlc.Platform;

namespace yan.libvlc
{
    /// <summary>
    /// Android平台特定的媒体播放器组件
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class MediaPlayerAndroid : MediaPlayer
    {
        #region 序列化字段

        [SerializeField, Tooltip("是否在低内存设备上降低分辨率")]
        private bool m_ReduceResolutionOnLowMemory = true;

        [SerializeField, Tooltip("是否使用硬件解码（通常更快但某些设备可能不稳定）")]
        private bool m_UseHardwareAcceleration = true;

        [SerializeField, Min(0), Tooltip("网络缓冲时间（毫秒）")]
        private int m_NetworkCachingTime = 3000;

        #endregion

        #region 私有字段

        private bool m_IsAndroid;
        private bool m_HasReportedMemoryWarning = false;
        private Coroutine m_LowMemoryRestartCoroutine;

        #endregion

        #region Unity生命周期方法

        protected override void OnAwake()
        {
            base.OnAwake();
            // 检查是否在Android平台上运行
            m_IsAndroid = PlatformManager.IsAndroid;
            
            if (!m_IsAndroid)
            {
                Debug.LogWarning("MediaPlayerAndroid组件在非Android平台上运行，部分功能可能不可用");
            }
            
            // 添加低内存警告监听
            Application.lowMemory += OnLowMemory;
        }

        protected override void OnDestroy()
        {
            // 移除低内存警告监听
            Application.lowMemory -= OnLowMemory;
            StopLowMemoryRestart();
            base.OnDestroy();
        }

        protected override void OnDisable()
        {
            StopLowMemoryRestart();
            base.OnDisable();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 在创建VLC播放器前应用Android特定设置
        /// </summary>
        /// <param name="url">视频URL</param>
        /// <param name="width">视频宽度</param>
        /// <param name="height">视频高度</param>
        /// <param name="mute">是否静音</param>
        /// <returns>包含Android特定参数的VLC播放器实例</returns>
        public VlcMediaPlayer CreateAndroidPlayer(string url, int width, int height, bool mute)
        {
            // 创建VLC参数
            string[] androidParams = GenerateAndroidParameters();
            
            Debug.Log($"为Android创建VLC播放器，分辨率: {width}x{height}, 参数: {string.Join(", ", androidParams)}");
            
            // 创建带有特定参数的VLC播放器
            return new VlcMediaPlayer(width, height, url, mute, androidParams);
        }

        /// <summary>
        /// 设置Android平台特定的网络缓冲时间
        /// </summary>
        /// <param name="milliseconds">缓冲时间（毫秒）</param>
        public void SetNetworkCaching(int milliseconds)
        {
            if (milliseconds < 0)
            {
                Debug.LogError("网络缓冲时间不能为负值");
                return;
            }
            
            m_NetworkCachingTime = milliseconds;
            
            // 这是LibVLC实例级参数，已有核心实例必须重建后才能生效。
            if (CorePlayer != null)
            {
                RecreatePlayer();
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 生成Android特定的VLC参数
        /// </summary>
        /// <returns>VLC启动参数数组</returns>
        private string[] GenerateAndroidParameters()
        {
            // 创建基本参数列表
            System.Collections.Generic.List<string> parameters = new System.Collections.Generic.List<string>
            {
                "--no-xlib",                                // 禁用X11支持
                "--no-video-title-show",                    // 不显示视频标题
                "--no-stats",                               // 禁用统计信息
                "--no-snapshot-preview",                    // 禁用快照预览
                "--android-display-chroma=RV24",            // 默认使用24位RGB格式与Unity兼容
                "--clock-jitter=0",                         // 减少时钟抖动
                "--avcodec-threads=4"                       // 多线程解码
            };
            
            parameters.Add($"--network-caching={m_NetworkCachingTime}");

            // 根据设备内存和性能添加特定参数
            if (m_ReduceResolutionOnLowMemory && PlatformManager.IsLowEndDevice)
            {
                parameters.Add("--avcodec-fast");           // 快速解码模式
                parameters.Add("--avcodec-skiploopfilter=all"); // 跳过环路滤波以提高性能
                parameters.Add("--sout-ffmpeg-strict=-2");  // 更宽松的格式兼容性
                parameters.Add("--clock-synchro=0");        // 禁用时钟同步
                parameters.Add("--file-caching=300");       // 降低文件缓存
            }
            else
            {
                // 中高端设备使用更优参数
                parameters.Add("--file-caching=300");       // 降低文件缓存
                parameters.Add("--live-caching=500");       // 直播流低延迟
            }

            // 硬件加速选项
            if (m_UseHardwareAcceleration && PlatformManager.SupportsHardwareDecoding)
            {
                // 保留内存视频回调所需的解码路径，不强制MediaCodec直接渲染到Surface。
                parameters.Add("--codec=mediacodec_ndk,mediacodec,all");   // 优先使用NDK版本
                parameters.Add("--mediacodec-audio");       // 音频也使用硬件加速
                Debug.Log("启用Android MediaCodec硬件解码（低延迟模式）");
            }
            else
            {
                parameters.Add("--codec=all");              // 使用所有可用解码器但不强制硬件加速
                Debug.Log("使用软件解码器");
            }

            // 特定设备的优化
            string deviceModel = SystemInfo.deviceModel.ToLower();
            if (deviceModel.Contains("samsung"))
            {
                // 三星设备特定优化
                parameters.Add("--live-caching=800");
            }
            else if (deviceModel.Contains("xiaomi") || deviceModel.Contains("redmi"))
            {
                // 小米设备特定优化
                parameters.Add("--live-caching=800");
                parameters.Add("--audio-time-stretch");     // 音频时间拉伸，减少卡顿
            }
            else if (deviceModel.Contains("huawei") || deviceModel.Contains("honor"))
            {
                // 华为设备优化
                parameters.Add("--live-caching=1000");
            }

            // 添加设备信息日志
            Debug.Log($"Android设备信息: 型号={SystemInfo.deviceModel}, 内存={SystemInfo.systemMemorySize}MB, 处理器={SystemInfo.processorCount}核, 图形API={SystemInfo.graphicsDeviceType}");

            return parameters.ToArray();
        }

        /// <summary>
        /// 处理低内存警告
        /// </summary>
        private void OnLowMemory()
        {
            if (!m_HasReportedMemoryWarning)
            {
                Debug.LogWarning("Android设备内存不足警告 - 考虑降低视频质量或关闭其他应用");
                m_HasReportedMemoryWarning = true;
            }
            
            // 低内存时重启解码器，释放LibVLC内部缓存和解码队列。
            // 输出分辨率不会在这里隐式变化，避免纹理与回调缓冲区尺寸失配。
            if (m_ReduceResolutionOnLowMemory && IsPlaying)
            {
                Stop();

                if (m_LowMemoryRestartCoroutine == null)
                {
                    // 在恢复播放前给系统一些时间清理内存
                    m_LowMemoryRestartCoroutine = StartCoroutine(RestartPlaybackAfterDelay(0.5f));
                }
            }
        }

        /// <summary>
        /// 延迟后重新开始播放
        /// </summary>
        private IEnumerator RestartPlaybackAfterDelay(float delayInSeconds)
        {
            yield return new WaitForSeconds(delayInSeconds);
            m_LowMemoryRestartCoroutine = null;
            RecreatePlayer(true);
        }

        private void StopLowMemoryRestart()
        {
            if (m_LowMemoryRestartCoroutine == null)
                return;

            StopCoroutine(m_LowMemoryRestartCoroutine);
            m_LowMemoryRestartCoroutine = null;
        }

        #endregion
    }
}
