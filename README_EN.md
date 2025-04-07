<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->

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

# MSO-Player

A powerful Unity video player plugin that supports standard video playback and 360° panoramic video playback, based on LibVLC.

<p><a href="README.md">🌏 中文</a> | <a href="README_EN.md">🌟 English</a></p>

## 📋 Features

- **Standard Video Playback**
  - Support for common video formats
  - Basic playback controls (play, pause, stop)
  - Volume control
  - Playback progress control

- **360° Panoramic Video**
  - Support for 360° video playback
  - Full directional adjustment
  - Y-axis flip support
  - Optimized rendering performance

- **Multiple Format Support**
  - MP4, AVI, MKV, MOV, etc.
  - Various codec support
  - High compatibility

- **Streaming Support**
  - RTMP, RTSP, HTTP streaming
  - Adaptive bitrate streaming
  - Network buffering optimization

- **Performance Optimization**
  - Optimized texture management
  - Improved memory usage
  - Enhanced playback stability

## 🚀 Quick Start

1. **Import the Package**
   - Download the latest release
   - Import into your Unity project
   - Ensure all dependencies are properly imported

2. **Basic Setup**
   ```csharp
   // Add MediaPlayer360 component to your camera
   MediaPlayer360 player = cameraObject.AddComponent<MediaPlayer360>();
   
   // Set video path
   player.VideoPath = "path/to/your/video.mp4";
   
   // Start playback
   player.Play();
   ```

3. **360° Video Setup**
   ```csharp
   // Enable 360° mode
   player.Is360Video = true;
   
   // Set initial orientation
   player.InitialOrientation = new Vector3(0, 0, 0);
   
   // Enable Y-axis flip if needed
   player.FlipY = true;
   ```

## 📚 Key Components

### MediaPlayer360
The core component for video playback, supporting both standard and 360° video playback.

#### Main Properties
| Property | Type | Description |
|----------|------|-------------|
| `VideoPath` | string | Video file path or URL |
| `IsPlaying` | bool | Current playback state |
| `Volume` | float | Playback volume (0-1) |
| `Is360Video` | bool | Whether it's a 360° video |
| `FlipY` | bool | Enable/disable Y-axis flip |
| `InitialOrientation` | Vector3 | Initial camera orientation |

#### Main Methods
| Method | Description |
|--------|-------------|
| `Play()` | Start playback |
| `Pause()` | Pause playback |
| `Stop()` | Stop playback |
| `SetVolume(float)` | Set playback volume |
| `SetVerticalFlip(bool)` | Enable/disable vertical flip |

## 📝 Usage Examples

### Basic Video Playback
```csharp
public class VideoPlayerExample : MonoBehaviour
{
    private MediaPlayer360 player;

    void Start()
    {
        player = gameObject.AddComponent<MediaPlayer360>();
        player.VideoPath = "path/to/video.mp4";
        player.Play();
    }
}
```

### 360° Video Playback
```csharp
public class Video360Example : MonoBehaviour
{
    private MediaPlayer360 player;

    void Start()
    {
        player = gameObject.AddComponent<MediaPlayer360>();
        player.VideoPath = "path/to/360video.mp4";
        player.Is360Video = true;
        player.FlipY = true;
        player.Play();
    }
}
```

## 🔌 Dependencies

- Unity 2019.4 or later
- LibVLC
- .NET Framework 4.7.1 or later

## 📋 Notes

1. **Performance Optimization**
   - Use appropriate video resolution
   - Enable hardware acceleration when possible
   - Monitor memory usage during playback

2. **360° Video Considerations**
   - Ensure correct video format
   - Test orientation controls
   - Verify Y-axis flip functionality

3. **Memory Management**
   - Properly dispose of resources
   - Monitor texture memory usage
   - Handle video unloading appropriately

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 📞 Contact & Support

- **GitHub Issues**: [Submit Issues](https://github.com/PengZeYan/MSO-Player/issues)
- **Email**: [pengzeyan@outlook.com](mailto:pengzeyan@outlook.com)
- **Documentation**: [Wiki](https://github.com/PengZeYan/MSO-Player/wiki)
