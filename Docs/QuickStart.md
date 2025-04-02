# MSO-Player 快速入门指南

本指南将帮助您快速上手使用MSO-Player插件，实现普通视频和全景视频的播放功能。

## 目录
- [安装说明](#安装说明)
- [基本视频播放](#基本视频播放)
- [全景视频播放](#360度全景视频播放)
- [常见问题](#常见问题)

## 安装说明

### 前提条件
- Unity 2019.4 或更高版本
- 支持的平台：Windows、macOS、Linux、Android、iOS

### 安装步骤
1. 下载MSO-Player包
2. 将整个MSO-Player文件夹导入您的Unity项目中
3. 确保项目中已包含libVLC相关dll文件（位于Plugins文件夹）
4. 如果需要支持移动平台，请确保配置相应的平台设置

## 基本视频播放

### 创建普通视频播放器

1. **创建UI画布**
   - 在层级窗口中，右键点击 -> UI -> Canvas，创建一个新的UI画布
   - 在画布中，右键点击 -> UI -> Raw Image，创建一个RawImage组件

2. **添加MediaPlayer组件**
   - 选中刚创建的RawImage对象
   - 在Inspector窗口中点击"Add Component"
   - 搜索并添加"MediaPlayer"组件

3. **配置MediaPlayer组件**
   - 在MediaPlayer组件中设置以下参数：
     - **URL**：视频文件路径或流媒体地址
     - **Width/Height**：视频分辨率（可设为0以使用源视频分辨率）
     - **Mute**：是否静音
     - **PlayOnStart**：是否自动播放

4. **测试播放**
   - 运行场景
   - 如果设置了自动播放，视频应该会自动开始
   - 您也可以使用编辑器中的播放、暂停和停止按钮控制视频

### 代码控制视频播放

```csharp
using UnityEngine;
using yan.libvlc;

public class VideoController : MonoBehaviour
{
    private MediaPlayer mediaPlayer;

    void Start()
    {
        // 获取MediaPlayer组件
        mediaPlayer = GetComponent<MediaPlayer>();
        
        // 设置视频URL
        mediaPlayer.SetUrl("https://example.com/video.mp4");
    }

    public void PlayVideo()
    {
        mediaPlayer.Play();
    }

    public void PauseVideo()
    {
        mediaPlayer.Pause();
    }

    public void StopVideo()
    {
        mediaPlayer.Stop();
    }

    // 切换到新视频
    public void ChangeVideo(string newUrl)
    {
        mediaPlayer.SetUrl(newUrl, true); // 第二个参数表示是否自动播放
    }
}
```

## 360度全景视频播放

### 创建全景视频播放器

1. **创建球体**
   - 在层级窗口中，右键点击 -> 3D Object -> Sphere，创建一个新的球体
   - 建议设置合适的球体细分度（推荐32-64）以获得更好的显示效果

2. **添加MediaPlayer360组件**
   - 选中刚创建的Sphere对象
   - 在Inspector窗口中点击"Add Component"
   - 搜索并添加"MediaPlayer360"组件

3. **设置全景材质和相机**
   - 在MediaPlayer360编辑器中，点击"设置全景材质"按钮
   - 点击"翻转球体法线"按钮（确保从球体内部观看）
   - 点击"在球体中心创建相机"按钮

4. **配置MediaPlayer360组件**
   - 设置以下参数：
     - **URL**：360度全景视频文件路径或流媒体地址
     - **Width/Height**：视频分辨率（推荐高分辨率，如3840x1920）
     - **Mute**：是否静音
     - **PlayOnStart**：是否自动播放

5. **测试全景视频**
   - 运行场景
   - 使用鼠标拖拽或触摸屏幕来改变视角
   - 在支持陀螺仪的设备上，可以通过移动设备来改变视角

### 调整全景视频方向
不同来源的全景视频可能需要不同的方向调整：

1. **视频上下颠倒**
   - 使用"垂直翻转"按钮或设置旋转为180°

2. **视频左右颠倒**
   - 使用"水平翻转"按钮

3. **视频需要旋转**
   - 使用旋转控制选择适当的角度：
     - 顺时针90°
     - 180°
     - 逆时针90°

### 代码控制全景视频

```csharp
using UnityEngine;
using yan.libvlc;

public class PanoramaController : MonoBehaviour
{
    private MediaPlayer360 mediaPlayer;

    void Start()
    {
        // 获取MediaPlayer360组件
        mediaPlayer = GetComponent<MediaPlayer360>();
        
        // 设置视频URL
        mediaPlayer.SetUrl("https://example.com/360video.mp4");
        
        // 调整视频方向（如果需要）
        mediaPlayer.SetTextureRotation(MediaPlayer360.TextureRotation.CW_180);
    }

    public void AdjustVideoOrientation()
    {
        // 水平翻转
        mediaPlayer.SetHorizontalFlip(true);
        
        // 垂直翻转
        mediaPlayer.SetVerticalFlip(true);
        
        // 设置旋转
        mediaPlayer.SetTextureRotation(MediaPlayer360.TextureRotation.CW_90);
    }
}
```

## 常见问题

### 视频无法播放
1. 确认libVLC的dll文件已正确导入
2. 检查视频URL是否有效
3. 某些流媒体格式可能需要适当的网络权限和设置

### 全景视频方向错误
1. 使用提供的翻转和旋转工具尝试不同的组合
2. 常见的方向修正组合：
   - 上下颠倒：垂直翻转或旋转180°
   - 左右颠倒：水平翻转
   - 需要旋转：选择适当的旋转角度

### 在移动设备上性能问题
1. 降低全景视频分辨率
2. 减少球体的多边形数量
3. 考虑使用更高效的编码格式（如H.264或H.265）

### 平台特定问题
1. Android：确保添加了适当的权限（如网络访问权限）
2. iOS：检查App Transport Security设置
3. WebGL：可能受到浏览器安全策略限制

---

有任何问题或建议，请在GitHub Issues中提出，或联系项目维护者。 