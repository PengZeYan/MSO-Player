using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using yan.libvlc.Core;

namespace yan.libvlc
{
    /// <summary>
    /// 媒体播放器预加载器 - 在初始化界面预热播放器，避免首次播放卡顿
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class MediaPlayerPreloader : MonoBehaviour
    {
        [Header("预热配置")]
        [SerializeField] private int prewarmCount = 4;
        [SerializeField] private List<Resolution> prewarmResolutions = new List<Resolution>
        {
            new Resolution { width = 1280, height = 720 },
            new Resolution { width = 1920, height = 1080 }
        };
        [SerializeField] private float prewarmDelay = 0f;
        [SerializeField] private bool persistAcrossScenes = true;
        [SerializeField] private bool enableDetailedLogs = true;

        [Header("高级选项")]
        [SerializeField] private bool mutePrewarmInstances = true;
        [SerializeField] private int maxPrewarmPerFrame = 1;
        private string dummyUrl = "dummy://prewarm";

        private static MediaPlayerPreloader instance;
        private bool isPrewarming = false;
        private bool isPrewarmed = false;
        private int totalPrewarmCount = 0;
        private int currentPrewarmCount = 0;
        private float prewarmStartTime = 0f;

        public static MediaPlayerPreloader Instance => instance;
        public bool IsPrewarming => isPrewarming;
        public bool IsPrewarmed => isPrewarmed;
        public float PrewarmProgress => totalPrewarmCount == 0 ? 0f : (float)currentPrewarmCount / totalPrewarmCount;

        [Serializable]
        public struct Resolution
        {
            public int width;
            public int height;
            public override string ToString() => $"{width}x{height}";
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (persistAcrossScenes) DontDestroyOnLoad(gameObject);

            _ = MediaPlayerPool.Instance;

            if (prewarmDelay > 0f)
                StartCoroutine(DelayedPrewarm());
            else
                StartCoroutine(PrewarmCoroutine());
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
        private IEnumerator DelayedPrewarm()
        {
            yield return new WaitForSeconds(prewarmDelay);
            yield return StartCoroutine(PrewarmCoroutine());
        }

        private IEnumerator PrewarmCoroutine()
        {
            if (isPrewarming || isPrewarmed) yield break;

            isPrewarming = true;
            prewarmStartTime = Time.realtimeSinceStartup;

            LogInfo($"开始预热: {prewarmCount}个实例 x {prewarmResolutions.Count}种分辨率");

            totalPrewarmCount = prewarmCount * prewarmResolutions.Count;
            currentPrewarmCount = 0;
            yield return null;

            foreach (var resolution in prewarmResolutions)
            {
                for (int i = 0; i < prewarmCount; i++)
                {
                    if (i > 0 && i % maxPrewarmPerFrame == 0)
                        yield return null;

                    try
                    {
                        VlcMediaPlayer player = new VlcMediaPlayer(
                            resolution.width, resolution.height, dummyUrl, mutePrewarmInstances);
                        player.Stop();
                        MediaPlayerPool.Instance.ReleasePlayer(
                            player, resolution.width, resolution.height, mutePrewarmInstances);
                        currentPrewarmCount++;
                    }
                    catch (Exception ex)
                    {
                        LogError($"预热失败 [{resolution}]: {ex.Message}");
                    }
                }
            }

            isPrewarming = false;
            isPrewarmed = true;

            float elapsedTime = Time.realtimeSinceStartup - prewarmStartTime;
            LogInfo($"预热完成: {currentPrewarmCount}/{totalPrewarmCount} 耗时{elapsedTime:F1}秒");
        }
        public void ManualPrewarm()
        {
            if (!isPrewarming && !isPrewarmed)
                StartCoroutine(PrewarmCoroutine());
        }

        public string GetPrewarmStatus()
        {
            if (isPrewarmed)
                return $"已完成 ({currentPrewarmCount}/{totalPrewarmCount})";
            else if (isPrewarming)
                return $"预热中 {PrewarmProgress * 100:F0}%";
            else
                return "未开始";
        }

        private void LogInfo(string msg)
        {
            if (enableDetailedLogs)
                Debug.Log($"[Preloader] {msg}");
        }

        private void LogError(string msg) => Debug.LogError($"[Preloader] {msg}");
    }
}
