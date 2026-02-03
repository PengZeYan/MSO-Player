using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using yan.libvlc.Core;

namespace yan.libvlc
{
    /// <summary>
    /// 媒体播放器加载指示器
    /// 在播放器初始化期间显示加载动画，改善用户体验
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class MediaPlayerLoadingIndicator : MonoBehaviour
    {
        [Header("UI组件")]
        [SerializeField, Tooltip("加载文本")]
        private Text loadingText;
        
        [SerializeField, Tooltip("加载图标（可选）")]
        private Image loadingIcon;
        
        [SerializeField, Tooltip("进度条（可选）")]
        private Slider progressBar;
        
        [Header("动画设置")]
        [SerializeField, Tooltip("旋转速度（度/秒）")]
        private float rotationSpeed = 180f;
        
        [SerializeField, Tooltip("淡入淡出时间")]
        private float fadeDuration = 0.3f;
        
        [SerializeField, Tooltip("加载文本动画")]
        private bool animateLoadingText = true;
        
        [SerializeField, Tooltip("文本动画间隔")]
        private float textAnimationInterval = 0.5f;
        
        [Header("自动控制")]
        [SerializeField, Tooltip("关联的媒体播放器")]
        private MediaPlayer mediaPlayer;
        
        [SerializeField, Tooltip("是否自动显示/隐藏")]
        private bool autoControl = true;
        
        [SerializeField, Tooltip("最小显示时间（秒）")]
        private float minDisplayTime = 0.5f;
        
        private CanvasGroup canvasGroup;
        private Coroutine fadeCoroutine;
        private Coroutine textAnimationCoroutine;
        private float showStartTime;
        private bool isShowing = false;
        
        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            
            // 初始状态：隐藏
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            
            if (progressBar != null)
            {
                progressBar.value = 0f;
            }
        }
        
        private void Start()
        {
            if (autoControl && mediaPlayer != null)
            {
                // 监听播放器状态
                mediaPlayer.OnMediaPlayerStateEvent += OnMediaPlayerStateChanged;
                
                // 如果播放器在启动时就开始加载，立即显示加载指示器
                if (mediaPlayer.CurrentMediaState == libvlc_state_t.libvlc_Opening ||
                    mediaPlayer.CurrentMediaState == libvlc_state_t.libvlc_Buffering)
                {
                    Show();
                }
            }
        }
        
        private void Update()
        {
            // 旋转加载图标
            if (isShowing && loadingIcon != null)
            {
                loadingIcon.transform.Rotate(0, 0, -rotationSpeed * Time.deltaTime);
            }
        }
        
        private void OnDestroy()
        {
            if (mediaPlayer != null)
            {
                mediaPlayer.OnMediaPlayerStateEvent -= OnMediaPlayerStateChanged;
            }
        }
        
        /// <summary>
        /// 显示加载指示器
        /// </summary>
        public void Show()
        {
            if (isShowing) return;
            
            isShowing = true;
            showStartTime = Time.time;
            
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            
            fadeCoroutine = StartCoroutine(FadeIn());
            
            if (animateLoadingText && loadingText != null)
            {
                if (textAnimationCoroutine != null)
                {
                    StopCoroutine(textAnimationCoroutine);
                }
                textAnimationCoroutine = StartCoroutine(AnimateLoadingText());
            }
            
            if (progressBar != null)
            {
                StartCoroutine(AnimateProgressBar());
            }
        }
        
        /// <summary>
        /// 隐藏加载指示器
        /// </summary>
        public void Hide()
        {
            if (!isShowing) return;
            
            // 确保至少显示了最小时间
            float displayTime = Time.time - showStartTime;
            if (displayTime < minDisplayTime)
            {
                StartCoroutine(DelayedHide(minDisplayTime - displayTime));
                return;
            }
            
            isShowing = false;
            
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            
            fadeCoroutine = StartCoroutine(FadeOut());
            
            if (textAnimationCoroutine != null)
            {
                StopCoroutine(textAnimationCoroutine);
                textAnimationCoroutine = null;
            }
        }
        
        /// <summary>
        /// 延迟隐藏
        /// </summary>
        private IEnumerator DelayedHide(float delay)
        {
            yield return new WaitForSeconds(delay);
            Hide();
        }
        
        /// <summary>
        /// 淡入动画
        /// </summary>
        private IEnumerator FadeIn()
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;
            
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / fadeDuration);
                yield return null;
            }
            
            canvasGroup.alpha = 1f;
        }
        
        /// <summary>
        /// 淡出动画
        /// </summary>
        private IEnumerator FadeOut()
        {
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;
            
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeDuration);
                yield return null;
            }
            
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        
        /// <summary>
        /// 加载文本动画
        /// </summary>
        private IEnumerator AnimateLoadingText()
        {
            if (loadingText == null) yield break;
            
            string baseText = "加载中";
            int dotCount = 0;
            
            while (isShowing)
            {
                loadingText.text = baseText + new string('.', dotCount);
                dotCount = (dotCount + 1) % 4;
                yield return new WaitForSeconds(textAnimationInterval);
            }
        }
        
        /// <summary>
        /// 进度条动画（模拟进度）
        /// </summary>
        private IEnumerator AnimateProgressBar()
        {
            if (progressBar == null) yield break;
            
            progressBar.value = 0f;
            float targetProgress = 0f;
            
            while (isShowing)
            {
                // 模拟进度增长，但永远不会到达100%
                targetProgress = Mathf.Min(targetProgress + Random.Range(0.05f, 0.15f), 0.95f);
                
                float elapsed = 0f;
                float startValue = progressBar.value;
                float duration = Random.Range(0.3f, 0.8f);
                
                while (elapsed < duration && isShowing)
                {
                    elapsed += Time.deltaTime;
                    progressBar.value = Mathf.Lerp(startValue, targetProgress, elapsed / duration);
                    yield return null;
                }
                
                yield return new WaitForSeconds(Random.Range(0.2f, 0.5f));
            }
            
            // 隐藏时快速完成到100%
            float finalElapsed = 0f;
            float finalDuration = 0.2f;
            float finalStartValue = progressBar.value;
            
            while (finalElapsed < finalDuration)
            {
                finalElapsed += Time.deltaTime;
                progressBar.value = Mathf.Lerp(finalStartValue, 1f, finalElapsed / finalDuration);
                yield return null;
            }
        }
        
        /// <summary>
        /// 媒体播放器状态变化处理
        /// </summary>
        private void OnMediaPlayerStateChanged(libvlc_state_t state, string stateMessage)
        {
            if (!autoControl) return;
            
            switch (state)
            {
                case libvlc_state_t.libvlc_Opening:
                case libvlc_state_t.libvlc_Buffering:
                    Show();
                    break;
                    
                case libvlc_state_t.libvlc_Playing:
                    Hide();
                    break;
                    
                case libvlc_state_t.libvlc_Error:
                case libvlc_state_t.libvlc_Stopped:
                    Hide();
                    break;
            }
        }
        
        /// <summary>
        /// 设置加载文本
        /// </summary>
        public void SetLoadingText(string text)
        {
            if (loadingText != null)
            {
                loadingText.text = text;
            }
        }
        
        /// <summary>
        /// 设置进度
        /// </summary>
        public void SetProgress(float progress)
        {
            if (progressBar != null)
            {
                progressBar.value = Mathf.Clamp01(progress);
            }
        }
    }
}
