using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using yan.libvlc.Core;

namespace yan.libvlc
{
    /// <summary>
    /// VlcMediaPlayer对象池管理器，用于复用VlcMediaPlayer实例
    /// 实现了单例模式的对象池管理器
    /// 按照不同分辨率和静音设置对播放器进行分组管理
    /// 限制每种配置的最大池大小（MAX_POOL_SIZE_PER_KEY = 10）
    /// 实现了自动清理机制，闲置超过5分钟的实例会被自动释放
    /// 记录每个对象的使用情况和闲置时间
    /// </summary>
    public class MediaPlayerPool : MonoBehaviour
    {
        // 每个键最大缓存数量
        private const int MAX_POOL_SIZE_PER_KEY = 10;

        // 延长清理间隔，减少检查开销
        // 进一步延长清理间隔，减少运行时开销
        private const float AUTO_CLEANUP_INTERVAL = 300f; // 从120秒增加到300秒（5分钟）

        // 闲置超过此时间的对象将被清理（秒）
        private const float MAX_IDLE_TIME = 300f;

        [SerializeField, Tooltip("是否启用详细日志")]
        private bool enableDetailedLogs = false;

        [SerializeField, Tooltip("是否在获取实例时自动回收无效引用")]
        private bool autoCleanupInvalidReferences = true;

        // 启动时预热常用分辨率
        // 禁用启动预热，避免冷启动卡顿，请使用MediaPlayerPreloader在登录界面预热
        [SerializeField, Tooltip("是否在启动时预热对象池（不推荐，请使用MediaPlayerPreloader）")]
        private bool enablePrewarm = false;

        [SerializeField, Tooltip("预热的播放器数量")]
        private int prewarmCount = 2;

        private static MediaPlayerPool instance;

        /// <summary>
        /// 单例实例
        /// </summary>
        public static MediaPlayerPool Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("MediaPlayerPool");
                    instance = go.AddComponent<MediaPlayerPool>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }
        // 可用的播放器实例池
        private Dictionary<string, Queue<PooledPlayer>> playerPool = new Dictionary<string, Queue<PooledPlayer>>();

        // 正在使用的播放器实例
        private Dictionary<string, List<VlcMediaPlayer>> activePlayers = new Dictionary<string, List<VlcMediaPlayer>>();

        // 上次自动清理时间
        private float lastCleanupTime;

        /// <summary>
        /// 对象池中的播放器包装类，记录入池时间
        /// </summary>
        private class PooledPlayer
        {
            public VlcMediaPlayer Player { get; private set; }
            public float EnterPoolTime { get; private set; }
            public bool IsValid => Player != null;

            public PooledPlayer(VlcMediaPlayer player)
            {
                Player = player;
                UpdateEnterPoolTime();
            }

            public void UpdateEnterPoolTime()
            {
                EnterPoolTime = Time.realtimeSinceStartup;
            }

            public float IdleTime => Time.realtimeSinceStartup - EnterPoolTime;
        }

        // 键格式: "{width}x{height}_{mute}"
        private string GetKey(int width, int height, bool mute)
        {
            return $"{width}x{height}_{mute}";
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            lastCleanupTime = Time.realtimeSinceStartup;
            StartCoroutine(AutoCleanupCoroutine());
        }

        /// <summary>
        /// 热对象池，提前创建常用分辨率的播放器
        /// </summary>
        private IEnumerator PrewarmPool()
        {
            // 等待一帧，确保系统初始化完成
            yield return null;

            LogInfo($"开始预热对象池，创建 {prewarmCount} 个播放器实例");

            // 常用分辨率配置
            var commonConfigs = new[]
            {
                new { width = 1280, height = 720 },   // 720p
                new { width = 1920, height = 1080 },  // 1080p
            };

            foreach (var config in commonConfigs)
            {
                for (int i = 0; i < prewarmCount; i++)
                {
                    bool success = false;
                    Exception error = null;

                    // 尝试创建播放器
                    try
                    {
                        // 创建一个临时URL用于初始化
                        string dummyUrl = "dummy://prewarm";
                        var player = new VlcMediaPlayer(config.width, config.height, dummyUrl, true);

                        // 立即停止并释放到池中
                        player.Stop();
                        ReleasePlayer(player, config.width, config.height, true);

                        success = true;
                        LogInfo($"预热创建播放器: {config.width}x{config.height}");
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }

                    if (!success && error != null)
                    {
                        LogError($"预热对象池失败: {error.Message}");
                    }

                    yield return null;
                }
            }

            LogInfo($"对象池预热完成，当前统计: {GetPoolStats()}");
        }

        private void Update()
        {
            // 检查是否需要清理
            if (Time.realtimeSinceStartup - lastCleanupTime >= AUTO_CLEANUP_INTERVAL)
            {
                CleanupIdlePlayers();
                lastCleanupTime = Time.realtimeSinceStartup;
            }
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        private void LogInfo(string message)
        {
            // 默认禁用详细日志，减少控制台输出
            // 如需调试，请在Inspector中启用 enableDetailedLogs
        }

        private void LogWarning(string message)
        {
            if (enableDetailedLogs)
                Debug.LogWarning($"[Pool] {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"[Pool] {message}");
        }

        /// <summary>
        /// 获取一个VlcMediaPlayer实例
        /// </summary>
        /// <param name="width">视频宽度</param>
        /// <param name="height">视频高度</param>
        /// <param name="url">媒体URL</param>
        /// <param name="mute">是否静音</param>
        /// <returns>VlcMediaPlayer实例</returns>
        public VlcMediaPlayer GetPlayer(int width, int height, string url, bool mute)
        {
            string key = GetKey(width, height, mute);
            VlcMediaPlayer player = null;

            LogInfo($"尝试获取播放器 - 配置: {key}");

            // 检查是否有可用的实例
            if (playerPool.TryGetValue(key, out Queue<PooledPlayer> players) && players.Count > 0)
            {
                LogInfo($"发现可用播放器池, 数量: {players.Count}");

                // 如果启用了自动清理无效引用，先检查队列中的实例是否都有效
                if (autoCleanupInvalidReferences)
                {
                    int originalCount = players.Count;
                    Queue<PooledPlayer> validPlayers = new Queue<PooledPlayer>();

                    while (players.Count > 0)
                    {
                        PooledPlayer pooledPlayer = players.Dequeue();
                        if (pooledPlayer.IsValid)
                        {
                            validPlayers.Enqueue(pooledPlayer);
                        }
                        else
                        {
                            LogWarning($"在池中发现无效的播放器引用，已移除");
                        }
                    }

                    players = validPlayers;
                    playerPool[key] = players;

                    if (originalCount != players.Count)
                    {
                        LogWarning($"从池中移除了 {originalCount - players.Count} 个无效引用");
                    }
                }

                if (players.Count > 0)
                {
                    PooledPlayer pooledPlayer = players.Dequeue();
                    player = pooledPlayer.Player;

                    try
                    {
                        // 确认播放器仍然有效
                        bool isValid = player != null;
                        if (isValid)
                        {
                            // 尝试访问属性，如果播放器无效会抛出异常
                            var state = player.State;
                            LogInfo($"从对象池中获取播放器实例: {key}, 闲置时间: {pooledPlayer.IdleTime:F1}秒, 剩余: {players.Count}");
                        }
                        else
                        {
                            LogWarning($"从池中取出的播放器实例无效，将创建新实例");
                            isValid = false;
                            player = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"从池中取出的播放器实例已失效: {ex.Message}，将创建新实例");
                        player = null;
                    }
                }
            }
            else
            {
                LogInfo($"没有可用的播放器池或池为空，将创建新实例");
            }

            if (player == null)
            {
                try
                {
                    player = new VlcMediaPlayer(width, height, url, mute);
                    LogInfo($"创建新的播放器实例: {key}");
                }
                catch (Exception ex)
                {
                    LogError($"创建播放器实例失败: {ex.Message}");
                    throw;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(url))
                {
                    try
                    {
                        player.UpdateUrl(url);
                        LogInfo($"更新播放器URL: {url}");
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"更新播放器URL失败: {ex.Message}，将重新创建实例");

                        // 如果更新URL失败，可能是播放器已失效，需要创建新实例
                        try
                        {
                            player = new VlcMediaPlayer(width, height, url, mute);
                            LogInfo($"重新创建播放器实例: {key}");
                        }
                        catch (Exception ex2)
                        {
                            LogError($"重新创建播放器实例失败: {ex2.Message}");
                            throw;
                        }
                    }
                }
            }

            // 添加到活动列表
            if (!activePlayers.TryGetValue(key, out List<VlcMediaPlayer> activeList))
            {
                activeList = new List<VlcMediaPlayer>();
                activePlayers[key] = activeList;
            }

            // 检查是否已在活动列表中
            if (!activeList.Contains(player))
            {
                activeList.Add(player);
                LogInfo($"播放器已添加到活动列表，当前活动数量: {activeList.Count}");
            }
            else
            {
                LogWarning($"播放器已在活动列表中，可能是重复获取或释放失败");
            }

            return player;
        }

        /// <summary>
        /// 释放VlcMediaPlayer实例回对象池
        /// </summary>
        /// <param name="player">要释放的播放器实例</param>
        /// <param name="width">视频宽度</param>
        /// <param name="height">视频高度</param>
        /// <param name="mute">是否静音</param>
        public void ReleasePlayer(VlcMediaPlayer player, int width, int height, bool mute)
        {
            if (player == null)
            {
                LogWarning("尝试释放空的播放器实例");
                return;
            }

            string key = GetKey(width, height, mute);
            LogInfo($"尝试释放播放器 - 配置: {key}");

            bool isPlayerValid = true;

            // 尝试停止播放
            try
            {
                if (player.IsPlaying())
                {
                    player.Stop();
                    LogInfo("播放器已停止播放");
                }
            }
            catch (Exception ex)
            {
                LogWarning($"释放播放器时停止播放失败: {ex.Message}");
                isPlayerValid = false;
            }

            // 从活动列表中移除
            bool removedFromActive = false;
            if (activePlayers.TryGetValue(key, out List<VlcMediaPlayer> activeList))
            {
                removedFromActive = activeList.Remove(player);
                LogInfo($"播放器已从活动列表移除: {removedFromActive}, 剩余活动数量: {activeList.Count}");
            }
            else
            {
                LogWarning($"活动列表不存在此配置: {key}");
            }

            // 如果播放器已无效或未从活动列表移除（可能是错误的实例），直接销毁而不是放入池中
            if (!isPlayerValid || !removedFromActive)
            {
                try
                {
                    LogWarning($"播放器实例无效或未在活动列表中，直接销毁");
                    player.Dispose();
                    return;
                }
                catch (Exception ex)
                {
                    LogError($"销毁无效的播放器实例失败: {ex.Message}");
                    return;
                }
            }

            // 获取对象池队列
            if (!playerPool.TryGetValue(key, out Queue<PooledPlayer> players))
            {
                players = new Queue<PooledPlayer>();
                playerPool[key] = players;
                LogInfo($"为配置 {key} 创建新的播放器池");
            }

            // 检查对象池是否已达到最大容量
            if (players.Count >= MAX_POOL_SIZE_PER_KEY)
            {
                // 池已满，直接释放播放器
                try
                {
                    player.Dispose();
                    LogInfo($"对象池[{key}]已满({MAX_POOL_SIZE_PER_KEY})，直接释放播放器实例");
                }
                catch (Exception ex)
                {
                    LogError($"释放播放器实例失败: {ex.Message}");
                }
            }
            else
            {
                // 将播放器加入对象池
                players.Enqueue(new PooledPlayer(player));
                LogInfo($"播放器实例释放回对象池: {key}, 当前数量: {players.Count}/{MAX_POOL_SIZE_PER_KEY}");
            }
        }

        /// <summary>
        /// 清理闲置超时的播放器实例
        /// </summary>
        private void CleanupIdlePlayers()
        {
            int totalCleaned = 0;
            int invalidReferences = 0;

            LogInfo("开始清理闲置播放器...");

            foreach (var key in new List<string>(playerPool.Keys))
            {
                if (!playerPool.TryGetValue(key, out Queue<PooledPlayer> players))
                    continue;

                int originalCount = players.Count;

                // 创建新队列存储保留的实例
                Queue<PooledPlayer> remainingPlayers = new Queue<PooledPlayer>();

                // 检查每个实例的闲置时间
                while (players.Count > 0)
                {
                    PooledPlayer pooledPlayer = players.Dequeue();

                    // 首先检查播放器引用是否有效
                    if (!pooledPlayer.IsValid)
                    {
                        invalidReferences++;
                        continue;
                    }

                    // 如果闲置时间超过阈值，则释放资源
                    if (pooledPlayer.IdleTime > MAX_IDLE_TIME)
                    {
                        try
                        {
                            pooledPlayer.Player.Dispose();
                            totalCleaned++;
                            LogInfo($"清理了闲置时间为 {pooledPlayer.IdleTime:F1}秒 的播放器实例");
                        }
                        catch (Exception ex)
                        {
                            LogError($"清理闲置播放器时发生错误: {ex.Message}");
                            invalidReferences++;
                        }
                    }
                    else
                    {
                        remainingPlayers.Enqueue(pooledPlayer);
                    }
                }

                playerPool[key] = remainingPlayers;

                LogInfo($"配置 {key}: 原始数量={originalCount}, 清理后数量={remainingPlayers.Count}, 清理={originalCount - remainingPlayers.Count}");
            }

            if (totalCleaned > 0 || invalidReferences > 0)
            {
                LogInfo($"清理完成: 释放了 {totalCleaned} 个长时间闲置的播放器实例, 移除了 {invalidReferences} 个无效引用");
            }
            else
            {
                LogInfo("清理完成: 没有需要清理的播放器实例");
            }
        }

        /// <summary>
        /// 清理活动列表中的无效引用
        /// </summary>
        private void CleanupInvalidActiveReferences()
        {
            int totalRemoved = 0;

            foreach (var key in new List<string>(activePlayers.Keys))
            {
                if (!activePlayers.TryGetValue(key, out List<VlcMediaPlayer> activeList))
                    continue;

                int originalCount = activeList.Count;
                activeList.RemoveAll(player => player == null);
                int removedCount = originalCount - activeList.Count;
                totalRemoved += removedCount;

                if (removedCount > 0)
                {
                    LogInfo($"从活动列表[{key}]中移除了 {removedCount} 个无效引用");
                }
            }

            if (totalRemoved > 0)
            {
                LogInfo($"总共从活动列表中移除了 {totalRemoved} 个无效引用");
            }
        }

        /// <summary>
        /// 自动清理
        /// </summary>
        private IEnumerator AutoCleanupCoroutine()
        {
            WaitForSeconds wait = new WaitForSeconds(AUTO_CLEANUP_INTERVAL);

            while (true)
            {
                yield return wait;

                // 先清理活动列表中的无效引用
                CleanupInvalidActiveReferences();

                // 再清理闲置的播放器
                CleanupIdlePlayers();

                LogInfo($"自动清理完成，当前统计: {GetPoolStats()}");
            }
        }

        /// <summary>
        /// 清理所有对象池中的资源
        /// </summary>
        public void ClearPool()
        {
            LogInfo("开始清理所有对象池资源...");

            int activeCount = 0;
            int pooledCount = 0;
            int failedCount = 0;

            // 释放所有活动的播放器
            foreach (var list in activePlayers.Values)
            {
                foreach (var player in list)
                {
                    if (player == null) continue;

                    try
                    {
                        player.Stop();
                        player.Dispose();
                        activeCount++;
                    }
                    catch (Exception ex)
                    {
                        LogError($"释放活动播放器失败: {ex.Message}");
                        failedCount++;
                    }
                }
                list.Clear();
            }
            activePlayers.Clear();

            // 释放所有池中的播放器
            foreach (var queue in playerPool.Values)
            {
                while (queue.Count > 0)
                {
                    var pooledPlayer = queue.Dequeue();
                    if (pooledPlayer == null || pooledPlayer.Player == null) continue;

                    try
                    {
                        pooledPlayer.Player.Dispose();
                        pooledCount++;
                    }
                    catch (Exception ex)
                    {
                        LogError($"释放池中播放器失败: {ex.Message}");
                        failedCount++;
                    }
                }
            }
            playerPool.Clear();

            LogInfo($"所有对象池资源已清理：活动={activeCount}, 池中={pooledCount}, 失败={failedCount}");
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                ClearPool();
                instance = null;
            }
        }

        /// <summary>
        /// 获取活动播放器数量和池中播放器数量的统计信息
        /// </summary>
        /// <returns>统计信息字符串</returns>
        public string GetPoolStats()
        {
            int totalActive = 0;
            int totalPooled = 0;

            foreach (var list in activePlayers.Values)
            {
                totalActive += list.Count;
            }

            foreach (var queue in playerPool.Values)
            {
                totalPooled += queue.Count;
            }

            return $"活动播放器: {totalActive}, 池中播放器: {totalPooled}";
        }

        /// <summary>
        /// 获取详细的对象池统计信息
        /// </summary>
        /// <returns>详细统计信息</returns>
        public string GetDetailedStats()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== MediaPlayerPool 统计信息 ===");

            // 合并所有键
            HashSet<string> allKeys = new HashSet<string>();
            foreach (var key in playerPool.Keys) allKeys.Add(key);
            foreach (var key in activePlayers.Keys) allKeys.Add(key);

            // 按配置分组显示
            foreach (var key in allKeys)
            {
                int activeCount = activePlayers.TryGetValue(key, out var list) ? list.Count : 0;
                int pooledCount = playerPool.TryGetValue(key, out var queue) ? queue.Count : 0;

                sb.AppendLine($"配置 [{key}]: 活动={activeCount}, 池中={pooledCount}");
            }

            return sb.ToString();
        }
    }
}