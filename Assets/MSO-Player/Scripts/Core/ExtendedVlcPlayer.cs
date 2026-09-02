using System;
using UnityEngine;
using yan.libvlc.Core;

namespace yan.libvlc
{
    /// <summary>
    /// 为播放器控制 UI 提供时间、进度、音量和媒体信息。
    /// 保留原有公共 API，但不再通过反射缓存易失效的原生指针。
    /// </summary>
    public class ExtendedVlcPlayer
    {
        private readonly VlcMediaPlayer _player;

        public ExtendedVlcPlayer(VlcMediaPlayer player)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public long GetTime()
        {
            try
            {
                return _player.GetTime();
            }
            catch (Exception ex)
            {
                Debug.LogError($"获取播放时间时发生错误: {ex.Message}");
                return 0;
            }
        }

        public bool SetTime(long time)
        {
            try
            {
                return _player.SetTime(time);
            }
            catch (Exception ex)
            {
                Debug.LogError($"设置播放时间时发生错误: {ex.Message}");
                return false;
            }
        }

        public long GetLength()
        {
            try
            {
                return _player.GetLength();
            }
            catch (Exception ex)
            {
                Debug.LogError($"获取媒体时长时发生错误: {ex.Message}");
                return 0;
            }
        }

        public float GetPosition()
        {
            try
            {
                return _player.GetPosition();
            }
            catch (Exception ex)
            {
                Debug.LogError($"获取播放位置时发生错误: {ex.Message}");
                return 0f;
            }
        }

        public bool SetPosition(float position)
        {
            try
            {
                return _player.SetPosition(position);
            }
            catch (Exception ex)
            {
                Debug.LogError($"设置播放位置时发生错误: {ex.Message}");
                return false;
            }
        }

        public int GetVolume()
        {
            try
            {
                int volume = _player.GetVolume();
                return volume >= 0 ? Mathf.Clamp(volume, 0, 100) : 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"获取音量时发生错误: {ex.Message}");
                return 0;
            }
        }

        public bool SetVolume(int volume)
        {
            try
            {
                return _player.SetVolume(Mathf.Clamp(volume, 0, 100));
            }
            catch (Exception ex)
            {
                Debug.LogError($"设置音量时发生错误: {ex.Message}");
                return false;
            }
        }

        public bool SetMute(bool mute)
        {
            try
            {
                return _player.SetMute(mute);
            }
            catch (Exception ex)
            {
                Debug.LogError($"设置静音状态时发生错误: {ex.Message}");
                return false;
            }
        }

        public bool IsMuted()
        {
            try
            {
                return _player.IsMuted();
            }
            catch (Exception ex)
            {
                Debug.LogError($"获取静音状态时发生错误: {ex.Message}");
                return false;
            }
        }

        public bool IsLiveStream()
        {
            long duration = GetLength();
            return duration <= 0 || duration >= 86400000;
        }

        public bool IsSeekable()
        {
            try
            {
                return _player.IsSeekable();
            }
            catch (Exception ex)
            {
                Debug.LogError($"检查媒体是否可跳转时发生错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 为兼容旧调用保留。核心 API 不再缓存原生指针，因此无需刷新。
        /// </summary>
        public void RefreshPointers()
        {
        }

        public bool GetVideoResolution(out uint width, out uint height)
        {
            width = 0;
            height = 0;

            try
            {
                libvlc_video_track_t? videoTrack = _player.VideoTrack;
                if (!videoTrack.HasValue)
                    return false;

                width = videoTrack.Value.i_width;
                height = videoTrack.Value.i_height;
                return width > 0 && height > 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"获取视频分辨率时发生错误: {ex.Message}");
                return false;
            }
        }

        public string GetResolutionDescription()
        {
            if (!GetVideoResolution(out uint width, out uint height))
                return string.Empty;

            if (height >= 2160) return "4K";
            if (height >= 1440) return "2K";
            if (height >= 1080) return "1080p";
            if (height >= 720) return "720p";
            if (height >= 480) return "480p";
            if (height >= 360) return "360p";
            if (height >= 240) return "240p";
            return $"{width}x{height}";
        }
    }
}
