<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->

## 📑 Table of Contents

- [🎥 Unity MSO Player](#-unity-mso-player)
  - [Core Features](#core-features)
  - [🚀 Quick Start](#-quick-start)
  - [📚 Documentation](#-documentation)
  - [📝 License](#-license)

<!-- END doctoc generated TOC please keep comment here to allow auto update -->

# MSO-Player

<div align="center">
  <img src="Docs/Image/MSO-Player_logo.png" alt="MSO-Player Logo" width="200" />
  <h3>基于libVLC的Unity视频播放解决方案</h3>
  <p>支持2D视频和360度全景视频播放的Unity插件</p>
  <p><a href="README.md">🌏 中文</a> | <a href="README_EN.md">🌟 English</a></p>
</div>

## 📋 功能概述

MSO-Player是一个为Unity开发的强大视频播放解决方案，基于libVLC库构建，提供了丰富的功能和卓越的性能：

### 演示
![基本功能演示](Docs/Video/demo.gif)

### 核心特性
- ✅ **普通视频播放**：在UI上或3D物体上播放常规视频
- ✅ **360度全景视频**：沉浸式全景视频体验，支持鼠标/触摸/陀螺仪控制
- ✅ **多种格式支持**：基于libVLC，几乎支持所有流行的视频格式和流媒体协议
- ✅ **流媒体支持**：RTSP、RTMP、HTTP等流媒体协议
- ✅ **全方位方向调整**：支持视频翻转、旋转，轻松适配各种源视频
- ✅ **超高性能播放**：针对移动设备优化的高性能视频渲染
- ✅ **多播放线路**：支持多条播放线路，自动切换最佳线路
- ✅ **高级错误恢复**：智能错误检测和自动恢复机制
- ✅ **增强内存管理**：优化的纹理管理和内存使用
- ✅ **实时渲染优化**：高效的视频帧处理和渲染
- ✅ **自动画质切换**：根据网络状况自动调整视频质量
- ✅ **增强稳定性**：改进的错误处理和播放稳定性

## 🚀 Quick Start

### Requirements
- Unity 2019.4 or later
- Supported platforms: Windows, macOS, Linux, Android, iOS

### Installation
1. Import the MSO-Player folder into your Unity project
2. Ensure libVLC related DLL files are included in your project (located in the Plugins folder)

### Basic Usage - Standard Video
1. Create a UI object with a RawImage component
2. Add the `MediaPlayer` component
3. Set the video URL (local file or streaming link)
4. Click the play button or call the `Play()` method

```csharp
// Code example - Controlling video playback
MediaPlayer player = GetComponent<MediaPlayer>();
player.SetUrl("https://example.com/video.mp4", true); // Set URL and autoplay
```

### Basic Usage - 360° Panoramic Video
1. Create a sphere object
2. Add the `MediaPlayer360` component
3. Use the editor tools to set appropriate materials and camera
4. Set the panoramic video URL and play

```csharp
// Code example - Controlling panoramic video playback
MediaPlayer360 player = GetComponent<MediaPlayer360>();
player.SetUrl("https://example.com/panorama.mp4", true);
player.SetTextureRotation(MediaPlayer360.TextureRotation.CW_90); // Adjust video orientation
```

## 📚 Key Components

### MediaPlayer
Standard video player component for playing videos on a UI RawImage.

**Main Properties:**
- `URL`: Video source address
- `Width/Height`: Video resolution
- `Mute`: Whether to mute audio
- `PlayOnStart`: Whether to play automatically

**Main Methods:**
- `Play()`: Start playback
- `Pause()`: Pause/resume playback
- `Stop()`: Stop playback
- `SetUrl(string url, bool autoPlay)`: Set a new media source

### MediaPlayer360
Panoramic video player component for playing 360° videos on a sphere.

**Main Properties:**
- All properties inherited from MediaPlayer
- `FlipY`: Enable/disable Y-axis flipping for 360° videos

**Main Methods:**
- All methods inherited from MediaPlayer
- `SetUrl(string url, bool autoPlay)`: Set a new media source
- `Play()`: Start playback
- `Pause()`: Pause/resume playback
- `Stop()`: Stop playback
- `Refresh()`: Refresh the current media

### CameraController360
Component for controlling the 360° panoramic camera, supporting multiple input methods.

**Main Features:**
- Mouse drag control
- Touchscreen control
- Device gyroscope control
- Smooth rotation transitions

## 📝 Use Cases

### Video Stream Monitoring
```csharp
// Real-time display of RTSP camera stream
MediaPlayer player = GetComponent<MediaPlayer>();
player.SetUrl("rtsp://admin:password@192.168.1.100:554/stream");
player.Play();
```

### VR Panoramic Experience
```csharp
// Create interactive 360° environment
MediaPlayer360 player = GetComponent<MediaPlayer360>();
player.SetUrl("https://example.com/360tour.mp4");
player.SetTextureRotation(MediaPlayer360.TextureRotation.CW_180); // Adapt to video orientation
```

## 🔌 Dependencies

- [LibVLC](https://www.videolan.org/vlc/libvlc.html) - Video decoding and processing
- Unity UI System - For video rendering and interaction

## 📋 Notes

1. **Performance Considerations**: Panoramic video resolution has a significant impact on performance; please adjust appropriately based on the target platform
2. **Platform-Specific Settings**: Check platform-specific settings and permissions before publishing on mobile platforms
3. **Video Orientation Issues**: 360° videos from different sources may require different flip/rotation settings

## 📄 License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## 📞 Contact & Support

- Issue reporting: Please use GitHub Issues
- Contact the author: [873438526@qq.com]

---

<div align="center">
  <p>If you like this project, please consider giving it a ⭐</p>
</div>
