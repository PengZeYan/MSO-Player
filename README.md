# MSO-Player

<div align="center">
  <img src="Docs/Image/MSO-Player_logo.png" alt="MSO-Player Logo" width="200" />
  <h3>Unity Video Player Solution Based on libVLC</h3>
  <p>A Unity plugin supporting both 2D video and 360° panoramic video playback</p>
  <p>[🌟 English](README.md) | [🌏 中文](README_CN.md)</p>
</div>

## 📋 Overview

MSO-Player is a powerful video playback solution for Unity, built on the libVLC library, offering rich features and excellent performance:

### Demo
![Basic Features Demo](Docs/Video/demo.gif)

### Core Features
- ✅ **Standard Video Playback**: Play regular videos on UI elements or 3D objects
- ✅ **360° Panoramic Video**: Immersive panoramic video experience with mouse/touch/gyroscope control
- ✅ **Multiple Format Support**: Based on libVLC, supporting almost all popular video formats and streaming protocols
- ✅ **Streaming Support**: RTSP, RTMP, HTTP and other streaming protocols
- ✅ **Full Directional Adjustment**: Support for video flipping and rotation to easily adapt to various source videos

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
- `FlipHorizontal/FlipVertical`: Video flip settings
- `TextureRotation`: Video rotation angle

**Main Methods:**
- All methods inherited from MediaPlayer
- `SetHorizontalFlip(bool)`: Set horizontal flip
- `SetVerticalFlip(bool)`: Set vertical flip
- `SetTextureRotation(TextureRotation)`: Set video rotation angle

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
