using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using System.Collections.Generic;

namespace yan.libvlc
{
    /// <summary>
    /// LibVLC播放器核心类，负责与libvlc库的底层交互
    /// </summary>
    public class VlcMediaPlayer : IDisposable
    {
        #region 私有字段

        private IntPtr _libvlc;
        private IntPtr _media;
        private IntPtr _mediaPlayer;
        private IntPtr _imageIntPtr;
        
        // 使用静态委托替代实例委托
        private static LockCB _lockCallback;
        private static UnlockCB _unlockCallback;
        private static DisplayCB _displayCallback;
        private GCHandle _gcHandle;

        private byte[] _currentImage;
        private bool _update = false;
        private bool _mute = true;
        private int _width = 480;
        private int _height = 256;
        private int _channels = 3;
        
        // 用于静态回调方法访问实例的静态字典
        private static Dictionary<IntPtr, VlcMediaPlayer> _playerInstances = new Dictionary<IntPtr, VlcMediaPlayer>();
        
        private const string DEFAULT_ARGS = "--ignore-config;--no-xlib;--no-video-title-show;--no-osd";
        private libvlc_video_track_t? _videoTrack = null;
        private IntPtr _trackToRelease;
        private int _tracks;
        
        private volatile bool _cancel = false;
        private bool _isRunning = false;

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取媒体播放器当前状态
        /// </summary>
        public libvlc_state_t State
        {
            get
            {
                if (_mediaPlayer != IntPtr.Zero)
                    return LibVLCWrapper.libvlc_media_player_get_state(_mediaPlayer);
                return libvlc_state_t.libvlc_Opening;
            }
        }

        /// <summary>
        /// 获取当前视频轨道信息
        /// </summary>
        public libvlc_video_track_t? VideoTrack => _videoTrack;

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建一个新的VLC媒体播放器实例
        /// </summary>
        /// <param name="width">视频显示宽度</param>
        /// <param name="height">视频显示高度</param>
        /// <param name="mediaUrl">媒体URL地址</param>
        /// <param name="mute">是否静音</param>
        public VlcMediaPlayer(int width, int height, string mediaUrl, bool mute = true)
        {
            _width = width;
            _height = height;
            _mute = mute;
            _gcHandle = GCHandle.Alloc(this);

            InitializeLibVLC(mediaUrl);
            SetupCallbacks();
            StartPlayback();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 检查自上次检查以来图像是否已更新，并输出图像字节
        /// </summary>
        /// <param name="currentImage">输出的图像字节数组</param>
        /// <returns>是否发生了更新</returns>
        public bool CheckForImageUpdate(out byte[] currentImage)
        {
            currentImage = null;
            if (_update)
            {
                currentImage = _currentImage;
                _update = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 暂停或恢复播放
        /// </summary>
        public void Pause()
        {
            if (IsPlaying())
            {
                LibVLCWrapper.libvlc_media_player_set_pause(_mediaPlayer, 1);
            }
            else
            {
                LibVLCWrapper.libvlc_media_player_play(_mediaPlayer);
            }
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        public void Stop()
        {
            if (_mediaPlayer != IntPtr.Zero)
            {
                LibVLCWrapper.libvlc_media_player_stop(_mediaPlayer);
            }
        }

        /// <summary>
        /// 更新播放地址
        /// </summary>
        /// <param name="newUrl">新的媒体URL</param>
        public void UpdateUrl(string newUrl)
        {
            if (string.IsNullOrEmpty(newUrl) || _libvlc == IntPtr.Zero)
            {
                Debug.LogError("无效的URL或LibVLC实例未初始化");
                return;
            }

            Stop();

            IntPtr newMedia = LibVLCWrapper.libvlc_media_new_location(_libvlc, newUrl);
            if (newMedia == IntPtr.Zero)
            {
                Debug.LogError("无法创建新的媒体对象");
                return;
            }

            LibVLCWrapper.libvlc_media_player_set_media(_mediaPlayer, newMedia);
            LibVLCWrapper.libvlc_media_release(newMedia);
            LibVLCWrapper.libvlc_media_player_play(_mediaPlayer);
        }

        /// <summary>
        /// 检查是否正在播放
        /// </summary>
        /// <returns>如果正在播放则返回true，否则返回false</returns>
        public bool IsPlaying()
        {
            return _mediaPlayer != IntPtr.Zero && 
                   LibVLCWrapper.libvlc_media_player_is_playing(_mediaPlayer);
        }

        /// <summary>
        /// 释放所有资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                // 标记为取消
                _cancel = true;
                _isRunning = false;

                // 确保停止播放
                try
                {
                    if (_mediaPlayer != IntPtr.Zero)
                    {
                        LibVLCWrapper.libvlc_media_player_stop(_mediaPlayer);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"停止播放时发生错误: {ex.Message}");
                }

                // 释放所有资源
                ReleaseResources();
            }
            catch (Exception ex)
            {
                Debug.LogError($"释放VLC播放器时发生错误: {ex.Message}");
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化LibVLC实例并设置媒体
        /// </summary>
        private void InitializeLibVLC(string mediaUrl)
        {
            string argStrings = DEFAULT_ARGS;
            if (_mute)
            {
                argStrings += ";--no-audio";
            }

            string[] args = argStrings.Split(';');
            _libvlc = LibVLCWrapper.libvlc_new(args.Length, args);

            if (_libvlc == IntPtr.Zero)
            {
                Debug.LogError("加载新的libvlc实例失败");
                return;
            }

            _media = LibVLCWrapper.libvlc_media_new_location(_libvlc, mediaUrl);

            if (_media == IntPtr.Zero)
            {
                Debug.LogError("创建媒体失败，请检查URL是否正确");
                return;
            }

            _mediaPlayer = LibVLCWrapper.libvlc_media_player_new(_libvlc);
            LibVLCWrapper.libvlc_media_player_set_media(_mediaPlayer, _media);
        }

        /// <summary>
        /// 设置视频回调函数
        /// </summary>
        private void SetupCallbacks()
        {
            // 初始化静态委托（如果尚未初始化）
            if (_lockCallback == null)
            {
                _lockCallback = OnLockStatic;
                _unlockCallback = OnUnlockStatic;
                _displayCallback = OnDisplayStatic;
            }

            // 将实例添加到静态字典
            IntPtr instancePtr = GCHandle.ToIntPtr(_gcHandle);
            _playerInstances[instancePtr] = this;

            LibVLCWrapper.libvlc_video_set_callbacks(
                _mediaPlayer, 
                _lockCallback, 
                _unlockCallback, 
                _displayCallback, 
                instancePtr
            );

            LibVLCWrapper.libvlc_video_set_format(
                _mediaPlayer, 
                "RV24", 
                (uint)_width, 
                (uint)_height, 
                (uint)_width * (uint)_channels
            );
        }

        /// <summary>
        /// 开始播放并启动轨道读取线程
        /// </summary>
        private void StartPlayback()
        {
            LibVLCWrapper.libvlc_media_player_play(_mediaPlayer);
            _isRunning = true;
            
            Thread trackReaderThread = new Thread(TrackReaderThread);
            trackReaderThread.IsBackground = true;
            trackReaderThread.Start();
        }

        /// <summary>
        /// 轨道读取线程
        /// </summary>
        private void TrackReaderThread()
        {
            const int MAX_TRACK_ATTEMPTS = 30;
            int trackGetAttempts = 0;
            
            while (_isRunning && trackGetAttempts < MAX_TRACK_ATTEMPTS && !_cancel)
            {
                libvlc_video_track_t? track = GetVideoTrack();

                if (track.HasValue)
                {
                    _videoTrack = track;

                    if (_width <= 0 || _height <= 0)
                    {
                        _width = (int)_videoTrack.Value.i_width;
                        _height = (int)_videoTrack.Value.i_height;
                        LibVLCWrapper.libvlc_video_set_format(
                            _mediaPlayer, 
                            "RV24", 
                            _videoTrack.Value.i_width, 
                            _videoTrack.Value.i_height, 
                            (uint)_width * (uint)_channels
                        );
                    }
                    break;
                }

                trackGetAttempts++;
                Thread.Sleep(500);
            }

            if (trackGetAttempts >= MAX_TRACK_ATTEMPTS)
            {
                Debug.LogError("已超过最大尝试获取视频轨道次数，打开失败");
            }
        }

        /// <summary>
        /// 获取视频轨道信息
        /// </summary>
        private libvlc_video_track_t? GetVideoTrack()
        {
            libvlc_video_track_t? videoTrack = null;
            IntPtr tracksPtr;
            int tracks = LibVLCWrapper.libvlc_media_tracks_get(_media, out tracksPtr);

            _tracks = tracks;
            _trackToRelease = tracksPtr;

            for (int i = 0; i < tracks; i++)
            {
                IntPtr trackPtr = Marshal.ReadIntPtr(tracksPtr, i * IntPtr.Size);
                libvlc_media_track_t track = Marshal.PtrToStructure<libvlc_media_track_t>(trackPtr);

                if (track.i_type == libvlc_track_type_t.libvlc_track_video)
                {
                    videoTrack = Marshal.PtrToStructure<libvlc_video_track_t>(track.media);
                    break;
                }
            }

            return videoTrack;
        }

        /// <summary>
        /// 释放所有分配的资源
        /// </summary>
        private void ReleaseResources()
        {
            try
            {
                // 从静态字典中移除实例
                if (_gcHandle.IsAllocated)
                {
                    IntPtr instancePtr = GCHandle.ToIntPtr(_gcHandle);
                    if (_playerInstances.ContainsKey(instancePtr))
                    {
                        _playerInstances.Remove(instancePtr);
                    }
                    
                    _gcHandle.Free();
                }

                if (_trackToRelease != IntPtr.Zero)
                {
                    LibVLCWrapper.libvlc_media_tracks_release(_trackToRelease, _tracks);
                    _trackToRelease = IntPtr.Zero;
                }

                if (_mediaPlayer != IntPtr.Zero)
                {
                    LibVLCWrapper.libvlc_media_player_release(_mediaPlayer);
                    _mediaPlayer = IntPtr.Zero;
                }

                if (_media != IntPtr.Zero)
                {
                    LibVLCWrapper.libvlc_media_release(_media);
                    _media = IntPtr.Zero;
                }

                if (_libvlc != IntPtr.Zero)
                {
                    LibVLCWrapper.libvlc_release(_libvlc);
                    _libvlc = IntPtr.Zero;
                }

                if (_imageIntPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_imageIntPtr);
                    _imageIntPtr = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"释放VLC资源时发生错误: {ex.Message}");
            }
        }

        #endregion

        #region 回调方法

        /// <summary>
        /// 通过opaque指针获取播放器实例
        /// </summary>
        private static VlcMediaPlayer GetPlayerInstance(IntPtr opaque)
        {
            try
            {
                if (opaque != IntPtr.Zero && _playerInstances.TryGetValue(opaque, out VlcMediaPlayer player))
                {
                    return player;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"获取播放器实例时发生错误: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// VLC锁定回调（静态方法）
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(LockCB))]
        private static IntPtr OnLockStatic(IntPtr opaque, ref IntPtr planes)
        {
            try
            {
                VlcMediaPlayer player = GetPlayerInstance(opaque);
                if (player != null)
                {
                    return player.OnLockInstance(ref planes);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"VLC锁定回调时发生错误: {ex.Message}");
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// VLC解锁回调（静态方法）
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(UnlockCB))]
        private static void OnUnlockStatic(IntPtr opaque, IntPtr picture, ref IntPtr planes)
        {
            try
            {
                VlcMediaPlayer player = GetPlayerInstance(opaque);
                player?.OnUnlockInstance(picture, ref planes);
            }
            catch (Exception ex)
            {
                Debug.LogError($"VLC解锁回调时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// VLC显示回调（静态方法）
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(DisplayCB))]
        private static void OnDisplayStatic(IntPtr opaque, IntPtr picture)
        {
            try
            {
                VlcMediaPlayer player = GetPlayerInstance(opaque);
                player?.OnDisplayInstance(picture);
            }
            catch (Exception ex)
            {
                Debug.LogError($"VLC显示回调时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// VLC锁定回调实例方法
        /// </summary>
        private IntPtr OnLockInstance(ref IntPtr planes)
        {
            if (_imageIntPtr == IntPtr.Zero)
            {
                _imageIntPtr = Marshal.AllocHGlobal(_width * _channels * _height);
            }

            planes = _imageIntPtr;
            return _imageIntPtr;
        }

        /// <summary>
        /// VLC解锁回调实例方法
        /// </summary>
        private void OnUnlockInstance(IntPtr picture, ref IntPtr planes)
        {
            // 在当前实现中不需要执行任何操作
        }

        /// <summary>
        /// VLC显示回调实例方法
        /// </summary>
        private void OnDisplayInstance(IntPtr picture)
        {
            if (!_update && picture != IntPtr.Zero)
            {
                _currentImage = new byte[_width * _channels * _height];
                Marshal.Copy(picture, _currentImage, 0, _width * _channels * _height);
                _update = true;
            }
        }

        #endregion
    }
}