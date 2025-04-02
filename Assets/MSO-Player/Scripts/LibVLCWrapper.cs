using System;
using System.Runtime.InteropServices;

/// <summary>
/// 这是一个libvlc库的部分封装。
/// This is just a partial wrapper.
/// For more information, check: https://www.videolan.org/developers/vlc/doc/doxygen/html/group__libvlc.html
/// </summary>

namespace yan.libvlc
{
    /// <summary>
    /// 媒体播放器状态枚举
    /// </summary>
    public enum libvlc_state_t
    {
        /// <summary>无特殊状态</summary>
        libvlc_NothingSpecial,
        /// <summary>正在打开媒体</summary>
        libvlc_Opening,
        /// <summary>媒体正在缓冲</summary>
        libvlc_Buffering,
        /// <summary>媒体正在播放</summary>
        libvlc_Playing,
        /// <summary>媒体已暂停</summary>
        libvlc_Paused,
        /// <summary>媒体已停止</summary>
        libvlc_Stopped,
        /// <summary>媒体已结束</summary>
        libvlc_Ended,
        /// <summary>媒体播放发生错误</summary>
        libvlc_Error
    }

    /// <summary>
    /// 媒体轨道类型枚举
    /// </summary>
    public enum libvlc_track_type_t
    {
        /// <summary>未知的轨道类型</summary>
        libvlc_track_unknown = -1,
        /// <summary>音频轨道</summary>
        libvlc_track_audio = 0,
        /// <summary>视频轨道</summary>
        libvlc_track_video = 1,
        /// <summary>文本轨道（如字幕）</summary>
        libvlc_track_text = 2
    }

    /// <summary>
    /// 媒体轨道结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct libvlc_media_track_t
    {
        /// <summary>编解码器</summary>
        public uint i_codec;
        /// <summary>原始FourCC</summary>
        public uint i_original_fourcc;
        /// <summary>轨道ID</summary>
        public int i_id;
        /// <summary>轨道类型</summary>
        public libvlc_track_type_t i_type;
        /// <summary>配置文件</summary>
        public int i_profile;
        /// <summary>级别</summary>
        public int i_level;
        /// <summary>媒体</summary>
        public IntPtr media;
        /// <summary>比特率</summary>
        public uint i_bitrate;
        /// <summary>语言</summary>
        public IntPtr psz_language;
        /// <summary>描述</summary>
        public IntPtr psz_description;
    }

    /// <summary>
    /// 视频轨道结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct libvlc_video_track_t
    {
        /// <summary>视频高度</summary>
        public uint i_height;
        /// <summary>视频宽度</summary>
        public uint i_width;
        /// <summary>采样纵横比分子</summary>
        public uint i_sar_num;
        /// <summary>采样纵横比分母</summary>
        public uint i_sar_den;
        /// <summary>帧率分子</summary>
        public uint i_frame_rate_num;
        /// <summary>帧率分母</summary>
        public uint i_frame_rate_den;
        /// <summary>方向</summary>
        public uint i_orientation;
        /// <summary>投影方式</summary>
        public uint i_projection;
        /// <summary>位姿</summary>
        public IntPtr pose;
        /// <summary>多视图</summary>
        public uint i_multiview;
    }

    /// <summary>
    /// 锁定回调委托
    /// </summary>
    /// <param name="opaque">不透明指针</param>
    /// <param name="planes">平面数据</param>
    /// <returns>内存缓冲区指针</returns>
    public delegate IntPtr LockCB(IntPtr opaque, ref IntPtr planes);

    /// <summary>
    /// 解锁回调委托
    /// </summary>
    /// <param name="opaque">不透明指针</param>
    /// <param name="picture">图像指针</param>
    /// <param name="planes">平面数据</param>
    public delegate void UnlockCB(IntPtr opaque, IntPtr picture, ref IntPtr planes);

    /// <summary>
    /// 显示回调委托
    /// </summary>
    /// <param name="opaque">不透明指针</param>
    /// <param name="picture">图像指针</param>
    public delegate void DisplayCB(IntPtr opaque, IntPtr picture);

    /// <summary>
    /// LibVLC库封装类
    /// 参考文档: https://videolan.videolan.me/vlc/group__libvlc.html
    /// </summary>
    public static class LibVLCWrapper
    {
        #region 库版本

        /// <summary>
        /// 获取libvlc版本
        /// </summary>
        /// <returns>包含libvlc版本的字符串</returns>
        [DllImport("libvlc")]
        internal static extern IntPtr libvlc_get_version();

        #endregion

        #region 错误处理

        /// <summary>
        /// 获取最后一个LibVLC错误的人类可读错误消息
        /// </summary>
        /// <returns>错误消息的指针，如果没有错误则为NULL</returns>
        [DllImport("libvlc")]
        internal static extern IntPtr libvlc_errmsg();

        /// <summary>
        /// 清除LibVLC错误状态
        /// </summary>
        [DllImport("libvlc")]
        internal static extern void libvlc_clearerr();

        #endregion

        #region 实例管理

        /// <summary>
        /// 创建并初始化libvlc实例
        /// </summary>
        /// <param name="argc">参数数量</param>
        /// <param name="args">参数列表</param>
        /// <returns>libvlc实例或NULL表示错误</returns>
        [DllImport("libvlc")]
        internal static extern IntPtr libvlc_new(int argc, params string[] args);

        /// <summary>
        /// 递减libvlc实例的引用计数，当引用计数为零时销毁实例
        /// </summary>
        /// <param name="libvlc_instance">要销毁的实例</param>
        [DllImport("libvlc")]
        internal static extern void libvlc_release(IntPtr libvlc_instance);

        #endregion

        #region 媒体轨道

        /// <summary>
        /// 获取媒体轨道信息
        /// </summary>
        /// <param name="media">媒体指针</param>
        /// <param name="ppTracks">轨道信息输出指针</param>
        /// <returns>轨道数量</returns>
        [DllImport("libvlc")]
        internal static extern int libvlc_media_tracks_get(IntPtr media, out IntPtr ppTracks);

        /// <summary>
        /// 释放由libvlc_media_tracks_get返回的轨道信息
        /// </summary>
        /// <param name="tracks">轨道信息指针</param>
        /// <param name="i_count">轨道数量</param>
        [DllImport("libvlc")]
        internal static extern void libvlc_media_tracks_release(IntPtr tracks, int i_count);

        #endregion

        #region 媒体播放器

        /// <summary>
        /// 创建空的媒体播放器对象
        /// </summary>
        /// <param name="libvlc_instance">libvlc实例</param>
        /// <returns>新的媒体播放器对象，错误时返回NULL</returns>
        [DllImport("libvlc")]
        internal static extern IntPtr libvlc_media_player_new(IntPtr libvlc_instance);

        /// <summary>
        /// 为特定文件路径创建媒体
        /// </summary>
        /// <param name="libvlc_instance">libvlc实例</param>
        /// <param name="path">文件路径</param>
        /// <returns>新创建的媒体，错误时返回NULL</returns>
        [DllImport("libvlc")]
        internal static extern IntPtr libvlc_media_new_path(IntPtr libvlc_instance, string path);

        /// <summary>
        /// 创建具有特定媒体位置的媒体
        /// </summary>
        /// <param name="libvlc_instance">libvlc实例</param>
        /// <param name="path">媒体URL</param>
        /// <returns>新创建的媒体，错误时返回NULL</returns>
        [DllImport("libvlc")]
        internal static extern IntPtr libvlc_media_new_location(IntPtr libvlc_instance, string path);

        /// <summary>
        /// 获取当前媒体状态
        /// </summary>
        /// <param name="mediaPlayer">媒体播放器</param>
        /// <returns>媒体状态</returns>
        [DllImport("libvlc")]
        internal static extern libvlc_state_t libvlc_media_player_get_state(IntPtr mediaPlayer);

        /// <summary>
        /// 暂停播放
        /// </summary>
        /// <param name="mediaPlayer">媒体播放器</param>
        [DllImport("libvlc")]
        internal static extern void libvlc_media_player_pause(IntPtr mediaPlayer);

        /// <summary>
        /// 开始播放
        /// </summary>
        /// <param name="mediaPlayer">媒体播放器</param>
        /// <returns>如果播放已开始则为0，错误则为-1</returns>
        [DllImport("libvlc")]
        internal static extern int libvlc_media_player_play(IntPtr mediaPlayer);

        /// <summary>
        /// 获取媒体持续时间（毫秒）
        /// </summary>
        /// <param name="media">媒体指针</param>
        /// <returns>媒体持续时间（毫秒）</returns>
        [DllImport("libvlc")]
        internal static extern Int64 libvlc_media_get_duration(IntPtr media);

        /// <summary>
        /// 获取当前播放时间（毫秒）
        /// </summary>
        /// <param name="mediaPlayer">媒体播放器</param>
        /// <returns>当前时间（毫秒）</returns>
        [DllImport("libvlc")]
        internal static extern Int64 libvlc_media_player_get_time(IntPtr mediaPlayer);

        /// <summary>
        /// 释放媒体播放器对象的引用计数
        /// </summary>
        /// <param name="mediaPlayer">媒体播放器</param>
        [DllImport("libvlc")]
        internal static extern void libvlc_media_player_release(IntPtr mediaPlayer);

        /// <summary>
        /// 减少媒体描述符对象的引用计数
        /// </summary>
        /// <param name="media">媒体指针</param>
        [DllImport("libvlc")]
        internal static extern void libvlc_media_release(IntPtr media);

        /// <summary>
        /// 设置媒体播放器将使用的媒体
        /// </summary>
        /// <param name="mediaPlayer">媒体播放器</param>
        /// <param name="media">媒体指针</param>
        [DllImport("libvlc")]
        internal static extern void libvlc_media_player_set_media(IntPtr mediaPlayer, IntPtr media);

        /// <summary>
        /// 停止播放
        /// </summary>
        /// <param name="mediaPlayer">媒体播放器</param>
        [DllImport("libvlc")]
        internal static extern void libvlc_media_player_stop(IntPtr mediaPlayer);

        #endregion

        #region 视频格式与回调

        /// <summary>
        /// 设置解码的视频色度和尺寸。
        /// 只能与libvlc_video_set_callbacks结合使用，与libvlc_video_set_format_callbacks互斥。
        /// </summary>
        /// <param name="mediaPlayer">媒体播放器</param>
        /// <param name="chroma">标识色度的四字符字符串（例如"RV24"、"RV32"或"YUYV"）</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <param name="pitch">行间距（每行字节数）</param>
        [DllImport("libvlc")]
        internal static extern void libvlc_video_set_format(IntPtr mediaPlayer, string chroma, uint width, uint height, uint pitch);

        /// <summary>
        /// 设置回调和私有数据，将解码后的视频渲染到内存中的自定义区域。
        /// 使用libvlc_video_set_format或libvlc_video_set_format_callbacks配置解码格式。
        /// </summary>
        /// <param name="mediaPlayer">媒体播放器</param>
        /// <param name="_lock">锁定回调</param>
        /// <param name="_unlock">解锁回调</param>
        /// <param name="_display">显示回调</param>
        /// <param name="_opaque">私有数据指针</param>
        [DllImport("libvlc")]
        internal static extern void libvlc_video_set_callbacks(IntPtr mediaPlayer, LockCB _lock, UnlockCB _unlock, DisplayCB _display, IntPtr _opaque);

        #endregion

        #region 播放状态控制

        /// <summary>
        /// 检查媒体是否正在播放
        /// </summary>
        /// <param name="mediaPlayer">媒体播放器</param>
        /// <returns>如果正在播放则为true，否则为false</returns>
        [DllImport("libvlc")]
        internal static extern bool libvlc_media_player_is_playing(IntPtr mediaPlayer);

        /// <summary>
        /// 设置媒体暂停状态
        /// </summary>
        /// <param name="mediaPlayer">媒体播放器</param>
        /// <param name="doPause">0表示播放，1表示暂停</param>
        [DllImport("libvlc")]
        internal static extern void libvlc_media_player_set_pause(IntPtr mediaPlayer, int doPause);

        #endregion
    }
}
