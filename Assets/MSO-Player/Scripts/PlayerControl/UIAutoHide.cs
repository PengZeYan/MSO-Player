using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 控制UI自动隐藏的脚本，当用户超过指定时间不操作时，UI会自动淡出
/// </summary>
public class UIAutoHide : MonoBehaviour
{
    [Header("设置")]
    [SerializeField, Tooltip("不操作自动隐藏的等待时间(秒)")]
    private float hideDelay = 2.0f;
    
    [SerializeField, Tooltip("淡出动画持续时间(秒)")]
    private float fadeOutDuration = 0.5f;
    
    [SerializeField, Tooltip("淡入动画持续时间(秒)，默认为淡出时间的一半")]
    private float fadeInDuration = 0.25f;
    
    [SerializeField, Tooltip("鼠标移动检测的敏感度")]
    private float mouseSensitivity = 0.5f;
    
    [SerializeField, Tooltip("是否在视频暂停时保持UI显示")]
    private bool keepVisibleOnPause = true;
    
    [Header("引用")]
    [SerializeField, Tooltip("要控制的Canvas Group组件")]
    private CanvasGroup controlsCanvasGroup;
    
    [SerializeField, Tooltip("播放器控制器引用")]
    private PlayerController playerController;

    // 私有字段
    private Vector3 lastMousePosition;
    private float idleTimer = 0f;
    private bool isHiding = false;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        // 自动获取CanvasGroup组件（如果未手动指定）
        if (controlsCanvasGroup == null)
        {
            controlsCanvasGroup = GetComponent<CanvasGroup>();
        }
        
        // 确保组件存在
        if (controlsCanvasGroup == null)
        {
            Debug.LogError("UIAutoHide脚本需要CanvasGroup组件");
            enabled = false;
            return;
        }
        
        // 初始状态为可见
        controlsCanvasGroup.alpha = 1f;
        
        // 如果没有设置淡入时间，默认为淡出时间的一半
        if (fadeInDuration <= 0)
        {
            fadeInDuration = fadeOutDuration * 0.5f;
        }
    }

    private void Start()
    {
        // 记录初始鼠标位置
        lastMousePosition = Input.mousePosition;
        // 重置计时器
        ResetIdleTimer();
    }

    private void Update()
    {
        // 检查是否应始终保持显示（暂停时）
        if (keepVisibleOnPause && playerController != null && !playerController.IsPlaying)
        {
            ShowUI();
            return;
        }
        
        // 检测鼠标移动
        Vector3 mouseDelta = Input.mousePosition - lastMousePosition;
        
        // 鼠标移动距离大于敏感度
        if (mouseDelta.magnitude > mouseSensitivity)
        {
            // 鼠标移动了，显示UI并重置计时器
            ShowUI();
            ResetIdleTimer();
            lastMousePosition = Input.mousePosition;
        }
        // 检测UI交互
        else if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            // 用户正在与UI交互，重置计时器
            ResetIdleTimer();
        }
        else
        {
            // 累加空闲时间
            idleTimer += Time.deltaTime;
            
            // 如果超过设定的隐藏延迟时间，开始隐藏UI
            if (idleTimer >= hideDelay && !isHiding)
            {
                HideUI();
            }
        }
        
        // 任何按键或鼠标点击也会显示UI
        if (Input.anyKeyDown)
        {
            ShowUI();
            ResetIdleTimer();
        }
    }
    
    /// <summary>
    /// 显示UI界面
    /// </summary>
    public void ShowUI()
    {
        // 停止任何正在进行的淡出动画
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
        
        isHiding = false;
        
        // 使用较快的淡入动画显示UI
        fadeCoroutine = StartCoroutine(FadeTo(1f, fadeInDuration));
    }
    
    /// <summary>
    /// 隐藏UI界面
    /// </summary>
    public void HideUI()
    {
        // 停止任何正在进行的淡入动画
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
        
        isHiding = true;
        
        // 使用正常速度淡出UI
        fadeCoroutine = StartCoroutine(FadeTo(0f, fadeOutDuration));
    }
    
    /// <summary>
    /// 重置空闲计时器
    /// </summary>
    private void ResetIdleTimer()
    {
        idleTimer = 0f;
        isHiding = false;
    }
    
    /// <summary>
    /// 淡入淡出动画协程
    /// </summary>
    /// <param name="targetAlpha">目标透明度</param>
    /// <param name="duration">动画持续时间</param>
    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        // 如果目标透明度与当前透明度相同，则不执行动画
        if (Mathf.Approximately(controlsCanvasGroup.alpha, targetAlpha))
        {
            controlsCanvasGroup.alpha = targetAlpha;
            fadeCoroutine = null;
            yield break;
        }
        
        float startAlpha = controlsCanvasGroup.alpha;
        float time = 0;
        
        while (time < duration)
        {
            time += Time.deltaTime;
            float normalizedTime = time / duration; // 0到1的范围
            
            // 使用平滑的插值实现更自然的过渡
            controlsCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.SmoothStep(0, 1, normalizedTime));
            
            yield return null;
        }
        
        // 确保最终值完全正确
        controlsCanvasGroup.alpha = targetAlpha;
        
        // 如果完全透明，禁用交互以避免隐形按钮被点击
        controlsCanvasGroup.interactable = targetAlpha > 0;
        controlsCanvasGroup.blocksRaycasts = targetAlpha > 0;
        
        fadeCoroutine = null;
    }
} 