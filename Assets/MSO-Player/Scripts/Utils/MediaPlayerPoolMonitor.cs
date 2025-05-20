using UnityEngine;

namespace yan.libvlc
{
    /// <summary>
    /// MediaPlayerPool监视器，用于展示和控制对象池状态
    /// </summary>
    public class MediaPlayerPoolMonitor : MonoBehaviour
    {
        [SerializeField, Tooltip("是否显示监视器")]
        private bool showMonitor = true;
        
        [SerializeField, Tooltip("是否显示详细统计信息")]
        private bool showDetailedStats = false;
        
        [SerializeField, Tooltip("更新统计信息的时间间隔（秒）")]
        private float updateInterval = 2.0f;
        
        [SerializeField, Tooltip("监视器位置")]
        private MonitorPosition monitorPosition = MonitorPosition.TopRight;
        
        [SerializeField, Tooltip("监视器宽度")]
        private float windowWidth = 300f;
        
        [SerializeField, Tooltip("监视器背景透明度")]
        [Range(0f, 1f)]
        private float backgroundAlpha = 0.7f;
        
        [SerializeField, Tooltip("监视器背景颜色")]
        private Color backgroundColor = new Color(0.1f, 0.1f, 0.2f);
        
        [SerializeField, Tooltip("标题栏颜色")]
        private Color titleBarColor = new Color(0.0f, 0.4f, 0.8f);
        
        [SerializeField, Tooltip("边框颜色")]
        private Color borderColor = new Color(0.5f, 0.5f, 0.9f);
        
        [SerializeField, Tooltip("边框宽度")]
        private float borderWidth = 2f;
        
        [SerializeField, Tooltip("显示/隐藏监视器的快捷键")]
        private KeyCode toggleKey = KeyCode.F8;
        
        [SerializeField, Tooltip("显示/隐藏详细信息的快捷键")]
        private KeyCode detailsKey = KeyCode.F9;
        
        [SerializeField, Tooltip("清理对象池的快捷键")]
        private KeyCode clearPoolKey = KeyCode.F10;
        
        [SerializeField, Tooltip("是否启用快捷键")]
        private bool enableHotkeys = true;
        
        private float lastUpdateTime;
        private string statsInfo = "";
        private string detailedStatsInfo = "";
        private Rect windowRect;
        private int windowId = 9898; // 随机ID，避免与其他GUI窗口冲突
        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle titleStyle;
        private GUIStyle windowStyle;
        private GUIStyle contentBoxStyle;
        private Vector2 scrollPosition;
        private bool stylesInitialized = false;
        private Texture2D borderTexture;
        private Texture2D contentBgTexture;
        private Texture2D titleBgTexture;
        private Texture2D hotkeysTexture;
        
        /// <summary>
        /// 监视器位置枚举
        /// </summary>
        public enum MonitorPosition
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        private void Start()
        {
            lastUpdateTime = -updateInterval; // 确保第一帧立即更新
            UpdateStats();
        }

        private void Update()
        {
            // 更新统计信息
            if (Time.time >= lastUpdateTime + updateInterval)
            {
                lastUpdateTime = Time.time;
                UpdateStats();
            }
            
            // 处理快捷键
            if (enableHotkeys)
            {
                // 显示/隐藏监视器
                if (Input.GetKeyDown(toggleKey))
                {
                    ToggleMonitor();
                }
                
                // 显示/隐藏详细信息
                if (Input.GetKeyDown(detailsKey))
                {
                    ToggleDetailedStats();
                }
                
                // 清理对象池
                if (Input.GetKeyDown(clearPoolKey))
                {
                    ClearPool();
                }
            }
        }
        
        private void InitStyles()
        {
            if (stylesInitialized) return;
            
            // 创建纹理
            contentBgTexture = CreateColorTexture(new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, backgroundAlpha));
            titleBgTexture = CreateColorTexture(new Color(titleBarColor.r, titleBarColor.g, titleBarColor.b, backgroundAlpha + 0.1f));
            borderTexture = CreateColorTexture(new Color(borderColor.r, borderColor.g, borderColor.b, backgroundAlpha + 0.2f));
            hotkeysTexture = CreateColorTexture(new Color(0f, 0f, 0f, backgroundAlpha));
            
            // 窗口样式
            windowStyle = new GUIStyle();
            windowStyle.normal.background = contentBgTexture;
            windowStyle.border = new RectOffset(1, 1, 1, 1);
            windowStyle.margin = new RectOffset(0, 0, 0, 0);
            windowStyle.padding = new RectOffset(10, 10, 10, 10);
            
            // 内容框样式
            contentBoxStyle = new GUIStyle(GUI.skin.box);
            contentBoxStyle.normal.background = contentBgTexture;
            contentBoxStyle.border = new RectOffset(3, 3, 3, 3);
            
            // 标准框样式
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = contentBgTexture;
            
            // 文本样式
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontSize = 12;
            labelStyle.fontStyle = FontStyle.Normal;
            labelStyle.wordWrap = true;
            
            // 按钮样式
            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 12;
            
            // 标题样式
            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.normal.textColor = Color.white;
            titleStyle.normal.background = titleBgTexture;
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontSize = 14;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.padding = new RectOffset(5, 5, 5, 5);
            
            stylesInitialized = true;
        }
        
        private Texture2D CreateColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void UpdateStats()
        {
            statsInfo = MediaPlayerPool.Instance.GetPoolStats();
            
            if (showDetailedStats)
            {
                detailedStatsInfo = MediaPlayerPool.Instance.GetDetailedStats();
            }
        }
        
        private void OnGUI()
        {
            if (!showMonitor) return;
            
            InitStyles();
            
            float windowHeight = showDetailedStats ? 250f : 100f;
            
            // 根据选择的位置设置窗口位置
            switch (monitorPosition)
            {
                case MonitorPosition.TopLeft:
                    windowRect = new Rect(10, 10, windowWidth, windowHeight);
                    break;
                case MonitorPosition.TopRight:
                    windowRect = new Rect(Screen.width - windowWidth - 10, 10, windowWidth, windowHeight);
                    break;
                case MonitorPosition.BottomLeft:
                    windowRect = new Rect(10, Screen.height - windowHeight - 10, windowWidth, windowHeight);
                    break;
                case MonitorPosition.BottomRight:
                    windowRect = new Rect(Screen.width - windowWidth - 10, Screen.height - windowHeight - 10, windowWidth, windowHeight);
                    break;
            }
            
            // 绘制边框
            Rect borderRect = new Rect(
                windowRect.x - borderWidth,
                windowRect.y - borderWidth,
                windowRect.width + borderWidth * 2,
                windowRect.height + borderWidth * 2
            );
            GUI.color = borderColor;
            GUI.DrawTexture(borderRect, borderTexture);
            GUI.color = Color.white;
            
            // 绘制窗口
            windowRect = GUILayout.Window(windowId, windowRect, DrawWindow, "媒体播放器对象池监视器", titleStyle);
            
            // 在窗口下方显示快捷键提示（如果启用）
            if (enableHotkeys)
            {
                float y = windowRect.y + windowRect.height + 5;
                Rect hotkeysRect = new Rect(windowRect.x, y, windowRect.width, 25);
                GUI.DrawTexture(hotkeysRect, hotkeysTexture);
                GUI.Label(hotkeysRect, $"快捷键: {toggleKey}=显示/隐藏 {detailsKey}=详情 {clearPoolKey}=清理", labelStyle);
            }
        }
        
        private void DrawWindow(int id)
        {
            GUILayout.Space(5);
            
            // 绘制内容背景
            Rect contentRect = new Rect(5, 25, windowRect.width - 10, windowRect.height - 30);
            GUI.Box(contentRect, "", contentBoxStyle);
            
            // 基本统计信息
            GUILayout.Label(statsInfo, labelStyle);
            
            GUILayout.Space(5);
            
            // 详细统计信息（可滚动）
            if (showDetailedStats)
            {
                GUILayout.Label("详细统计：", labelStyle);
                
                // 创建带滚动条的区域并添加背景
                Rect scrollRect = GUILayoutUtility.GetRect(windowRect.width - 20, 130);
                GUI.Box(scrollRect, "", contentBoxStyle);
                
                scrollPosition = GUI.BeginScrollView(scrollRect, scrollPosition, 
                    new Rect(0, 0, scrollRect.width - 20, Mathf.Max(scrollRect.height, 200)));
                GUILayout.Label(detailedStatsInfo, labelStyle);
                GUI.EndScrollView();
            }
            
            GUILayout.Space(5);
            
            // 按钮区域
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button(showDetailedStats ? "隐藏详情" : "显示详情", buttonStyle))
            {
                ToggleDetailedStats();
            }
            
            if (GUILayout.Button("清理池", buttonStyle))
            {
                ClearPool();
            }
            
            if (GUILayout.Button("关闭", buttonStyle))
            {
                showMonitor = false;
            }
            
            GUILayout.EndHorizontal();
            
            // 显示性能指标
            GUILayout.Space(5);
            float memoryUsage = (float)System.GC.GetTotalMemory(false) / (1024 * 1024); // MB
            Rect performanceRect = GUILayoutUtility.GetRect(windowRect.width - 20, 20);
            GUI.Box(performanceRect, "", contentBoxStyle);
            GUI.Label(performanceRect, $"内存: {memoryUsage:F1} MB | FPS: {1.0f/Time.deltaTime:F1}", labelStyle);
            
            // 允许窗口拖动
            GUI.DragWindow();
        }

        /// <summary>
        /// 切换监视器显示状态
        /// </summary>
        public void ToggleMonitor()
        {
            showMonitor = !showMonitor;
            Debug.Log($"媒体播放器监视器已{(showMonitor ? "显示" : "隐藏")}");
        }
        
        /// <summary>
        /// 切换是否显示详细统计信息
        /// </summary>
        public void ToggleDetailedStats()
        {
            showDetailedStats = !showDetailedStats;
            UpdateStats();
        }

        /// <summary>
        /// 清理所有对象池资源
        /// </summary>
        public void ClearPool()
        {
            MediaPlayerPool.Instance.ClearPool();
            UpdateStats();
            Debug.Log("媒体播放器对象池已手动清理");
        }
        
        /// <summary>
        /// 设置监视器位置
        /// </summary>
        public void SetPosition(MonitorPosition position)
        {
            monitorPosition = position;
        }
        
        private void OnDestroy()
        {
            // 清理创建的纹理资源
            if (contentBgTexture != null) Destroy(contentBgTexture);
            if (titleBgTexture != null) Destroy(titleBgTexture);
            if (borderTexture != null) Destroy(borderTexture);
            if (hotkeysTexture != null) Destroy(hotkeysTexture);
        }
    }
} 