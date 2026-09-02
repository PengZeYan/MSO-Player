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
            
            if (UnityMainThreadDispatcher.IsMainThread)
            {
                action();
                return;
            }

            UnityMainThreadDispatcher.EnqueueFromAnyThread(action);
        }
    }
    
    /// <summary>
    /// 主线程调度器，用于在主线程上执行操作
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private static int _mainThreadId;
        private static readonly Queue<Action> _actionQueue = new Queue<Action>();
        private static readonly object _queueLock = new object();

        internal static bool IsMainThread =>
            _mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            _instance = null;
            _mainThreadId = 0;
            lock (_queueLock)
            {
                _actionQueue.Clear();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnMainThread()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            EnsureInstanceOnMainThread();
        }
        
        /// <summary>
        /// 获取实例（如果不存在则创建）
        /// </summary>
        public static UnityMainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    if (!IsMainThread)
                        throw new InvalidOperationException("主线程调度器尚未在Unity主线程初始化");

                    EnsureInstanceOnMainThread();
                }

                return _instance;
            }
        }

        private static void EnsureInstanceOnMainThread()
        {
            if (_instance != null)
                return;

            _instance = FindObjectOfType<UnityMainThreadDispatcher>();
            if (_instance != null)
                return;

            GameObject go = new GameObject("UnityMainThreadDispatcher");
            _instance = go.AddComponent<UnityMainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_mainThreadId == 0)
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;

            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>从任意托管线程将操作加入Unity主线程队列。</summary>
        public static void EnqueueFromAnyThread(Action action)
        {
            if (action == null) return;

            lock (_queueLock)
            {
                _actionQueue.Enqueue(action);
            }
        }
        
        /// <summary>
        /// 将操作添加到队列
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void Enqueue(Action action)
        {
            EnqueueFromAnyThread(action);
        }
        
        private void Update()
        {
            while (true)
            {
                Action action;
                lock (_queueLock)
                {
                    if (_actionQueue.Count == 0)
                        return;

                    action = _actionQueue.Dequeue();
                }

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

        private readonly object _lifecycleLock = new object();
        private readonly object _callbackLock = new object();
        private readonly object _trackThreadLock = new object();
        private bool _acceptCallbacks = true;
        private int _activeCallbacks = 0;
        private int _disposeState = 0;
        private int _mediaGeneration = 0;
        private Thread _trackReaderThread;
        private CancellationTokenSource _trackReaderCancellation;

        // 优化1：双缓冲机制，避免每帧分配新数组
        private byte[] _currentImage;
        private byte[] _backBuffer;
        private readonly object _bufferLock = new object();
        private bool _update = false;
        private bool _mute = true;
        private int _width = 480;
        private int _height = 256;
        private int _channels = 3;
        private readonly bool _useDefaultMediaOptions;
        private const long MAX_OUTPUT_PIXELS = 33_554_432;
        
        // 用于静态回调方法访问实例的静态字典
        private static readonly Dictionary<IntPtr, VlcMediaPlayer> _playerInstances = new Dictionary<IntPtr, VlcMediaPlayer>();
        private static readonly object _playerInstancesLock = new object();
        
        // 优化：优化默认参数
        private const string DEFAULT_ARGS = "--ignore-config;--no-xlib;--no-video-title-show;--no-osd;--clock-jitter=0;--avcodec-threads=4";
        private libvlc_video_track_t? _videoTrack = null;
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
                lock (_lifecycleLock)
                {
                    if (_mediaPlayer != IntPtr.Zero && !IsDisposed)
                        return LibVLCWrapper.libvlc_media_player_get_state(_mediaPlayer);
                    return libvlc_state_t.libvlc_NothingSpecial;
                }
            }
        }

        /// <summary>
        /// 获取当前视频轨道信息
        /// </summary>
        public libvlc_video_track_t? VideoTrack
        {
            get
            {
                lock (_lifecycleLock)
                {
                    return _videoTrack;
                }
            }
        }

        /// <summary>获取当前输出缓冲区宽度</summary>
        public int OutputWidth => _width;

        /// <summary>获取当前输出缓冲区高度</summary>
        public int OutputHeight => _height;

        /// <summary>播放器是否已经释放</summary>
        public bool IsDisposed => Volatile.Read(ref _disposeState) != 0;
        
        /// <summary>
        /// 获取无图像数据接收的时间（秒）
        /// </summary>
        public float NoImageDataReceivedTime
        {
            get
            {
                // 如果从未收到过图像数据，则检查播放状态
                bool hasReceivedAnyImage;
                float lastImageReceivedTime;

                lock (_bufferLock)
                {
                    hasReceivedAnyImage = _hasReceivedAnyImage;
                    lastImageReceivedTime = _lastImageReceivedTime;
                }

                if (!hasReceivedAnyImage)
                {
                    // 只有在播放状态下才认为是问题
                    return State == libvlc_state_t.libvlc_Playing
                        ? Mathf.Max(0f, Time.realtimeSinceStartup - lastImageReceivedTime)
                        : 0f;
                }
                
                return Time.realtimeSinceStartup - lastImageReceivedTime;
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
            if (string.IsNullOrWhiteSpace(mediaUrl))
                throw new ArgumentException("媒体URL不能为空", nameof(mediaUrl));

            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "视频输出宽度必须大于0");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), "视频输出高度必须大于0");
            if ((long)width * height > MAX_OUTPUT_PIXELS)
                throw new ArgumentOutOfRangeException("width/height", "视频输出像素数量过大");

            _width = width;
            _height = height;
            _mute = mute;
            _useDefaultMediaOptions = customArgs == null || customArgs.Length == 0;
            _gcHandle = GCHandle.Alloc(this);
            _lastImageReceivedTime = Time.realtimeSinceStartup;

            try
            {
                AllocateFrameBuffers();

                // 注意：LibVLC初始化必须在主线程执行，不能异步
                // 通过MediaPlayerPreloader在登录界面预热来避免首次使用时的卡顿
                InitializeLibVLC(mediaUrl, customArgs);
                SetupCallbacks();
                StartPlayback();
            }
            catch
            {
                Interlocked.Exchange(ref _disposeState, 1);
                ReleaseResources();
                throw;
            }
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

            bool updateTimestamp;
            lock (_bufferLock)
            {
                updateTimestamp = _needToUpdateTimestamp;
                _needToUpdateTimestamp = false;

                if (_update)
                {
                    currentImage = _currentImage;
                    _update = false;
                }
            }

            if (updateTimestamp)
            {
                lock (_bufferLock)
                {
                    _lastImageReceivedTime = Time.realtimeSinceStartup;
                }
            }

            return currentImage != null;
        }

        /// <summary>
        /// 开始或恢复播放当前媒体
        /// </summary>
        /// <returns>播放请求是否成功提交</returns>
        public bool Play()
        {
            lock (_lifecycleLock)
            {
                if (IsDisposed || _mediaPlayer == IntPtr.Zero)
                    return false;

                int result = LibVLCWrapper.libvlc_media_player_play(_mediaPlayer);
                if (result != 0)
                    return false;

                ApplyAudioStateLocked();
                return true;
            }
        }

        /// <summary>
        /// 暂停播放；已暂停或未播放时不改变状态
        /// </summary>
        public void Pause()
        {
            lock (_lifecycleLock)
            {
                if (IsDisposed || _mediaPlayer == IntPtr.Zero)
                    return;

                if (LibVLCWrapper.libvlc_media_player_is_playing(_mediaPlayer))
                    LibVLCWrapper.libvlc_media_player_set_pause(_mediaPlayer, 1);
            }
        }

        /// <summary>恢复已暂停的媒体；非暂停状态下提交普通播放请求。</summary>
        public bool Resume()
        {
            lock (_lifecycleLock)
            {
                if (IsDisposed || _mediaPlayer == IntPtr.Zero)
                    return false;

                if (LibVLCWrapper.libvlc_media_player_get_state(_mediaPlayer) == libvlc_state_t.libvlc_Paused)
                {
                    LibVLCWrapper.libvlc_media_player_set_pause(_mediaPlayer, 0);
                    ApplyAudioStateLocked();
                    return true;
                }

                int result = LibVLCWrapper.libvlc_media_player_play(_mediaPlayer);
                if (result == 0)
                    ApplyAudioStateLocked();
                return result == 0;
            }
        }

        /// <summary>在播放与暂停状态之间切换。</summary>
        public bool TogglePause()
        {
            lock (_lifecycleLock)
            {
                if (IsDisposed || _mediaPlayer == IntPtr.Zero)
                    return false;

                if (LibVLCWrapper.libvlc_media_player_is_playing(_mediaPlayer))
                {
                    LibVLCWrapper.libvlc_media_player_set_pause(_mediaPlayer, 1);
                    return true;
                }

                if (LibVLCWrapper.libvlc_media_player_get_state(_mediaPlayer) == libvlc_state_t.libvlc_Paused)
                {
                    LibVLCWrapper.libvlc_media_player_set_pause(_mediaPlayer, 0);
                    ApplyAudioStateLocked();
                    return true;
                }

                int result = LibVLCWrapper.libvlc_media_player_play(_mediaPlayer);
                if (result == 0)
                    ApplyAudioStateLocked();
                return result == 0;
            }
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        public void Stop()
        {
            lock (_lifecycleLock)
            {
                if (IsDisposed || _mediaPlayer == IntPtr.Zero)
                    return;

                LibVLCWrapper.libvlc_media_player_stop(_mediaPlayer);
            }

            SetBlankFrame();
        }

        /// <summary>
        /// 更新播放地址
        /// </summary>
        /// <param name="newUrl">新的媒体URL</param>
        public void UpdateUrl(string newUrl)
        {
            SwitchUrl(newUrl, false);
        }

        /// <summary>
        /// 无感更新播放地址（预先加载方式）
        /// </summary>
        /// <param name="newUrl">新的媒体URL</param>
        /// <param name="transitionCallback">转换完成后的回调</param>
        public void UpdateUrlSmooth(string newUrl, Action transitionCallback = null)
        {
            SwitchUrl(newUrl, true);
            transitionCallback?.Invoke();
        }

        /// <summary>
        /// 检查是否正在播放
        /// </summary>
        /// <returns>如果正在播放则返回true，否则返回false</returns>
        public bool IsPlaying()
        {
            lock (_lifecycleLock)
            {
                return !IsDisposed &&
                       _mediaPlayer != IntPtr.Zero &&
                       LibVLCWrapper.libvlc_media_player_is_playing(_mediaPlayer);
            }
        }

        /// <summary>获取当前播放时间（毫秒）</summary>
        public long GetTime()
        {
            lock (_lifecycleLock)
            {
                if (IsDisposed || _mediaPlayer == IntPtr.Zero)
                    return 0;

                long value = LibVLCWrapper.libvlc_media_player_get_time(_mediaPlayer);
                return value >= 0 ? value : 0;
            }
        }

        /// <summary>设置当前播放时间（毫秒）</summary>
        public bool SetTime(long time)
        {
            lock (_lifecycleLock)
            {
                return !IsDisposed &&
                       _mediaPlayer != IntPtr.Zero &&
                       LibVLCWrapper.libvlc_media_player_set_time(_mediaPlayer, Math.Max(0, time)) == 0;
            }
        }

        /// <summary>获取当前媒体总时长（毫秒）</summary>
        public long GetLength()
        {
            lock (_lifecycleLock)
            {
                if (IsDisposed || _mediaPlayer == IntPtr.Zero)
                    return 0;

                long value = LibVLCWrapper.libvlc_media_player_get_length(_mediaPlayer);
                if (value <= 0 && _media != IntPtr.Zero)
                    value = LibVLCWrapper.libvlc_media_get_duration(_media);

                return value >= 0 ? value : 0;
            }
        }

        /// <summary>获取当前播放位置（0到1）</summary>
        public float GetPosition()
        {
            lock (_lifecycleLock)
            {
                if (IsDisposed || _mediaPlayer == IntPtr.Zero)
                    return 0f;

                float value = LibVLCWrapper.libvlc_media_player_get_position(_mediaPlayer);
                return value >= 0f ? Mathf.Clamp01(value) : 0f;
            }
        }

        /// <summary>设置当前播放位置（0到1）</summary>
        public bool SetPosition(float position)
        {
            lock (_lifecycleLock)
            {
                return !IsDisposed &&
                       _mediaPlayer != IntPtr.Zero &&
                       LibVLCWrapper.libvlc_media_player_set_position(_mediaPlayer, Mathf.Clamp01(position)) == 0;
            }
        }

        /// <summary>当前媒体是否允许跳转</summary>
        public bool IsSeekable()
        {
            lock (_lifecycleLock)
            {
                return !IsDisposed &&
                       _mediaPlayer != IntPtr.Zero &&
                       LibVLCWrapper.libvlc_media_player_is_seekable(_mediaPlayer);
            }
        }

        /// <summary>
        /// 设置静音状态
        /// </summary>
        /// <param name="mute">是否静音</param>
        /// <returns>操作是否成功</returns>
        public bool SetMute(bool mute)
        {
            _mute = mute;

            lock (_lifecycleLock)
            {
                if (IsDisposed || _mediaPlayer == IntPtr.Zero)
                    return false;

                try
                {
                    int result = LibVLCWrapper.libvlc_audio_set_mute(_mediaPlayer, mute ? 1 : 0);
                    return result == 0;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"设置静音状态失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 获取静音状态
        /// </summary>
        /// <returns>是否静音</returns>
        public bool IsMuted()
        {
            lock (_lifecycleLock)
            {
                if (IsDisposed || _mediaPlayer == IntPtr.Zero)
                    return _mute;

                try
                {
                    int mute = LibVLCWrapper.libvlc_audio_get_mute(_mediaPlayer);
                    return mute == 1;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"获取静音状态失败: {ex.Message}");
                    return _mute;
                }
            }
        }

        /// <summary>
        /// 设置音量（0-100）
        /// </summary>
        /// <param name="volume">音量值</param>
        /// <returns>操作是否成功</returns>
        public bool SetVolume(int volume)
        {
            lock (_lifecycleLock)
            {
                if (IsDisposed || _mediaPlayer == IntPtr.Zero)
                    return false;

                try
                {
                    volume = Mathf.Clamp(volume, 0, 100);
                    int result = LibVLCWrapper.libvlc_audio_set_volume(_mediaPlayer, volume);
                    return result == 0;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"设置音量失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 获取音量（0-100）
        /// </summary>
        /// <returns>当前音量</returns>
        public int GetVolume()
        {
            lock (_lifecycleLock)
            {
                if (IsDisposed || _mediaPlayer == IntPtr.Zero)
                    return 0;

                try
                {
                    return LibVLCWrapper.libvlc_audio_get_volume(_mediaPlayer);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"获取音量失败: {ex.Message}");
                    return 0;
                }
            }
        }

        /// <summary>
        /// 释放所有资源
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0)
                return;

            try
            {
                StopTrackReader();

                lock (_lifecycleLock)
                {
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

                    lock (_callbackLock)
                    {
                        _acceptCallbacks = false;
                    }
                }

                WaitForCallbacksToDrain();
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
            if (IsDisposed || _libvlc == IntPtr.Zero)
                return "LibVLC实例为空";

            IntPtr errorPtr = LibVLCWrapper.libvlc_errmsg();
            if (errorPtr == IntPtr.Zero)
                return "无错误信息";

            string error = Marshal.PtrToStringAnsi(errorPtr);
            return string.IsNullOrEmpty(error) ? "未知错误" : error;
        }

        #endregion

        #region 私有方法

        private void AllocateFrameBuffers()
        {
            int bufferSize;
            try
            {
                bufferSize = checked(_width * _height * _channels);
            }
            catch (OverflowException)
            {
                throw new ArgumentOutOfRangeException("width/height", "视频输出分辨率过大");
            }

            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException("width/height", "视频输出分辨率必须大于0");

            _currentImage = new byte[bufferSize];
            _backBuffer = new byte[bufferSize];
            _imageIntPtr = Marshal.AllocHGlobal(bufferSize);
        }

        /// <summary>
        /// 初始化LibVLC实例并设置媒体
        /// </summary>
        private void InitializeLibVLC(string mediaUrl, string[] customArgs)
        {
            string[] args;

            if (customArgs != null && customArgs.Length > 0)
            {
                args = customArgs;
                Debug.Log($"使用自定义VLC参数（{args.Length}项）");
            }
            else
            {
                List<string> argsList = new List<string>(DEFAULT_ARGS.Split(';'));
                if (IsNetworkStream(mediaUrl))
                {
                    argsList.Add("--network-caching=1000");
                    argsList.Add("--live-caching=500");
                    argsList.Add("--clock-synchro=0");
                }
                argsList.Add("--file-caching=300");
                args = argsList.ToArray();
            }

            _libvlc = LibVLCWrapper.libvlc_new(args.Length, args);
            if (_libvlc == IntPtr.Zero)
                throw new InvalidOperationException("初始化LibVLC失败");

            _media = CreateMedia(mediaUrl);
            if (_media == IntPtr.Zero)
                throw new InvalidOperationException("创建媒体失败，请检查媒体地址");

            if (_useDefaultMediaOptions)
                ApplyNetworkMediaOptions(_media, mediaUrl, false);

            _mediaPlayer = LibVLCWrapper.libvlc_media_player_new(_libvlc);
            if (_mediaPlayer == IntPtr.Zero)
                throw new InvalidOperationException("创建LibVLC播放器失败");

            LibVLCWrapper.libvlc_media_player_set_media(_mediaPlayer, _media);
            _mediaGeneration++;
        }

        private IntPtr CreateMedia(string mediaUrl)
        {
            if (Uri.TryCreate(mediaUrl, UriKind.Absolute, out Uri uri))
            {
                if (uri.IsFile)
                    return LibVLCWrapper.libvlc_media_new_path(_libvlc, uri.LocalPath);

                return LibVLCWrapper.libvlc_media_new_location(_libvlc, mediaUrl);
            }

            return LibVLCWrapper.libvlc_media_new_path(_libvlc, mediaUrl);
        }

        private static bool IsNetworkStream(string mediaUrl)
        {
            return mediaUrl.StartsWith("rtmp://", StringComparison.OrdinalIgnoreCase) ||
                   mediaUrl.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) ||
                   mediaUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   mediaUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyNetworkMediaOptions(IntPtr media, string mediaUrl, bool lowLatency)
        {
            if (media == IntPtr.Zero || !IsNetworkStream(mediaUrl))
                return;

            LibVLCWrapper.libvlc_media_add_option(media, lowLatency ? ":network-caching=100" : ":network-caching=1000");
            LibVLCWrapper.libvlc_media_add_option(media, ":clock-jitter=0");

            if (lowLatency)
            {
                LibVLCWrapper.libvlc_media_add_option(media, ":live-caching=50");
                LibVLCWrapper.libvlc_media_add_option(media, ":file-caching=50");
            }
        }

        private void SwitchUrl(string newUrl, bool preserveLastFrame)
        {
            if (string.IsNullOrWhiteSpace(newUrl))
                throw new ArgumentException("媒体URL不能为空", nameof(newUrl));

            IntPtr newMedia;
            lock (_lifecycleLock)
            {
                if (IsDisposed || _libvlc == IntPtr.Zero || _mediaPlayer == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(VlcMediaPlayer), "播放器已经释放或LibVLC尚未初始化");

                newMedia = CreateMedia(newUrl);
                if (newMedia != IntPtr.Zero && _useDefaultMediaOptions)
                    ApplyNetworkMediaOptions(newMedia, newUrl, preserveLastFrame);
            }

            if (newMedia == IntPtr.Zero)
                throw new InvalidOperationException("无法创建新的媒体对象");

            bool adopted = false;
            IntPtr oldMedia = IntPtr.Zero;

            try
            {
                if (preserveLastFrame)
                    LibVLCWrapper.libvlc_media_parse_async(newMedia);

                StopTrackReader();

                int playResult;
                lock (_lifecycleLock)
                {
                    if (IsDisposed || _mediaPlayer == IntPtr.Zero)
                        throw new ObjectDisposedException(nameof(VlcMediaPlayer), "切换媒体时播放器已被释放");

                    LibVLCWrapper.libvlc_media_player_stop(_mediaPlayer);
                    LibVLCWrapper.libvlc_media_player_set_media(_mediaPlayer, newMedia);

                    oldMedia = _media;
                    _media = newMedia;
                    adopted = true;
                    _mediaGeneration++;
                    _videoTrack = null;

                    lock (_bufferLock)
                    {
                        _hasReceivedAnyImage = false;
                        _needToUpdateTimestamp = false;
                        _lastImageReceivedTime = Time.realtimeSinceStartup;
                    }

                    playResult = LibVLCWrapper.libvlc_media_player_play(_mediaPlayer);
                    ApplyAudioStateLocked();
                }

                if (oldMedia != IntPtr.Zero)
                    LibVLCWrapper.libvlc_media_release(oldMedia);

                if (!preserveLastFrame)
                    SetBlankFrame();

                if (playResult != 0)
                    throw new InvalidOperationException("切换媒体后启动播放失败");

                StartTrackReader();
            }
            finally
            {
                if (!adopted)
                    LibVLCWrapper.libvlc_media_release(newMedia);
            }
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
            lock (_playerInstancesLock)
            {
                _playerInstances[instancePtr] = this;
            }

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
            if (!Play())
                throw new InvalidOperationException("LibVLC无法开始播放媒体");

            StartTrackReader();
        }

        private void ApplyAudioStateLocked()
        {
            LibVLCWrapper.libvlc_audio_set_mute(_mediaPlayer, _mute ? 1 : 0);
            if (!_mute)
            {
                int currentVolume = LibVLCWrapper.libvlc_audio_get_volume(_mediaPlayer);
                if (currentVolume <= 0)
                    LibVLCWrapper.libvlc_audio_set_volume(_mediaPlayer, 100);
            }
        }

        private void StartTrackReader()
        {
            StopTrackReader();

            IntPtr media;
            int generation;
            lock (_lifecycleLock)
            {
                if (IsDisposed || _media == IntPtr.Zero)
                    return;

                media = _media;
                generation = _mediaGeneration;
                LibVLCWrapper.libvlc_media_retain(media);
            }

            CancellationTokenSource cancellation = new CancellationTokenSource();
            Thread thread = new Thread(() => TrackReaderThread(media, generation, cancellation.Token));
            thread.IsBackground = true;
            thread.Name = "MSO-Player Track Reader";

            lock (_trackThreadLock)
            {
                _trackReaderCancellation = cancellation;
                _trackReaderThread = thread;
            }

            try
            {
                thread.Start();
            }
            catch
            {
                lock (_trackThreadLock)
                {
                    _trackReaderCancellation = null;
                    _trackReaderThread = null;
                }

                cancellation.Dispose();
                LibVLCWrapper.libvlc_media_release(media);
                throw;
            }
        }

        private void StopTrackReader()
        {
            CancellationTokenSource cancellation;
            Thread thread;

            lock (_trackThreadLock)
            {
                cancellation = _trackReaderCancellation;
                thread = _trackReaderThread;
                _trackReaderCancellation = null;
                _trackReaderThread = null;
            }

            cancellation?.Cancel();

            bool stopped = thread == null || !thread.IsAlive || thread == Thread.CurrentThread || thread.Join(1000);
            if (!stopped)
            {
                Debug.LogWarning("等待视频轨道读取线程退出超时；保留取消令牌直到线程自行结束");
                return;
            }

            cancellation?.Dispose();
        }

        /// <summary>
        /// 轨道读取线程
        /// </summary>
        private void TrackReaderThread(IntPtr media, int generation, CancellationToken cancellationToken)
        {
            const int MAX_TRACK_ATTEMPTS = 20;
            int trackGetAttempts = 0;

            try
            {
                if (cancellationToken.WaitHandle.WaitOne(300))
                    return;

                while (trackGetAttempts < MAX_TRACK_ATTEMPTS && !cancellationToken.IsCancellationRequested)
                {
                    libvlc_video_track_t? track = GetVideoTrack(media);
                    if (track.HasValue)
                    {
                        lock (_lifecycleLock)
                        {
                            if (!IsDisposed && generation == _mediaGeneration)
                                _videoTrack = track;
                        }
                        return;
                    }

                    trackGetAttempts++;
                    int waitTime = Math.Min(50 + (30 * trackGetAttempts), 300);
                    if (cancellationToken.WaitHandle.WaitOne(waitTime))
                        return;
                }

                lock (_lifecycleLock)
                {
                    if (!IsDisposed && generation == _mediaGeneration)
                        Debug.LogError("已超过最大尝试获取视频轨道次数，打开失败");
                }
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                    Debug.LogError($"轨道读取线程异常: {ex.Message}");
            }
            finally
            {
                LibVLCWrapper.libvlc_media_release(media);
            }
        }

        /// <summary>
        /// 获取视频轨道信息
        /// </summary>
        private static libvlc_video_track_t? GetVideoTrack(IntPtr media)
        {
            if (media == IntPtr.Zero)
                return null;

            libvlc_video_track_t? videoTrack = null;
            IntPtr tracksPtr = IntPtr.Zero;
            int tracks = 0;

            try
            {
                tracks = LibVLCWrapper.libvlc_media_tracks_get(media, out tracksPtr);
                if (tracksPtr == IntPtr.Zero)
                    return null;

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
            finally
            {
                if (tracksPtr != IntPtr.Zero)
                    LibVLCWrapper.libvlc_media_tracks_release(tracksPtr, tracks);
            }

            return videoTrack;
        }

        /// <summary>
        /// 释放所有分配的资源
        /// </summary>
        private void ReleaseResources()
        {
            StopTrackReader();

            lock (_lifecycleLock)
            {
                try
                {
                    if (_mediaPlayer != IntPtr.Zero)
                        LibVLCWrapper.libvlc_media_player_stop(_mediaPlayer);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"停止LibVLC播放器时发生错误: {ex.Message}");
                }

                lock (_callbackLock)
                {
                    _acceptCallbacks = false;
                }
            }

            WaitForCallbacksToDrain();

            lock (_lifecycleLock)
            {
                try
                {
                    if (_mediaPlayer != IntPtr.Zero)
                        LibVLCWrapper.libvlc_media_player_release(_mediaPlayer);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"释放LibVLC播放器时发生错误: {ex.Message}");
                }
                finally
                {
                    _mediaPlayer = IntPtr.Zero;
                }

                try
                {
                    if (_media != IntPtr.Zero)
                        LibVLCWrapper.libvlc_media_release(_media);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"释放LibVLC媒体时发生错误: {ex.Message}");
                }
                finally
                {
                    _media = IntPtr.Zero;
                }

                try
                {
                    if (_libvlc != IntPtr.Zero)
                        LibVLCWrapper.libvlc_release(_libvlc);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"释放LibVLC实例时发生错误: {ex.Message}");
                }
                finally
                {
                    _libvlc = IntPtr.Zero;
                }
            }

            if (_imageIntPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_imageIntPtr);
                _imageIntPtr = IntPtr.Zero;
            }

            if (_gcHandle.IsAllocated)
            {
                IntPtr instancePtr = GCHandle.ToIntPtr(_gcHandle);
                lock (_playerInstancesLock)
                {
                    _playerInstances.Remove(instancePtr);
                }
                _gcHandle.Free();
            }
        }

        private bool TryBeginCallback()
        {
            lock (_callbackLock)
            {
                if (!_acceptCallbacks || IsDisposed)
                    return false;

                _activeCallbacks++;
                return true;
            }
        }

        private void EndCallback()
        {
            lock (_callbackLock)
            {
                _activeCallbacks--;
                if (_activeCallbacks == 0)
                    Monitor.PulseAll(_callbackLock);
            }
        }

        private void WaitForCallbacksToDrain()
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(2);
            lock (_callbackLock)
            {
                while (_activeCallbacks > 0)
                {
                    TimeSpan remaining = deadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                    {
                        Debug.LogWarning("等待LibVLC视频回调结束超时");
                        return;
                    }

                    Monitor.Wait(_callbackLock, remaining);
                }
            }
        }

        /// <summary>
        /// 设置空白画面
        /// </summary>
        private void SetBlankFrame()
        {
            lock (_bufferLock)
            {
                if (_currentImage == null)
                    return;

                for (int i = 0; i < _currentImage.Length; i++)
                    _currentImage[i] = 50;

                _update = true;
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
                if (opaque == IntPtr.Zero)
                    return null;

                lock (_playerInstancesLock)
                {
                    if (_playerInstances.TryGetValue(opaque, out VlcMediaPlayer player))
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
            VlcMediaPlayer player = null;
            bool frameLeaseActive = false;
            try
            {
                player = GetPlayerInstance(opaque);
                if (player == null || !player.TryBeginCallback())
                    return IntPtr.Zero;

                frameLeaseActive = true;
                IntPtr picture = player.OnLockInstance(ref planes);
                if (picture == IntPtr.Zero)
                    return IntPtr.Zero;

                // 帧租约跨越LibVLC写入阶段，直到对应unlock回调才结束。
                frameLeaseActive = false;
                return picture;
            }
            catch (Exception ex)
            {
                Debug.LogError($"VLC锁定回调时发生错误: {ex.Message}");
            }
            finally
            {
                if (frameLeaseActive)
                    player.EndCallback();
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
                if (player == null)
                    return;

                try
                {
                    player.OnUnlockInstance(picture, ref planes);
                }
                finally
                {
                    player.EndCallback();
                }
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
                if (player == null || !player.TryBeginCallback())
                    return;

                try
                {
                    player.OnDisplayInstance(picture);
                }
                finally
                {
                    player.EndCallback();
                }
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
                return IntPtr.Zero;

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
                if (picture == IntPtr.Zero)
                    return;

                lock (_bufferLock)
                {
                    if (_update || _backBuffer == null)
                        return;

                    Marshal.Copy(picture, _backBuffer, 0, _backBuffer.Length);

                    byte[] temp = _currentImage;
                    _currentImage = _backBuffer;
                    _backBuffer = temp;

                    _update = true;
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
