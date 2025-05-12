using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using System.Collections.Generic;

namespace yan.libvlc.Core
{
    /// <summary>
    /// 提供一些用于主线程执行的工具方法
    /// </summary>
    public static class ApplicationExtensions
    {
        /// <summary>
        /// 在主线程上执行操作
        /// </summary>
        /// <param name="application">Application静态类</param>
        /// <param name="action">要执行的操作</param>
        public static void InvokeOnMainThread(this UnityEngine.Application application, Action action)
        {
            if (action == null) return;
            
            // 如果已经在主线程上，则直接执行
            if (Thread.CurrentThread.ManagedThreadId == 1)
            {
                action();
                return;
            }
            
            // 使用Unity的主线程同步上下文执行
            UnityMainThreadDispatcher.Instance.Enqueue(action);
        }
    }
    
    /// <summary>
    /// 主线程调度器，用于在主线程上执行操作
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        
        /// <summary>
        /// 获取实例（如果不存在则创建）
        /// </summary>
        public static UnityMainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    // 在场景中查找现有实例
                    _instance = FindObjectOfType<UnityMainThreadDispatcher>();
                    
                    // 如果不存在，则创建一个新的游戏对象
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("UnityMainThreadDispatcher");
                        _instance = go.AddComponent<UnityMainThreadDispatcher>();
                        DontDestroyOnLoad(go);
                    }
                }
                
                return _instance;
            }
        }
        
        private readonly Queue<Action> _actionQueue = new Queue<Action>();
        private readonly object _queueLock = new object();
        
        /// <summary>
        /// 将操作添加到队列
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void Enqueue(Action action)
        {
            if (action == null) return;
            
            lock (_queueLock)
            {
                _actionQueue.Enqueue(action);
            }
        }
        
        private void Update()
        {
            lock (_queueLock)
            {
                while (_actionQueue.Count > 0)
                {
                    Action action = _actionQueue.Dequeue();
                    
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"执行主线程操作时发生异常: {ex.Message}");
                    }
                }
            }
        }
    }

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
        
        // 图像数据跟踪
        private float _lastImageReceivedTime;
        private bool _hasReceivedAnyImage = false;
        private bool _needToUpdateTimestamp = false;

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
        
        /// <summary>
        /// 获取无图像数据接收的时间（秒）
        /// </summary>
        public float NoImageDataReceivedTime
        {
            get
            {
                // 如果从未收到过图像数据，则检查播放状态
                if (!_hasReceivedAnyImage)
                {
                    // 只有在播放状态下才认为是问题
                    return State == libvlc_state_t.libvlc_Playing ? 
                        (_lastImageReceivedTime > 0 ? Time.time - _lastImageReceivedTime : 3.0f) : 0f;
                }
                
                return Time.time - _lastImageReceivedTime;
            }
        }

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
            : this(width, height, mediaUrl, mute, null)
        {
        }

        /// <summary>
        /// 创建一个新的VLC媒体播放器实例，使用自定义参数
        /// </summary>
        /// <param name="width">视频显示宽度</param>
        /// <param name="height">视频显示高度</param>
        /// <param name="mediaUrl">媒体URL地址</param>
        /// <param name="mute">是否静音</param>
        /// <param name="customArgs">自定义的VLC启动参数，如果为null则使用默认参数</param>
        public VlcMediaPlayer(int width, int height, string mediaUrl, bool mute, string[] customArgs)
        {
            _width = width;
            _height = height;
            _mute = mute;
            _gcHandle = GCHandle.Alloc(this);
            _lastImageReceivedTime = 0;

            InitializeLibVLC(mediaUrl, customArgs);
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
            
            // 在主线程更新时间戳
            if (_needToUpdateTimestamp)
            {
                _lastImageReceivedTime = Time.time;
                _needToUpdateTimestamp = false;
            }
            
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
                SetBlankFrame();
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
        /// 无感更新播放地址（预先加载方式）
        /// </summary>
        /// <param name="newUrl">新的媒体URL</param>
        /// <param name="transitionCallback">转换完成后的回调</param>
        public void UpdateUrlSmooth(string newUrl, Action transitionCallback = null)
        {
            if (string.IsNullOrEmpty(newUrl) || _libvlc == IntPtr.Zero)
            {
                Debug.LogError("无效的URL或LibVLC实例未初始化");
                return;
            }

            // 检查是否为网络流，网络流使用特殊处理
            bool isNetworkStream = newUrl.ToLower().StartsWith("rtmp://") ||
                                  newUrl.ToLower().StartsWith("rtsp://") ||
                                  newUrl.ToLower().StartsWith("http://") ||
                                  newUrl.ToLower().StartsWith("https://");

            // 创建新的媒体对象
            IntPtr newMedia = LibVLCWrapper.libvlc_media_new_location(_libvlc, newUrl);
            if (newMedia == IntPtr.Zero)
            {
                Debug.LogError("无法创建新的媒体对象");
                return;
            }

            // 应用网络流优化参数
            if (isNetworkStream)
            {
                // 设置低延迟参数
                LibVLCWrapper.libvlc_media_add_option(newMedia, ":network-caching=100");
                LibVLCWrapper.libvlc_media_add_option(newMedia, ":clock-jitter=0");
                LibVLCWrapper.libvlc_media_add_option(newMedia, ":live-caching=50");
                // 对于直播流，添加此选项可能会减少首次播放延迟
                LibVLCWrapper.libvlc_media_add_option(newMedia, ":file-caching=50");
            }

            // 预解析媒体以提前缓冲
            LibVLCWrapper.libvlc_media_parse_async(newMedia);
                
            // 等待预解析完成，然后快速切换
            System.Threading.ThreadPool.QueueUserWorkItem(_ => {
                // 等待解析完成，最长等待500ms
                int waitCount = 0;
                int maxWait = 50; // 10ms * 50 = 500ms
                
                while (waitCount < maxWait)
                {
                    libvlc_media_parsed_status_t status = LibVLCWrapper.libvlc_media_get_parsed_status(newMedia);
                    if (status == libvlc_media_parsed_status_t.libvlc_media_parsed_status_done ||
                        status == libvlc_media_parsed_status_t.libvlc_media_parsed_status_failed)
                    {
                        break;
                    }
                    
                    System.Threading.Thread.Sleep(10);
                    waitCount++;
                }
                
                // 在主线程中执行切换
                UnityMainThreadDispatcher.Instance.Enqueue(() => {
                    try
                    {
                        // 记录上一个图像数据
                        byte[] lastImageData = null;
                        if (_currentImage != null)
                        {
                            lastImageData = new byte[_currentImage.Length];
                            Array.Copy(_currentImage, lastImageData, _currentImage.Length);
                        }
                        
                        // 快速停止当前播放但不释放资源
                        if (_mediaPlayer != IntPtr.Zero)
                        {
                            LibVLCWrapper.libvlc_media_player_stop(_mediaPlayer);
                        }

                        // 设置新媒体并立即播放
                        LibVLCWrapper.libvlc_media_player_set_media(_mediaPlayer, newMedia);
                        LibVLCWrapper.libvlc_media_player_play(_mediaPlayer);
                        
                        // 如果有最后一帧数据，在新视频加载期间继续显示
                        if (lastImageData != null)
                        {
                            _currentImage = lastImageData;
                        }
                        
                        // 调用回调
                        transitionCallback?.Invoke();
                    }
                    finally
                    {
                        // 释放媒体对象
                        LibVLCWrapper.libvlc_media_release(newMedia);
                    }
                });
            });
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

        /// <summary>
        /// 获取当前的VLC错误信息
        /// </summary>
        /// <returns>错误信息字符串，如果没有错误则返回空字符串</returns>
        public string GetErrorMessage()
        {
            if (_libvlc == IntPtr.Zero)
                return "LibVLC实例为空";

            IntPtr errorPtr = LibVLCWrapper.libvlc_errmsg();
            if (errorPtr == IntPtr.Zero)
                return "无错误信息";

            string error = Marshal.PtrToStringAnsi(errorPtr);
            return string.IsNullOrEmpty(error) ? "未知错误" : error;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化LibVLC实例并设置媒体
        /// </summary>
        private void InitializeLibVLC(string mediaUrl, string[] customArgs)
        {
            // 如果提供了自定义参数，使用自定义参数，否则使用默认参数
            string[] args;

            // 对于网络流，添加额外的媒体选项
            bool isNetworkStream = mediaUrl.ToLower().StartsWith("rtmp://") ||
                                  mediaUrl.ToLower().StartsWith("rtsp://") ||
                                  mediaUrl.ToLower().StartsWith("http://") ||
                                  mediaUrl.ToLower().StartsWith("https://");

            if (customArgs != null && customArgs.Length > 0)
            {
                args = customArgs;
                Debug.Log($"使用自定义VLC参数: {string.Join(", ", args)}");
            }
            else
            {
                // 解析默认参数
                args = DEFAULT_ARGS.Split(';');

                if (isNetworkStream)
                {
                    // 自定义网络缓冲参数列表
                    List<string> argsList = new List<string>(args);
                    argsList.Add("--network-caching=100");  // 增加网络缓存
                    argsList.Add("--live-caching=100");     // 直播流缓存
                    argsList.Add("--clock-jitter=0");        // 减少时钟抖动
                    argsList.Add("--clock-synchro=0");       // 禁用时钟同步
                    args = argsList.ToArray();
                    
                    Debug.Log($"检测到网络流，已添加额外的缓冲参数: {string.Join(", ", argsList)}");
                }
            }

            _libvlc = LibVLCWrapper.libvlc_new(args.Length, args);

            if (_libvlc == IntPtr.Zero)
            {
                Debug.LogError("初始化LibVLC失败");
                return;
            }

            _media = LibVLCWrapper.libvlc_media_new_location(_libvlc, mediaUrl);

            if (_media == IntPtr.Zero)
            {
                Debug.LogError("创建媒体失败，请检查URL是否正确");
                return;
            }
            
           
            if (isNetworkStream && (customArgs == null || customArgs.Length == 0))
            {
                LibVLCWrapper.libvlc_media_add_option(_media, ":network-caching=3000");
                LibVLCWrapper.libvlc_media_add_option(_media, ":clock-jitter=0");
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
            const int MAX_TRACK_ATTEMPTS = 60; // 增加尝试次数
            int trackGetAttempts = 0;
            
            try 
            {
                // 先等待播放开始
                Thread.Sleep(1000); // 等待1秒，让播放器有足够时间初始化
                
                while (_isRunning && trackGetAttempts < MAX_TRACK_ATTEMPTS && !_cancel)
                {
                    try
                    {
                        // 检查媒体是否开始播放
                        libvlc_state_t state = State;
                        if (state == libvlc_state_t.libvlc_Error)
                        {
                            Debug.LogError($"媒体播放出错，无法获取轨道信息");
                            break;
                        }
                        
                        libvlc_video_track_t? track = GetVideoTrack();

                        if (track.HasValue)
                        {
                            _videoTrack = track;

                            if (_width <= 0 || _height <= 0)
                            {
                                _width = (int)_videoTrack.Value.i_width;
                                _height = (int)_videoTrack.Value.i_height;
                                
                                // 确保分辨率合理
                                if (_width <= 0) _width = 1280;
                                if (_height <= 0) _height = 720;
                                
                                LibVLCWrapper.libvlc_video_set_format(
                                    _mediaPlayer, 
                                    "RV24", 
                                    (uint)_width,
                                    (uint)_height, 
                                    (uint)_width * (uint)_channels
                                );
                            }
                            break;
                        }

                        trackGetAttempts++;
                        
                        // 增加指数退避策略，随着尝试次数增加等待时间
                        int sleepTime = Math.Min(500 + (100 * trackGetAttempts), 2000);
                        Thread.Sleep(sleepTime);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"获取视频轨道时发生异常: {ex.Message}");
                        Thread.Sleep(500);
                        trackGetAttempts++;
                    }
                }

                if (trackGetAttempts >= MAX_TRACK_ATTEMPTS)
                {
                    string errorMsg = "已超过最大尝试获取视频轨道次数，打开失败";
                    Debug.LogError(errorMsg);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"轨道读取线程异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取视频轨道信息
        /// </summary>
        private libvlc_video_track_t? GetVideoTrack()
        {
            if (_media == IntPtr.Zero)
            {
                Debug.LogError("尝试获取轨道但媒体指针为null");
                return null;
            }
            
            libvlc_video_track_t? videoTrack = null;
            IntPtr tracksPtr = IntPtr.Zero;
            int tracks = 0;
            
            try
            {
                tracks = LibVLCWrapper.libvlc_media_tracks_get(_media, out tracksPtr);
                
                if (tracksPtr == IntPtr.Zero)
                {
                    return null;
                }

                _tracks = tracks;
                _trackToRelease = tracksPtr;

                for (int i = 0; i < tracks; i++)
                {
                    IntPtr trackPtr = Marshal.ReadIntPtr(tracksPtr, i * IntPtr.Size);
                    if (trackPtr == IntPtr.Zero) continue;
                    
                    libvlc_media_track_t track = Marshal.PtrToStructure<libvlc_media_track_t>(trackPtr);

                    if (track.i_type == libvlc_track_type_t.libvlc_track_video && track.media != IntPtr.Zero)
                    {
                        try
                        {
                            videoTrack = Marshal.PtrToStructure<libvlc_video_track_t>(track.media);
                            // 检查宽高是否合理
                            if (videoTrack.Value.i_width == 0 || videoTrack.Value.i_height == 0)
                            {
                                videoTrack = null;
                                continue;
                            }
                            break;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"解析视频轨道结构时发生错误: {ex.Message}");
                            videoTrack = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"获取视频轨道时发生异常: {ex.Message}");
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

        /// <summary>
        /// 设置空白画面
        /// </summary>
        private void SetBlankFrame()
        {
            // 创建一个指定颜色的画面，这里以灰色为例 (128, 128, 128)
            byte[] blankFrame = new byte[_width * _channels * _height];
            for (int i = 0; i < blankFrame.Length; i += _channels)
            {
                blankFrame[i] = 50;     // R
                blankFrame[i + 1] = 50; // G
                blankFrame[i + 2] = 50; // B
            }

            // 更新当前图像为空白画面
            _currentImage = blankFrame;
            _update = true;
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
            try 
            {
                if (!_update && picture != IntPtr.Zero)
                {
                    _currentImage = new byte[_width * _channels * _height];
                    Marshal.Copy(picture, _currentImage, 0, _width * _channels * _height);
                    _update = true;
                    
                    // 标记需要在主线程更新时间戳，而不是直接调用Time.time
                    _needToUpdateTimestamp = true;
                    _hasReceivedAnyImage = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"处理VLC视频帧时发生错误: {ex.Message}");
            }
        }

        #endregion
    }
}