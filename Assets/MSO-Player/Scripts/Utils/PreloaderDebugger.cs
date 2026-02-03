using UnityEngine;
using UnityEngine.UI;

namespace yan.libvlc
{
    /// <summary>
    /// 预加载器调试工具
    /// 用于在运行时查看预热状态和性能数据
    /// 可选添加到初始化场景用于调试
    /// </summary>
    public class PreloaderDebugger : MonoBehaviour
    {
        [Header("UI引用（可选）")]
        [SerializeField, Tooltip("显示预热状态的文本")]
        private Text statusText;

        [SerializeField, Tooltip("显示预热进度的文本")]
        private Text progressText;

        [SerializeField, Tooltip("显示对象池状态的文本")]
        private Text poolStatsText;

        [Header("调试选项")]
        [SerializeField, Tooltip("更新间隔（秒）")]
        private float updateInterval = 0.5f;

        [SerializeField, Tooltip("是否在Console输出日志")]
        private bool logToConsole = true;

        [SerializeField, Tooltip("显示调试信息的快捷键")]
        private KeyCode debugKey = KeyCode.F7;

        private float lastUpdateTime;
        private bool showDebugInfo = false;

        private void Start()
        {
            lastUpdateTime = Time.time;
            UpdateDebugInfo();
        }

        private void Update()
        {
            // 快捷键切换显示
            if (Input.GetKeyDown(debugKey))
            {
                showDebugInfo = !showDebugInfo;
                Debug.Log($"预加载器调试信息: {(showDebugInfo ? "显示" : "隐藏")}");
            }

            // 定期更新
            if (Time.time >= lastUpdateTime + updateInterval)
            {
                lastUpdateTime = Time.time;
                UpdateDebugInfo();
            }
        }

        private void UpdateDebugInfo()
        {
            if (MediaPlayerPreloader.Instance == null)
            {
                UpdateUI("预加载器未初始化", "N/A", "N/A");
                return;
            }

            // 获取状态信息
            string status = MediaPlayerPreloader.Instance.GetPrewarmStatus();
            float progress = MediaPlayerPreloader.Instance.PrewarmProgress;
            string poolStats = MediaPlayerPool.Instance.GetPoolStats();

            // 更新UI
            UpdateUI(status, $"{progress * 100:F0}%", poolStats);

            // 输出到Console
            if (logToConsole && showDebugInfo)
            {
                Debug.Log($"[PreloaderDebugger] 状态: {status} | 进度: {progress * 100:F0}% | 对象池: {poolStats}");
            }
        }

        private void UpdateUI(string status, string progress, string poolStats)
        {
            if (statusText != null)
            {
                statusText.text = $"预热状态: {status}";
            }

            if (progressText != null)
            {
                progressText.text = $"预热进度: {progress}";
            }

            if (poolStatsText != null)
            {
                poolStatsText.text = $"对象池: {poolStats}";
            }
        }

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            // 在屏幕左上角显示调试信息
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.normal.textColor = Color.white;
            style.fontSize = 14;
            style.alignment = TextAnchor.UpperLeft;
            style.padding = new RectOffset(10, 10, 10, 10);

            string debugInfo = GetDebugInfo();
            
            Rect rect = new Rect(10, 10, 400, 150);
            GUI.Box(rect, debugInfo, style);
        }

        private string GetDebugInfo()
        {
            if (MediaPlayerPreloader.Instance == null)
            {
                return "预加载器未初始化";
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== 预加载器调试信息 ===");
            sb.AppendLine($"状态: {MediaPlayerPreloader.Instance.GetPrewarmStatus()}");
            sb.AppendLine($"进度: {MediaPlayerPreloader.Instance.PrewarmProgress * 100:F1}%");
            sb.AppendLine($"对象池: {MediaPlayerPool.Instance.GetPoolStats()}");
            sb.AppendLine($"快捷键: {debugKey} = 显示/隐藏");

            return sb.ToString();
        }

        /// <summary>
        /// 手动触发预热（用于测试）
        /// </summary>
        public void ManualTriggerPrewarm()
        {
            if (MediaPlayerPreloader.Instance != null)
            {
                MediaPlayerPreloader.Instance.ManualPrewarm();
                Debug.Log("手动触发预热");
            }
            else
            {
                Debug.LogWarning("预加载器未初始化");
            }
        }

        /// <summary>
        /// 清理对象池（用于测试）
        /// </summary>
        public void ClearPool()
        {
            MediaPlayerPool.Instance.ClearPool();
            Debug.Log("对象池已清理");
            UpdateDebugInfo();
        }

        /// <summary>
        /// 输出详细统计信息
        /// </summary>
        public void LogDetailedStats()
        {
            Debug.Log("=== 详细统计信息 ===");
            Debug.Log(MediaPlayerPool.Instance.GetDetailedStats());
            
            if (MediaPlayerPreloader.Instance != null)
            {
                Debug.Log($"预热状态: {MediaPlayerPreloader.Instance.GetPrewarmStatus()}");
                Debug.Log($"预热进度: {MediaPlayerPreloader.Instance.PrewarmProgress * 100:F1}%");
            }
        }
    }
}
