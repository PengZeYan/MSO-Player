# MSO-Player

<div align="center">
  <img src="Docs/Images/mso-player-logo.png" alt="MSO-Player Logo" width="200" />
  <h3>基于libVLC的Unity视频播放解决方案</h3>
  <p>支持2D视频和360度全景视频播放的Unity插件</p>
</div>

## 📋 功能概述

MSO-Player是一个为Unity开发的强大视频播放解决方案，基于libVLC库构建，提供了丰富的功能和卓越的性能：

### 核心特性
- ✅ **普通视频播放**：在UI上或3D物体上播放常规视频
- ✅ **360度全景视频**：沉浸式全景视频体验，支持鼠标/触摸/陀螺仪控制
- ✅ **多种格式支持**：基于libVLC，几乎支持所有流行的视频格式和流媒体协议
- ✅ **流媒体支持**：RTSP、RTMP、HTTP等流媒体协议
- ✅ **全方位方向调整**：支持视频翻转、旋转，轻松适配各种源视频

## 🚀 快速入门

### 安装要求
- Unity 2019.4 或更高版本
- 支持的平台：Windows、macOS、Linux、Android、iOS

### 安装步骤
1. 将 MSO-Player 文件夹导入您的 Unity 项目
2. 确保项目中已包含 libVLC 相关 dll 文件（位于 Plugins 文件夹）

### 基本使用 - 普通视频
1. 创建一个带有 RawImage 组件的 UI 对象
2. 添加 `MediaPlayer` 组件
3. 设置视频 URL（本地文件或流媒体链接）
4. 点击播放按钮或调用 `Play()` 方法

```csharp
// 代码示例 - 控制视频播放
MediaPlayer player = GetComponent<MediaPlayer>();
player.SetUrl("https://example.com/video.mp4", true); // 设置URL并自动播放
```

### 基本使用 - 360度全景视频
1. 创建一个球体物体
2. 添加 `MediaPlayer360` 组件
3. 使用编辑器工具设置适当的材质和相机
4. 设置全景视频 URL 并播放

```csharp
// 代码示例 - 控制全景视频播放
MediaPlayer360 player = GetComponent<MediaPlayer360>();
player.SetUrl("https://example.com/panorama.mp4", true);
player.SetTextureRotation(MediaPlayer360.TextureRotation.CW_90); // 调整视频方向
```

## 📚 关键组件

### MediaPlayer
标准视频播放器组件，用于在UI的RawImage上播放视频。

**主要属性：**
- `URL`: 视频源地址
- `Width/Height`: 视频分辨率
- `Mute`: 是否静音
- `PlayOnStart`: 是否自动播放

**主要方法：**
- `Play()`: 开始播放
- `Pause()`: 暂停/恢复播放
- `Stop()`: 停止播放
- `SetUrl(string url, bool autoPlay)`: 设置新的媒体源

### MediaPlayer360
全景视频播放器组件，用于在球体上播放360度视频。

**主要属性：**
- 继承自MediaPlayer的所有属性
- `FlipHorizontal/FlipVertical`: 视频翻转设置
- `TextureRotation`: 视频旋转角度

**主要方法：**
- 继承自MediaPlayer的所有方法
- `SetHorizontalFlip(bool)`: 设置水平翻转
- `SetVerticalFlip(bool)`: 设置垂直翻转
- `SetTextureRotation(TextureRotation)`: 设置视频旋转角度

### CameraController360
用于控制360全景相机的组件，支持多种输入方式。

**主要特性：**
- 鼠标拖拽控制
- 触摸屏幕控制
- 设备陀螺仪控制
- 平滑旋转过渡

## 📝 使用案例

### 视频流监控
```csharp
// 实时显示RTSP摄像头流
MediaPlayer player = GetComponent<MediaPlayer>();
player.SetUrl("rtsp://admin:password@192.168.1.100:554/stream");
player.Play();
```

### VR全景体验
```csharp
// 创建可交互的360度环境
MediaPlayer360 player = GetComponent<MediaPlayer360>();
player.SetUrl("https://example.com/360tour.mp4");
player.SetTextureRotation(MediaPlayer360.TextureRotation.CW_180); // 适配视频方向
```

## 🔌 依赖项

- [LibVLC](https://www.videolan.org/vlc/libvlc.html) - 视频解码和处理
- Unity UI System - 用于视频渲染和交互

## 📋 注意事项

1. **性能考虑**：全景视频分辨率对性能影响较大，请根据目标平台适当调整
2. **平台特定设置**：在移动平台上发布前，请检查平台特定的设置和权限
3. **视频方向问题**：不同来源的360视频可能需要不同的翻转/旋转设置

## 📄 许可证

本项目采用 MIT 许可证，详情请参阅 [LICENSE](LICENSE) 文件。

## 📞 联系与支持

- 问题报告：请使用 GitHub Issues
- 联系作者：[873438526@qq.com]

---

<div align="center">
  <p>如果您喜欢这个项目，请考虑给它一个⭐</p>
</div> "# MSO-Player" 
