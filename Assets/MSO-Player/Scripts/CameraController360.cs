using UnityEngine;

namespace yan.libvlc
{
    /// <summary>
    /// 控制360度全景相机的旋转，支持鼠标和触摸输入
    /// </summary>
    public class CameraController360 : MonoBehaviour
    {
        [Header("鼠标控制设置")]
        [SerializeField, Tooltip("水平灵敏度")]
        private float m_HorizontalSensitivity = 2.0f;
        
        [SerializeField, Tooltip("垂直灵敏度")]
        private float m_VerticalSensitivity = 2.0f;
        
        [SerializeField, Tooltip("平滑度，值越大移动越平滑")]
        private float m_SmoothTime = 0.1f;
        
        [SerializeField, Tooltip("垂直角度限制")]
        private float m_VerticalLimit = 80.0f;
        
        [Header("移动平台设置")]
        [SerializeField, Tooltip("使用陀螺仪")]
        private bool m_UseGyroscope = true;
        
        [SerializeField, Tooltip("陀螺仪灵敏度")]
        private float m_GyroSensitivity = 1.0f;

        // 当前和目标欧拉角
        private Vector3 m_CurrentRotation;
        private Vector3 m_TargetRotation;
        
        // 速度变量用于平滑
        private Vector3 m_RotationVelocity;
        
        // 陀螺仪初始化状态
        private bool m_GyroInitialized = false;
        private Quaternion m_GyroInitialRotation;

        private void Start()
        {
            // 初始化当前旋转为相机的欧拉角
            m_CurrentRotation = transform.localEulerAngles;
            m_TargetRotation = m_CurrentRotation;
            
            // 初始化陀螺仪（如果设备支持）
            InitializeGyroscope();
        }

        private void Update()
        {
            // 在移动平台上优先使用陀螺仪（如果启用）
            if (m_UseGyroscope && m_GyroInitialized && SystemInfo.supportsGyroscope)
            {
                UpdateGyroRotation();
            }
            else
            {
                // 否则使用鼠标/触摸输入
                UpdateMouseRotation();
            }
            
            // 平滑地应用旋转
            ApplyRotation();
        }

        /// <summary>
        /// 初始化陀螺仪控制
        /// </summary>
        private void InitializeGyroscope()
        {
            if (!SystemInfo.supportsGyroscope)
            {
                return;
            }
            
            Input.gyro.enabled = true;
            m_GyroInitialized = true;
            m_GyroInitialRotation = Input.gyro.attitude;
            
            Debug.Log("陀螺仪已初始化");
        }

        /// <summary>
        /// 基于陀螺仪更新旋转
        /// </summary>
        private void UpdateGyroRotation()
        {
            // 获取陀螺仪旋转并应用灵敏度
            Quaternion attitude = Input.gyro.attitude;
            Quaternion rotationFix = new Quaternion(attitude.x, attitude.y, -attitude.z, -attitude.w);
            Quaternion rotation = Quaternion.Inverse(m_GyroInitialRotation) * rotationFix;
            
            // 转换为欧拉角并应用
            Vector3 gyroEulerAngles = rotation.eulerAngles;
            m_TargetRotation.x = -gyroEulerAngles.x * m_GyroSensitivity;
            m_TargetRotation.y = gyroEulerAngles.y * m_GyroSensitivity;
        }

        /// <summary>
        /// 基于鼠标/触摸输入更新旋转
        /// </summary>
        private void UpdateMouseRotation()
        {
            // 处理鼠标和触摸输入
            if (Input.GetMouseButton(0))
            {
                float mouseX = Input.GetAxis("Mouse X") * m_HorizontalSensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * m_VerticalSensitivity;
                
                m_TargetRotation.y += mouseX;
                m_TargetRotation.x -= mouseY;
                
                // 限制垂直角度范围
                m_TargetRotation.x = Mathf.Clamp(m_TargetRotation.x, -m_VerticalLimit, m_VerticalLimit);
            }
            
            // 触摸输入处理（适用于移动设备）
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Moved)
            {
                Touch touch = Input.GetTouch(0);
                float touchX = touch.deltaPosition.x * 0.1f * m_HorizontalSensitivity;
                float touchY = touch.deltaPosition.y * 0.1f * m_VerticalSensitivity;
                
                m_TargetRotation.y += touchX;
                m_TargetRotation.x -= touchY;
                
                // 限制垂直角度范围
                m_TargetRotation.x = Mathf.Clamp(m_TargetRotation.x, -m_VerticalLimit, m_VerticalLimit);
            }
        }

        /// <summary>
        /// 平滑地应用旋转到相机
        /// </summary>
        private void ApplyRotation()
        {
            // 使用SmoothDamp平滑过渡到目标旋转
            m_CurrentRotation = Vector3.SmoothDamp(
                m_CurrentRotation, 
                m_TargetRotation, 
                ref m_RotationVelocity, 
                m_SmoothTime
            );
            
            // 应用旋转到相机
            transform.localRotation = Quaternion.Euler(m_CurrentRotation);
        }
    }
} 