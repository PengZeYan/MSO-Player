using UnityEngine;

namespace yan.libvlc
{
    /// <summary>
    /// 媒体播放器对象池引导类，确保对象池和监视器在场景中自动创建
    /// 提供了对象池状态的监控界面
    /// 显示活跃和缓存的播放器数量
    /// 支持显示详细统计信息
    /// 提供手动清理池的功能
    /// </summary>
    [DefaultExecutionOrder(-100)] // 确保在其他脚本之前执行
    public class MediaPlayerPoolBootstrap : MonoBehaviour
    {
        [SerializeField, Tooltip("是否在启动时初始化对象池")]
        private bool initializeOnStart = true;
        
        [SerializeField, Tooltip("是否添加监视器")]
        private bool addMonitor = true;
        
        [SerializeField, Tooltip("监视器初始是否可见")]
        private bool monitorVisible = true;
        
        [SerializeField, Tooltip("监视器位置")]
        private MediaPlayerPoolMonitor.MonitorPosition monitorPosition = MediaPlayerPoolMonitor.MonitorPosition.TopRight;
        
        private static bool hasInitialized = false;
        
        private void Awake()
        {
            // 确保只初始化一次
            if (hasInitialized)
            {
                Destroy(this);
                return;
            }
            
            // 在加载新场景时不销毁
            DontDestroyOnLoad(gameObject);
            
            // 标记为已初始化
            hasInitialized = true;
            
            if (initializeOnStart)
            {
                // 初始化对象池（访问单例实例即可初始化）
                _ = MediaPlayerPool.Instance;
                
                if (addMonitor)
                {
                    // 添加监视器
                    var monitor = gameObject.AddComponent<MediaPlayerPoolMonitor>();
                    
                    // 设置监视器属性
                    var prop = new MonitorProperties 
                    { 
                        showMonitor = monitorVisible,
                        position = monitorPosition
                    };
                    
                    ConfigureMonitor(monitor, prop);
                }
            }
        }
        
        /// <summary>
        /// 监视器属性结构
        /// </summary>
        public struct MonitorProperties
        {
            public bool showMonitor;
            public MediaPlayerPoolMonitor.MonitorPosition position;
        }
        
        /// <summary>
        /// 配置监视器
        /// </summary>
        private void ConfigureMonitor(MediaPlayerPoolMonitor monitor, MonitorProperties props)
        {
            if (monitor == null) return;
            
            // 反射设置私有字段
            var showMonitorField = monitor.GetType().GetField("showMonitor", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                
            if (showMonitorField != null)
            {
                showMonitorField.SetValue(monitor, props.showMonitor);
            }
            
            // 设置监视器位置
            monitor.SetPosition(props.position);
            
            Debug.Log($"媒体播放器监视器已初始化，初始状态: {(props.showMonitor ? "显示" : "隐藏")}");
        }
        
        /// <summary>
        /// 清理所有对象池资源
        /// </summary>
        public void ClearAllPools()
        {
            MediaPlayerPool.Instance.ClearPool();
        }
        
        private void OnDestroy()
        {
            if (hasInitialized)
            {
                // 确保清理所有对象池资源
                ClearAllPools();
                hasInitialized = false;
            }
        }
    }
} 