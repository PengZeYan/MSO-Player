<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->
# MSO-Player

<div align="center">
  <img src="Docs/Image/MSO-Player_logo.png" alt="MSO-Player Logo" width="200" />
  <h3>Unity Video Playback Solution Based on libVLC</h3>
  <p>A Unity plugin that supports 2D video and 360° panoramic video playback</p>
  <p><a href="README.md">🌏 中文</a> | <a href="README_EN.md">🌟 English</a></p>
</div>

## 📑 Table of Contents

- [🎥 MSO-Player](#mso-player)
  - [📋 Features](#-features)
  - [🚀 Quick Start](#-quick-start)
  - [📚 Key Components](#-key-components)
  - [📝 Usage Examples](#-usage-examples)
  - [🔌 Dependencies](#-dependencies)
  - [📋 Notes](#-notes)
  - [📄 License](#-license)
  - [📞 Contact & Support](#-contact--support)

<!-- END doctoc generated TOC please keep comment here to allow auto update -->

## 📋 Features

MSO-Player is a powerful video playback solution for Unity, built on the libVLC library, offering rich features and excellent performance:

### Demo
![Basic Features Demo](Docs/Video/demo.gif)

![Android Platform Demo](Docs/Image/AndroidPlayer.png)

### Core Features
- ✅ **Standard Video Playback**: Play regular videos on UI or 3D objects
- ✅ **360° Panoramic Video**: Immersive panoramic video experience with mouse/touch/gyroscope control
- ✅ **Multiple Format Support**: Based on libVLC, supports almost all popular video formats and streaming protocols
- ✅ **Streaming Support**: RTSP, RTMP, HTTP and other streaming protocols
- ✅ **Full Directional Adjustment**: Supports video flipping and rotation for easy adaptation to various source videos
- ✅ **Ultra-High Performance Playback**: High-performance video rendering optimized for mobile devices
- ✅ **Multiple Playback Routes**: Supports multiple playback routes with automatic switching to the best route
- ✅ **Advanced Error Recovery**: Intelligent error detection and automatic recovery mechanism
- ✅ **Enhanced Memory Management**: Optimized texture management and memory usage
- ✅ **Real-time Rendering Optimization**: Efficient video frame processing and rendering
- ✅ **Automatic Quality Switching**: Automatically adjusts video quality based on network conditions
- ✅ **Enhanced Stability**: Improved error handling and playback stability
- ✅ **Android Platform Support**: Full support for Android devices, including basic hardware acceleration
- ✅ **Hardware Decoding Acceleration**: Support for GPU hardware decoding, enhancing playback performance and reducing power consumption
- ✅ **Specific Device Optimization**: Special optimizations for specific Android devices like Xiaomi and Samsung
- ✅ **Low-Performance Device Adaptation**: Special optimizations for low-configuration phones to ensure smooth playback experience

## 🚀 Quick Start

### Requirements
- Unity 2019.4 or later
- Supported platforms: Windows, Linux, Android

### Installation
1. Import the MSO-Player folder into your Unity project
2. Ensure libVLC related DLL files are included in your project (located in the Plugins folder)
   - Windows: Plugins/x86_64/libvlc/
   - Android: Plugins/Android/

> **Note**: Large library files for Android platform (`libvlc.so` and `libmla.so`) exceed GitHub's file size limit and need to be downloaded separately from the [Releases page](https://github.com/PengZeYan/MSO-Player/releases). For detailed instructions, please check the [Android Plugin README](Assets/MSO-Player/Plugins/Android/README.md).

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

### Android Platform Specific Notes
When using on Android platform, ensure to add the following permissions in AndroidManifest.xml:
```xml
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />
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
- `RotationMode`: Video rotation mode (applicable for videos from different sources)

**Main Methods:**
- All methods inherited from MediaPlayer
- `SetUrl(string url, bool autoPlay)`: Set a new media source
- `Play()`: Start playback
- `Pause()`: Pause/resume playback
- `Stop()`: Stop playback
- `Refresh()`: Refresh the current media
- `SetTextureRotation(TextureRotation rotation)`: Set video texture rotation

### CameraController360
Component for controlling the 360° panoramic camera, supporting multiple input methods.

**Main Features:**
- Mouse drag control
- Touchscreen control
- Device gyroscope control
- Smooth rotation transitions

## 📝 Usage Examples

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

### Video Display in Android Applications
```csharp
// Play video in an Android app
MediaPlayer player = GetComponent<MediaPlayer>();
player.SetUrl("file:///storage/emulated/0/DCIM/Camera/video.mp4");
player.Play();
```

## 🔌 Dependencies

- [LibVLC](https://www.videolan.org/vlc/libvlc.html) - Video decoding and processing
- Unity UI System - For video rendering and interaction

## 📋 Notes

1. **Performance Considerations**: Panoramic video resolution has a significant impact on performance; please adjust appropriately based on the target platform
2. **Platform-Specific Settings**: Check platform-specific settings and permissions before publishing on mobile platforms
3. **Video Orientation Issues**: 360° videos from different sources may require different flip/rotation settings
4. **Android Compatibility**: Compatibility has been optimized for different Android devices, but further adjustments may be needed on extremely low-configuration devices

## 📄 License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## 📞 Contact & Support

- Issue reporting: Please use GitHub Issues
- Contact the author: [873438526@qq.com]

---

<div align="center">
  <p>If you like this project, please consider giving it a ⭐</p>
</div>
