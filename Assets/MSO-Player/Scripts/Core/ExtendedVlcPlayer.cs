using System;
using UnityEngine;
using yan.libvlc.Core;

namespace yan.libvlc
{
    /// <summary>
    /// 扩展VLC播放器类，添加对原始libvlc API的封装，提供更多控制功能
    /// </summary>
    public class ExtendedVlcPlayer
    {
        /// <summary>
        /// 内部VLC播放器实例
        /// </summary>
        private readonly VlcMediaPlayer _player;
        
        /// <summary>
        /// 媒体播放器指针
        /// </summary>
        private IntPtr _mediaPlayerPtr;
        
        /// <summary>
        /// 媒体指针
        /// </summary>
        private IntPtr _mediaPtr;

        // 优化：缓存反射FieldInfo对象
        private static System.Reflection.FieldInfo _mediaPlayerFieldInfo;
        private static System.Reflection.FieldInfo _mediaFieldInfo;
        private static readonly object _reflectionLock = new object();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="player">VLC播放器实例</param>
        public ExtendedVlcPlayer(VlcMediaPlayer player)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            
            InitializeReflectionCache();
            
            _mediaPlayerPtr = GetMediaPlayerPtr();
            _mediaPtr = GetMediaPtr();
        }

        /// <summary>
        /// 获取媒体当前播放时间（毫秒）
        /// </summary>
        /// <returns>当前时间（毫秒）</returns>
        public long GetTime()
        {
            if (_mediaPlayerPtr == IntPtr.Zero)
            {
                _mediaPlayerPtr = GetMediaPlayerPtr();
                if (_mediaPlayerPtr == IntPtr.Zero) return 0;
            }

            try
            {
                // 获取当前播放时间
                long time = LibVLCWrapper.libvlc_media_player_get_time(_mediaPlayerPtr);
                return time >= 0 ? time : 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"获取播放时间时发生错误: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 设置媒体播放时间位置（毫秒）
        /// </summary>
        /// <param name="time">目标时间（毫秒）</param>
        /// <returns>操作是否成功</returns>
        public bool SetTime(long time)
        {
            if (_mediaPlayerPtr == IntPtr.Zero)
            {
                _mediaPlayerPtr = GetMediaPlayerPtr();
                if (_mediaPlayerPtr == IntPtr.Zero) return false;
            }

            try
            {
                // 设置当前播放时间
                int result = LibVLCWrapper.libvlc_media_player_set_time(_mediaPlayerPtr, time);
                return result == 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"设置播放时间时发生错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取媒体总时长（毫秒）
        /// </summary>
        /// <returns>总时长（毫秒）</returns>
        public long GetLength()
        {
            if (_mediaPlayerPtr == IntPtr.Zero)
            {
                _mediaPlayerPtr = GetMediaPlayerPtr();
                if (_mediaPlayerPtr == IntPtr.Zero) return 0;
            }

            try
            {
                // 首先尝试使用媒体播放器获取长度
                long duration = LibVLCWrapper.libvlc_media_player_get_length(_mediaPlayerPtr);
                
                if (duration <= 0 && _mediaPtr != IntPtr.Zero)
                {
                    duration = LibVLCWrapper.libvlc_media_get_duration(_mediaPtr);
                }
                
                return duration >= 0 ? duration : 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"获取媒体时长时发生错误: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 获取当前播放位置（0-1）
        /// </summary>
        /// <returns>当前位置（0-1）</returns>
        public float GetPosition()
        {
            if (_mediaPlayerPtr == IntPtr.Zero)
            {
                _mediaPlayerPtr = GetMediaPlayerPtr();
                if (_mediaPlayerPtr == IntPtr.Zero) return 0;
            }

            try
            {
                // 获取当前播放位置
                float position = LibVLCWrapper.libvlc_media_player_get_position(_mediaPlayerPtr);
                return position >= 0 ? Mathf.Clamp01(position) : 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"获取播放位置时发生错误: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 设置播放位置（0-1）
        /// </summary>
        /// <param name="position">目标位置（0-1）</param>
        /// <returns>操作是否成功</returns>
        public bool SetPosition(float position)
        {
            if (_mediaPlayerPtr == IntPtr.Zero)
            {
                _mediaPlayerPtr = GetMediaPlayerPtr();
                if (_mediaPlayerPtr == IntPtr.Zero) return false;
            }

            try
            {
                position = Mathf.Clamp01(position);
                
                // 设置播放位置
                int result = LibVLCWrapper.libvlc_media_player_set_position(_mediaPlayerPtr, position);
                return result == 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"设置播放位置时发生错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取当前音量（0-100）
        /// </summary>
        /// <returns>当前音量（0-100）</returns>
        public int GetVolume()
        {
            if (_mediaPlayerPtr == IntPtr.Zero)
            {
                _mediaPlayerPtr = GetMediaPlayerPtr();
                if (_mediaPlayerPtr == IntPtr.Zero) return 0;
            }

            try
            {
                // 获取当前音量
                int volume = LibVLCWrapper.libvlc_audio_get_volume(_mediaPlayerPtr);
                return volume >= 0 ? Mathf.Clamp(volume, 0, 100) : 100;
            }
            catch (Exception ex)
            {
                Debug.LogError($"获取音量时发生错误: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 设置音量（0-100）
        /// </summary>
        /// <param name="volume">目标音量（0-100）</param>
        /// <returns>操作是否成功</returns>
        public bool SetVolume(int volume)
        {
            if (_mediaPlayerPtr == IntPtr.Zero)
            {
                _mediaPlayerPtr = GetMediaPlayerPtr();
                if (_mediaPlayerPtr == IntPtr.Zero) return false;
            }

            try
            {
                volume = Mathf.Clamp(volume, 0, 100);
                
                int result = LibVLCWrapper.libvlc_audio_set_volume(_mediaPlayerPtr, volume);
                return result == 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"设置音量时发生错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 静音或取消静音
        /// </summary>
        /// <param name="mute">是否静音</param>
        /// <returns>操作是否成功</returns>
        public bool SetMute(bool mute)
        {
            if (_mediaPlayerPtr == IntPtr.Zero)
            {
                _mediaPlayerPtr = GetMediaPlayerPtr();
                if (_mediaPlayerPtr == IntPtr.Zero) return false;
            }

            try
            {
                // 设置静音状态
                int result = LibVLCWrapper.libvlc_audio_set_mute(_mediaPlayerPtr, mute ? 1 : 0);
                return result == 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"设置静音状态时发生错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取静音状态
        /// </summary>
        /// <returns>是否静音</returns>
        public bool IsMuted()
        {
            if (_mediaPlayerPtr == IntPtr.Zero)
            {
                _mediaPlayerPtr = GetMediaPlayerPtr();
                if (_mediaPlayerPtr == IntPtr.Zero) return false;
            }

            try
            {
                // 获取静音状态
                int mute = LibVLCWrapper.libvlc_audio_get_mute(_mediaPlayerPtr);
                return mute == 1;
            }
            catch (Exception ex)
            {
                Debug.LogError($"获取静音状态时发生错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查是否为直播流
        /// </summary>
        /// <returns>是否为直播流</returns>
        public bool IsLiveStream()
        {
            // 通常直播流的持续时间为0或非常大的值
            long duration = GetLength();
            return duration <= 0 || duration >= 86400000; // 24小时以上视为直播
        }

        /// <summary>
        /// 检查是否可跳转
        /// </summary>
        /// <returns>是否可跳转</returns>
        public bool IsSeekable()
        {
            if (_mediaPlayerPtr == IntPtr.Zero)
            {
                _mediaPlayerPtr = GetMediaPlayerPtr();
                if (_mediaPlayerPtr == IntPtr.Zero) return false;
            }

            try
            {
                // 检查是否可跳转
                return LibVLCWrapper.libvlc_media_player_is_seekable(_mediaPlayerPtr);
            }
            catch (Exception ex)
            {
                Debug.LogError($"检查是否可跳转时发生错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 刷新内部指针
        /// </summary>
        public void RefreshPointers()
        {
            _mediaPlayerPtr = GetMediaPlayerPtr();
            _mediaPtr = GetMediaPtr();
        }

        /// <summary>
        /// 优化：初始化反射缓存
        /// </summary>
        private static void InitializeReflectionCache()
        {
            if (_mediaPlayerFieldInfo != null && _mediaFieldInfo != null)
                return;

            lock (_reflectionLock)
            {
                // 双重检查锁定
                if (_mediaPlayerFieldInfo != null && _mediaFieldInfo != null)
                    return;

                try
                {
                    var playerType = typeof(VlcMediaPlayer);
                    _mediaPlayerFieldInfo = playerType.GetField("_mediaPlayer",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    _mediaFieldInfo = playerType.GetField("_media",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"初始化反射缓存失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 获取媒体播放器指针（优化：使用缓存的FieldInfo）
        /// </summary>
        private IntPtr GetMediaPlayerPtr()
        {
            if (_player == null) return IntPtr.Zero;

            try
            {
                if (_mediaPlayerFieldInfo == null)
                    InitializeReflectionCache();

                var value = _mediaPlayerFieldInfo?.GetValue(_player);
                return value is IntPtr ptr ? ptr : IntPtr.Zero;
            }
            catch (Exception ex)
            {
                Debug.LogError($"获取媒体播放器指针时发生错误: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// 获取媒体指针
        /// </summary>
        private IntPtr GetMediaPtr()
        {
            if (_player == null) return IntPtr.Zero;

            try
            {
                if (_mediaFieldInfo == null)
                    InitializeReflectionCache();

                var value = _mediaFieldInfo?.GetValue(_player);
                return value is IntPtr ptr ? ptr : IntPtr.Zero;
            }
            catch (Exception ex)
            {
                Debug.LogError($"获取媒体指针时发生错误: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// 获取视频分辨率信息
        /// </summary>
        /// <param name="width">输出参数：宽度</param>
        /// <param name="height">输出参数：高度</param>
        /// <returns>是否成功获取分辨率信息</returns>
        public bool GetVideoResolution(out uint width, out uint height)
        {
            width = 0;
            height = 0;

            if (_player == null)
                return false;
            
            try
            {
                var videoTrack = _player.VideoTrack;
                if (videoTrack.HasValue)
                {
                    width = videoTrack.Value.i_width;
                    height = videoTrack.Value.i_height;
                    return width > 0 && height > 0;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"获取视频分辨率时发生错误: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 获取视频分辨率描述
        /// </summary>
        /// <returns>分辨率描述（例如：1080p, 720p, 480p等）</returns>
        public string GetResolutionDescription()
        {
            if (!GetVideoResolution(out uint width, out uint height))
                return "";
            
            if (height >= 2160)
                return "4K";
            else if (height >= 1440)
                return "2K";
            else if (height >= 1080)
                return "1080p";
            else if (height >= 720)
                return "720p";
            else if (height >= 480)
                return "480p";
            else if (height >= 360)
                return "360p";
            else if (height >= 240)
                return "240p";
            else
                return $"{width}x{height}";
        }
    }
} 