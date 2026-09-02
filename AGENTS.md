# AGENTS.md

## 适用范围与优先级

本文件适用于仓库根目录及其全部子目录。若某个子目录以后出现更具体的 `AGENTS.md`，则该文件只覆盖其所在目录及后代；系统指令、开发者指令和用户当前请求始终高于本文件。

所有工作都应遵循以下基本原则：

- 只修改完成当前任务所必需的文件，不顺手重构无关代码。
- 开始前先读取 `git status --short` 和相关文件的现有差异，保留用户的未提交修改。
- 诊断、审查和“读取代码”类请求默认只读；只有明确要求实现或修复时才写文件。
- 不以“能够编译”代替 Unity Play Mode、播放器构建或真实媒体播放验证。
- 不隐瞒未执行的验证；最终说明必须区分已验证、静态推断和待验证内容。

## 项目定位

MSO-Player 是一个 MIT 许可的 Unity/libVLC 播放器项目，提供：

- `RawImage` 上的普通 2D 视频播放。
- `MeshRenderer`/球体材质上的 360 度全景视频播放。
- 本地媒体以及 HTTP、HLS、RTSP、RTMP 等 LibVLC 可处理的媒体源。
- 播放、暂停、停止、进度、音量、切流、状态事件和播放器对象池。
- Windows x86_64 与 Android ARMv7/ARM64 原生依赖。

Unity 编辑器版本以已提交的 `ProjectSettings/ProjectVersion.txt` 为准。截至本文件创建时，Git 基线为 `2022.3.33f1c1`。若工作区文件显示其他版本，应先视为本地未提交的编辑器迁移，不能擅自纳入提交。

仓库内置 Windows `libvlc.dll` 的文件版本为 3.0.19。修改 P/Invoke、原生插件或构建复制逻辑时，必须以实际打包版本的 ABI 为准。

## 事实来源

按以下顺序判断项目当前事实：

1. 当前源码、Unity YAML、插件导入设置和 Git 差异。
2. `ProjectSettings/`、`Packages/manifest.json` 与 `Packages/packages-lock.json`。
3. `README.md`、`README_EN.md` 和 `Docs/QuickStart.md`。
4. 运行日志和历史说明。

文档中的平台声明不等于已打包支持。当前仓库实际提供 Windows x86_64 DLL 和 Android ARMv7/ARM64 `.so`；没有看到可直接发布的 Linux、macOS、iOS 或 WebGL 原生实现。只有在补齐对应原生库、导入设置、构建流程并完成目标平台运行验证后，才可扩大支持声明。特别注意：Windows DLL 的 `.meta` 勾选其他平台不能让该 DLL 在那些平台运行。

`Assets/Player.log`、`Logs/` 和外部客户端导出的日志只能作为指定运行的证据，不能自动代表当前仓库、当前提交或当前设备状态。

## 仓库结构

- `Assets/MSO-Player/Scripts/Core/`
  - `LibVLCWrapper.cs`：LibVLC 3.x P/Invoke、枚举、结构体和回调委托。
  - `VlcMediaPlayer.cs`：原生实例、媒体、播放器、视频回调、RGB24 缓冲、切流和释放。
  - `ExtendedVlcPlayer.cs`：时间、位置、音量、直播判断和分辨率等扩展控制；通过核心层受控 API 访问播放器状态。
- `Assets/MSO-Player/Scripts/Platform/`
  - `MediaPlayer.cs`：面向 Unity UI 的 2D 播放组件。
  - `MediaPlayer360.cs`：面向球体/材质的全景播放组件。
  - `MediaPlayerAndroid.cs`：Android 参数、硬解与低内存处理。
  - `PlatformManager.cs`：平台和设备能力判断。
- `Assets/MSO-Player/Scripts/Pool/`：播放器池、启动器和预热逻辑。
- `Assets/MSO-Player/Scripts/PlayerControl/`：示例播放器 UI、进度和交互控制。
- `Assets/MSO-Player/Scripts/Utils/`：加载提示、360 相机和调试监视器。
- `Assets/MSO-Player/Editor/`：自定义 Inspector 和 Windows 构建后处理。
- `Assets/MSO-Player/Plugins/`：Windows 与 Android 原生依赖。二进制及其 `.meta` 都是发布链路的一部分。
- `Assets/MSO-Player/Prefab/`、`Assets/MSO-Player/Scene/`：示例 Prefab 和场景。
- `Assets/MSO-Player/Materials/`、`Assets/MSO-Player/Sprites/`：示例渲染和 UI 资源。
- `Docs/`、`README.md`、`README_EN.md`：用户文档。
- `Packages/com.unity.asset-store-tools/`：随项目放置的第三方/工具包代码。除非任务明确涉及它，否则不要修改。
- `Library/`、`Temp/`、`Obj/`、`Logs/`、`Build/`、`Builds/`、`UserSettings/`：Unity 生成目录，不得提交或作为源码修改目标。

项目自身目前没有独立 asmdef，也没有一套第一方自动化测试。Unity/IDE 生成的 `.sln` 和 `.csproj` 被忽略，不能当作稳定的仓库输入。

## 运行时数据流

正常的 2D 播放链路如下：

1. `MediaPlayer.SetUrl()`/`Play()` 取得或创建 `VlcMediaPlayer`。
2. `VlcMediaPlayer` 创建 `libvlc_instance_t`、`libvlc_media_t` 和 `libvlc_media_player_t`。
3. `libvlc_video_set_callbacks()` 注册 lock、unlock 和 display 回调，`libvlc_video_set_format()` 要求输出 `RV24`。
4. LibVLC 解码线程把一帧写入 `_imageIntPtr` 指向的非托管内存。
5. display 回调将帧复制到托管后备缓冲，并在锁内交换前后缓冲。
6. Unity 主线程的 `Update()` 调用 `CheckForImageUpdate()`，再用 `LoadRawTextureData()` 和 `Apply(false)` 上传到 `Texture2D`。
7. `RawImage.uvRect` 或材质纹理缩放负责垂直翻转。

音频由 LibVLC 直接管理，不经过 Unity `AudioSource`。播放器状态由协程轮询 LibVLC 状态并转换为 Unity 事件。普通播放器可从 `MediaPlayerPool` 复用；360 播放器当前直接持有和释放自己的核心实例。

## 核心不变量

### 原生互操作

- P/Invoke 函数名、参数宽度、返回值、结构体字段顺序和回调签名必须与仓库实际 LibVLC 版本匹配。
- 不凭记忆修改 ABI。涉及 ABI 时，应核对对应版本的官方头文件或导出符号，并至少验证 Windows 和一个 Android ABI。
- 原生回调委托必须在播放器整个原生生命周期中保持强引用；不得把短生命周期 lambda 直接交给 LibVLC。
- `opaque`、`GCHandle` 和播放器实例映射必须同生共灭。解除映射或释放 `GCHandle` 前，应确保原生回调已经停止。
- 新增字符串互操作时明确字符编码和所有权；不得释放 LibVLC 所有的指针，也不得遗忘释放调用方所有的分配。

### 线程模型

- Unity 对象、场景、组件、材质、纹理、协程和 `Time` API 只在 Unity 主线程使用。
- LibVLC 视频回调和轨道读取线程不能直接修改 Unity 对象。
- 跨线程共享的字典、标志、媒体指针和缓冲区必须具备明确的同步或所有权。`volatile` 不能替代复合操作所需的锁。
- 不在持锁期间调用可能阻塞、回调用户代码或进入 Unity 的操作。
- 后台线程必须能取消，并在释放相关原生指针和托管缓冲前确认退出；不要依赖后台线程随进程退出。
- 主线程分发器不得假定托管线程 ID 永远等于 1。若修改这一部分，应在 Unity 启动时捕获真实主线程或同步上下文。

### 帧缓冲与纹理

- `RV24` 固定为每像素 3 字节，pitch 为 `width * 3`。缓冲区大小、pitch、纹理格式和上传长度必须一致。
- 对宽高和乘法做合法性/溢出检查，拒绝零值、负值和不合理的大分辨率。
- 分辨率发生变化时，必须作为一个受控生命周期操作同时更新：托管前后缓冲、非托管帧内存、LibVLC 视频格式和 Unity 纹理。不得让回调写入旧尺寸内存。
- 回调热路径和 `Update()` 中避免逐帧分配、LINQ、反射和重复日志。
- `Texture2D.Apply(false)` 仍有 CPU 到 GPU 上传成本。性能改动必须用 Profiler/目标设备数据验证，不能只凭代码注释声称“零开销”或“超高性能”。
- CPU 垂直翻转会原地修改共享帧数组；若其他消费者也读取该帧，必须先明确所有权。

### 媒体与资源所有权

- 每次成功创建 `libvlc_media_t` 都必须有清晰的释放点。
- 切换 URL 时要保持“播放器当前媒体、`_media` 字段、轨道元数据和扩展播放器指针”一致；旧媒体和轨道信息应在不再使用后及时释放。
- `libvlc_media_tracks_get()` 的每次成功返回都应与一次 `libvlc_media_tracks_release()` 配对，不能只保存最后一次返回值。
- 推荐的销毁顺序是：停止新的工作和恢复协程，停止播放，确保回调/工作线程静止，释放轨道结果，释放 media player，释放 media，释放 LibVLC 实例，释放非托管帧内存，最后移除实例映射和 `GCHandle`。
- `Dispose()` 必须幂等。释放后的实例不能再次进池或继续响应回调。
- Unity 资源使用 `Destroy()`，不要在运行时使用 `DestroyImmediate()`。

### 播放状态与事件

- 区分 `Play`、`Resume`、`Pause`、`TogglePause`、`Stop` 和 `Reload`；不要依赖模糊的“如果没在播放就切换”行为。
- 一个用户操作对应的 `OnPlayEvent`、`OnStopEvent` 和错误事件应只触发一次。修改播放流程时检查手动触发和状态监控是否重复。
- LibVLC 构造完成、`libvlc_media_player_play()` 返回成功、状态变为 Playing、收到首帧是四个不同阶段；日志和 UI 不能混为一谈。
- 网络流短暂处于 Opening/Buffering 不等于失败。恢复策略应有单实例互斥、退避、次数上限和取消条件。
- 不要从 `libvlc_errmsg()` 的全局/线程局部最后错误推断任意旧操作的失败原因；尽量在失败调用附近读取并记录上下文。

### 对象池

- 入池实例必须停止播放但仍保持可复用，且不能已经 `Dispose()`。
- 出池后应重置 URL、媒体、轨道、缓冲状态、静音/音量、错误计数和首帧时间等所有会跨使用者泄漏的状态。
- 池键必须覆盖影响实例兼容性的配置。若平台、色度、VLC 参数或硬解设置不同，应扩展池键或禁用复用。
- 活动列表和可用队列中同一实例只能出现一次。
- 清理池时不得销毁仍被组件、回调或后台线程使用的播放器。
- 对池的改动必须在多播放器场景中验证，而不只是单播放器示例。

## 平台约束

### Windows x86_64

- 原生源目录是 `Assets/MSO-Player/Plugins/x86_64/libvlc/`，其中包括 `libvlc.dll`、`libvlccore.dll` 和 `plugins/` 模块树。
- `BuildProcessor` 在 Windows 构建完成后复制整套目录到 `<Product>_Data/Plugins/x86_64/`。修改它时不得只验证 DLL 存在，还要验证插件模块目录和真实播放。
- 构建输出文件名不一定等于 `Application.productName + ".exe"`。处理输出路径时优先使用 `Path.GetDirectoryName(report.summary.outputPath)`，不要依赖字符串查找后直接 `Remove()`。
- 不应提交构建目录中的复制结果。

### Android

- 当前包含 `armeabi-v7a` 和 `arm64-v8a` 原生库；修改某个 ABI 时必须检查另一 ABI 是否仍可打包。
- 网络播放需要清单中的网络权限；本地文件访问还受 Android 版本和分区存储限制，不能只依赖旧的外部存储权限。
- `MediaPlayerAndroid` 的序列化缓存配置应真正参与生成参数。新增或修改参数时区分实例参数 `--option` 与媒体参数 `:option`，避免同一缓存项被多处冲突设置。
- MediaCodec、直接渲染和 LibVLC 内存视频回调可能相互影响。硬解参数的有效性必须以设备日志和首帧结果验证。
- 至少在一个 ARM64 真机上验证启动、首帧、切流、前后台切换、低内存处理和退出释放；模拟器结果不能覆盖真机硬解行为。

### 未验证平台

- WebGL 不能直接加载当前原生 LibVLC。
- macOS、iOS 和 Linux 需要各自的原生库、插件导入设置、加载路径、构建处理和运行验证。
- 文档、Inspector 或 `.meta` 中出现平台名字，不构成支持证据。

## 持续风险审查清单

以下不表示当前必然存在缺陷，而是该播放器最容易回归的边界。触及对应代码时必须显式评估：

- 构造、`Play()`、对象池复用和 URL 切换只能产生一次预期播放动作，事件不得由调用层手工重复触发。
- `UpdateUrl()`/`UpdateUrlSmooth()` 必须同步替换原生媒体、轨道读取任务和包装器引用；旧媒体的异步结果不得覆盖新媒体状态。
- 每次 `libvlc_media_tracks_get()` 成功调用都必须对应一次 `libvlc_media_tracks_release()`，媒体在线程读取期间必须保有独立引用。
- 静态播放器实例字典、帧缓冲区和 Dispose/LibVLC 回调之间必须保持并发保护，释放后不得接受新回调。
- 不得静默吞掉生命周期、切流、纹理更新和对象池异常；日志需带操作上下文，但不能输出完整签名 URL。
- 360 播放器同一时刻只能存在一个状态监控和一个恢复任务；禁用、销毁和恢复不能互相停止到半释放状态。
- Android 缓存值必须进入实例参数；修改 MediaCodec 参数后需复查其与 LibVLC 内存视频回调的兼容性。
- Prefab 脚本 GUID 必须与 `.meta` 一致；序列化字段重命名需保留 `FormerlySerializedAs`，并在 Unity 中检查 Missing Script 和字段迁移。
- Windows 构建后处理必须从实际输出文件名推导 `_Data` 目录，不得按 DLL 同名规则递归删除 LibVLC 插件。
- 示例场景可能含过期、私有或带签名的流地址。不要把它们当成稳定测试源，也不要在日志、提交信息或回复中传播完整地址。

## 工作流程

### 开始前

1. 运行 `git status --short`，再对相关文件运行 `git diff -- <paths>`。
2. 阅读本文件、相关源码、对应 `.meta`、Prefab/场景引用和必要文档。
3. 明确任务涉及 2D、360、对象池、Windows 构建还是 Android；不要用一个平台的结论代替另一个平台。
4. 识别媒体 URL、查询签名、摄像机账号和本机绝对路径等敏感内容，输出时做脱敏。
5. 如果要升级 Unity、LibVLC、Android ABI 或包依赖，先单独说明迁移范围和回退方式。

### 修改时

- 使用 UTF-8，保持现有换行风格和最小差异。
- 移动或新增 `Assets/` 下的 Unity 资源时同步保留/生成 `.meta`；不要手工复用其他资源的 GUID。
- 不编辑 Unity 生成的 `.sln`、`.csproj` 或 `Library/PackageCache`。
- 不因打开不同 Unity 版本而顺带提交 `ProjectVersion.txt`、`packages-lock.json`、场景重序列化或大批 `.meta` 变化。
- 不直接改写大型原生二进制。升级二进制时应记录来源、版本、许可证、支持 ABI 和校验值，并验证插件导入设置。
- 公共 API 改动要考虑已有场景、Prefab 和外部项目的源码兼容性；需要重命名序列化字段时使用 `FormerlySerializedAs` 并验证迁移。
- 修复应靠明确状态和所有权，不靠增加任意 `Sleep`、无限重试或吞异常。

### C# 风格

- 使用 4 空格缩进和 Allman 大括号风格，遵循所在文件已有命名约定。
- Unity 序列化字段使用 `private` + `[SerializeField]`；新增公共成员应有简洁 XML 文档。
- `Core` 里的私有字段当前使用 `_name`，Unity 组件多使用 `m_Name`；在同一文件中保持一致。
- 优先早返回和小型、职责明确的方法。
- 公共错误应包含操作、状态和可脱敏的上下文；不要记录凭据、完整签名 URL 或每帧刷屏。
- 不在每帧、原生回调或高频协程中制造可避免的 GC 分配。
- 若必须新增反射，应缓存元数据并说明为何不能使用明确接口；更推荐为核心层增加受控 API，逐步移除对私有字段名的依赖。

## 验证要求

验证强度应与改动风险匹配。仓库当前缺少第一方测试，因此不能声称“全部测试通过”，除非先添加并实际运行了测试。

### 静态检查

每次修改至少执行：

```powershell
git diff --check
git status --short
git diff -- AGENTS.md Assets Packages ProjectSettings README.md README_EN.md Docs
```

检查是否出现意外的 Unity 版本变化、锁文件变化、场景重序列化、生成文件、日志、构建产物、绝对路径或敏感媒体地址。

### Unity 编译

首选使用 `ProjectSettings/ProjectVersion.txt` 指定的编辑器打开项目并等待脚本编译完成。可用命令行时使用等价方式：

```powershell
& '<Unity.exe>' -batchmode -nographics -quit -projectPath (Get-Location).Path -logFile 'Logs/agent-compile.log'
```

Unity 退出码为 0 仍需检查日志中的 C# 编译错误、原生插件加载错误和异常。普通 `dotnet build` 只编译 Unity 生成工程的一部分，不能作为最终验证。

若新增测试，按所需平台运行 EditMode/PlayMode，并把结果写到忽略目录：

```powershell
& '<Unity.exe>' -batchmode -nographics -quit -projectPath (Get-Location).Path -runTests -testPlatform EditMode -testResults 'Logs/EditMode-results.xml' -logFile 'Logs/EditMode.log'
```

### 播放验收

涉及播放器核心、Unity 组件或生命周期的修改，至少用一个已知可用且不含秘密的测试媒体完成：

- 创建播放器、到达 Playing、收到首帧并显示正确颜色/方向。
- Pause/Resume、Stop、再次 Play。
- 切换 URL 后画面、状态、时长、进度和分辨率来自新媒体。
- GameObject Disable/Enable、场景切换、应用暂停/恢复后状态正确。
- 重复创建/销毁或入池/出池，不发生回调访问已释放对象。
- 无效 URL、超时和断流能给出一次明确错误，并受控恢复或停止。
- `OnPlayEvent`、`OnStopEvent` 和错误事件没有重复或遗漏。

涉及缓冲、线程或释放时，再执行长时间播放和反复切流，并观察 Unity Profiler、托管 GC、进程私有内存和原生崩溃日志。仅看到画面不能证明没有泄漏或竞态。

### 场景与平台验收矩阵

| 改动范围 | 最低验收 |
| --- | --- |
| P/Invoke/结构体/回调 | Unity 编译、Windows 首帧、回调与释放；Android 相关时增加真机验证 |
| 缓冲区/纹理上传 | 不同分辨率、切流、画面方向、Profiler 分配与内存稳定性 |
| 播放状态/事件 | Play/Pause/Stop/Ended/Error 状态序列和事件次数 |
| 对象池 | 单播放器与 `MultiPanel` 多播放器、反复入池出池、空闲清理 |
| 360 播放器 | 球体内部画面方向、材质生命周期、前后台与断流恢复 |
| Windows 构建处理 | 干净 Standalone 构建、完整 LibVLC 模块树、构建产物实际播放 |
| Android 参数/插件 | ARM64 真机打包安装、首帧、硬解日志、前后台和退出 |
| Prefab/Scene | Unity 中无 Missing Script、无意外 YAML 大改、序列化值可保留 |
| 文档/API 示例 | 中文和英文说明一致，示例可编译，平台声明有实测依据 |

## 文档与发布规则

- 面向用户的功能、API、平台或安装方式变化，应同步检查 `README.md`、`README_EN.md` 和 `Docs/QuickStart.md`。
- README 示例必须使用占位地址，不放真实摄像机凭据、访问令牌、签名查询串或内网详情。
- 不虚构下载量、用户数、性能百分比、平台兼容性或硬件加速效果。
- 新增第三方二进制、代码或素材时，核对许可证和再分发要求；项目 MIT 许可不会自动覆盖第三方组件。
- 发布前从干净检出验证 Unity 导入、示例场景、Windows 构建和计划支持的 Android ABI。

## 安全与隐私

- 媒体 URL 的用户信息和查询参数可能是凭据。日志中最多保留协议、脱敏主机和不敏感路径摘要。
- 未经用户明确要求，不主动访问示例场景中的远程流、摄像机或私有地址。
- 不提交 `Player.log`、崩溃转储、设备日志、构建输出或带本机用户名的绝对路径。
- 错误报告需要原始日志时，先生成脱敏副本，不直接传播整个日志。
- 原生插件升级不得从不明来源获取；记录可信来源并校验文件完整性。

## 完成检查清单

提交结果前逐项确认：

- 修改范围与用户请求一致，未覆盖既有未提交内容。
- 核心线程、回调、媒体和缓冲区所有权仍然闭合。
- Unity 资源及 `.meta` 配对正确，没有意外 GUID 变化。
- 没有生成目录、日志、构建产物、密钥或完整签名 URL 进入差异。
- `git diff --check` 通过，并人工审阅完整 diff。
- 已执行适合改动的 Unity 编译、Play Mode、构建或真机验证。
- 最终说明列出改了什么、验证了什么、哪些验证因环境限制仍未完成。
