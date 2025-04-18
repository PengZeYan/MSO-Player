using UnityEngine;

namespace yan.libvlc.Platform
{
    /// <summary>
    /// 平台管理器：负责处理跨平台特性检测和专有设置
    /// </summary>
    public static class PlatformManager
    {
        #region 平台类型检测

        /// <summary>
        /// 检查当前运行的平台是否为Android
        /// </summary>
        public static bool IsAndroid => Application.platform == RuntimePlatform.Android;

        /// <summary>
        /// 检查当前运行的平台是否为iOS
        /// </summary>
        public static bool IsIOS => Application.platform == RuntimePlatform.IPhonePlayer;

        /// <summary>
        /// 检查当前运行的平台是否为Windows
        /// </summary>
        public static bool IsWindows => Application.platform == RuntimePlatform.WindowsPlayer || 
                                        Application.platform == RuntimePlatform.WindowsEditor;

        /// <summary>
        /// 检查当前运行的平台是否为MacOS
        /// </summary>
        public static bool IsMacOS => Application.platform == RuntimePlatform.OSXPlayer || 
                                      Application.platform == RuntimePlatform.OSXEditor;

        #endregion

        #region 设备特性检测

        /// <summary>
        /// 检测当前设备是否为低性能设备
        /// </summary>
        public static bool IsLowEndDevice
        {
            get
            {
                // 根据系统内存和处理器核心数判断
                bool lowMemory = SystemInfo.systemMemorySize < 2048; // 低于2GB RAM
                bool lowCPU = SystemInfo.processorCount <= 2; // 双核或更少
                
                // Android设备额外检查
                if (IsAndroid)
                {
                    // 检查GPU渲染能力
                    bool lowGPU = !SystemInfo.graphicsDeviceName.ToLower().Contains("adreno") &&
                                  !SystemInfo.graphicsDeviceName.ToLower().Contains("mali-g");
                    
                    return lowMemory || (lowCPU && lowGPU);
                }
                
                return lowMemory && lowCPU;
            }
        }

        /// <summary>
        /// 检测当前设备是否支持硬件解码
        /// </summary>
        public static bool SupportsHardwareDecoding
        {
            get
            {
                if (IsAndroid)
                {
                    // Android 5.0 (API 21)及以上支持MediaCodec硬件解码
                    return int.Parse(SystemInfo.operatingSystem.Split(' ')[2].Split('.')[0]) >= 5;
                }
                
                // 其他平台默认支持
                return true;
            }
        }

        #endregion

        #region 平台特定配置

        /// <summary>
        /// 为当前平台获取最佳的VLC启动参数
        /// </summary>
        /// <returns>针对当前平台优化的VLC参数数组</returns>
        public static string[] GetOptimalVlcParameters()
        {
            if (IsAndroid)
            {
                return GetAndroidOptimalParameters();
            }
            else if (IsIOS)
            {
                return GetIOSOptimalParameters();
            }
            else if (IsWindows)
            {
                return GetWindowsOptimalParameters();
            }
            else if (IsMacOS)
            {
                return GetMacOSOptimalParameters();
            }
            
            // 默认参数
            return new string[] 
            {
                "--ignore-config",
                "--no-video-title-show",
                "--no-osd"
            };
        }

        /// <summary>
        /// 获取针对Android平台优化的VLC参数
        /// </summary>
        private static string[] GetAndroidOptimalParameters()
        {
            System.Collections.Generic.List<string> parameters = new System.Collections.Generic.List<string>
            {
                "--no-xlib",
                "--no-video-title-show",
                "--no-stats",
                "--no-snapshot-preview",
                "--network-caching=2000",
                "--android-display-chroma=RV16"
            };
            
            // 根据设备性能调整参数
            if (IsLowEndDevice)
            {
                parameters.Add("--avcodec-fast");
                parameters.Add("--avcodec-skiploopfilter=all");
                parameters.Add("--clock-jitter=0");
                parameters.Add("--clock-synchro=0");
            }
            
            // 硬件加速选项
            if (SupportsHardwareDecoding)
            {
                parameters.Add("--codec=mediacodec,all");
            }
            
            return parameters.ToArray();
        }

        /// <summary>
        /// 获取针对iOS平台优化的VLC参数
        /// </summary>
        private static string[] GetIOSOptimalParameters()
        {
            return new string[]
            {
                "--no-video-title-show",
                "--no-stats",
                "--no-osd",
                "--network-caching=1500"
            };
        }

        /// <summary>
        /// 获取针对Windows平台优化的VLC参数
        /// </summary>
        private static string[] GetWindowsOptimalParameters()
        {
            return new string[]
            {
                "--ignore-config",
                "--no-video-title-show",
                "--no-osd",
                "--network-caching=2000",
                "--direct3d11-filters=true"
            };
        }

        /// <summary>
        /// 获取针对MacOS平台优化的VLC参数
        /// </summary>
        private static string[] GetMacOSOptimalParameters()
        {
            return new string[]
            {
                "--ignore-config",
                "--no-video-title-show",
                "--no-osd",
                "--network-caching=2000"
            };
        }

        #endregion
    }
} 