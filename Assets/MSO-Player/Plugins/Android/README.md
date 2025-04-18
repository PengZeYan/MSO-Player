# Android 插件大型库文件

由于以下文件超过了GitHub的文件大小限制，请从发布页面下载：

- `libs/arm64-v8a/libvlc.so` (214.89 MB)
- `libs/armeabi-v7a/libvlc.so` (235.79 MB)
- `libs/arm64-v8a/libmla.so` (56.10 MB)

## 下载说明

1. 访问项目的Releases页面： https://github.com/PengZeYan/MSO-Player/releases
2. 下载最新版本的 `android-libs.zip` 文件
3. 解压并将文件放置在对应的目录中：
   - `libvlc.so` (arm64-v8a) → `Assets/MSO-Player/Plugins/Android/libs/arm64-v8a/`
   - `libvlc.so` (armeabi-v7a) → `Assets/MSO-Player/Plugins/Android/libs/armeabi-v7a/`
   - `libmla.so` → `Assets/MSO-Player/Plugins/Android/libs/arm64-v8a/`

## 文件说明

这些是VLC媒体播放器的Android平台原生库文件，用于支持视频的解码和播放功能：

- `libvlc.so`: VLC媒体播放器的核心库
- `libmla.so`: 多媒体音频库，用于音频处理

如有问题，请提交issue到项目仓库。 