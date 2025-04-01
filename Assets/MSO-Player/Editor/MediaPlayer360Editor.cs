using UnityEditor;
using UnityEngine;
using yan.libvlc;

namespace yan.libvlc
{
    /// <summary>
    /// 360度全景媒体播放器的自定义编辑器
    /// </summary>
    [CustomEditor(typeof(MediaPlayer360))]
    public class MediaPlayer360Editor : Editor
    {
        public override void OnInspectorGUI()
        {
            MediaPlayer360 mediaPlayer = (MediaPlayer360)target;

            DrawDefaultInspector();

            // 添加分隔线和标题
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("全景视频控制", EditorStyles.boldLabel);

            // 显示当前媒体状态（只读）
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("当前状态", GUILayout.Width(120));
            EditorGUILayout.LabelField(mediaPlayer.CurrentMediaState.ToString(), EditorStyles.helpBox);
            EditorGUILayout.EndHorizontal();

            // 控制按钮
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("播放"))
            {
                mediaPlayer.Play();
            }

            if (GUILayout.Button("暂停"))
            {
                mediaPlayer.Pause();
            }

            if (GUILayout.Button("停止"))
            {
                mediaPlayer.Stop();
            }

            if (GUILayout.Button("刷新"))
            {
                mediaPlayer.Refresh();
            }

            EditorGUILayout.EndHorizontal();

            // 翻转控制
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("视频翻转控制", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("水平翻转"))
            {
                SerializedProperty flipH = serializedObject.FindProperty("m_FlipHorizontal");
                flipH.boolValue = !flipH.boolValue;
                serializedObject.ApplyModifiedProperties();
                mediaPlayer.SetHorizontalFlip(flipH.boolValue);
            }
            
            if (GUILayout.Button("垂直翻转"))
            {
                SerializedProperty flipV = serializedObject.FindProperty("m_FlipVertical");
                flipV.boolValue = !flipV.boolValue;
                serializedObject.ApplyModifiedProperties();
                mediaPlayer.SetVerticalFlip(flipV.boolValue);
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 旋转控制
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("视频旋转控制", EditorStyles.boldLabel);
            
            // 获取当前旋转值
            SerializedProperty rotationProp = serializedObject.FindProperty("m_TextureRotation");
            EditorGUILayout.PropertyField(rotationProp, new GUIContent("当前旋转角度"));
            
            // 旋转按钮
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("不旋转 (0°)"))
            {
                rotationProp.enumValueIndex = (int)MediaPlayer360.TextureRotation.None;
                serializedObject.ApplyModifiedProperties();
                mediaPlayer.SetTextureRotation(MediaPlayer360.TextureRotation.None);
            }
            
            if (GUILayout.Button("顺时针 90°"))
            {
                rotationProp.enumValueIndex = (int)MediaPlayer360.TextureRotation.CW_90;
                serializedObject.ApplyModifiedProperties();
                mediaPlayer.SetTextureRotation(MediaPlayer360.TextureRotation.CW_90);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("旋转 180°"))
            {
                rotationProp.enumValueIndex = (int)MediaPlayer360.TextureRotation.CW_180;
                serializedObject.ApplyModifiedProperties();
                mediaPlayer.SetTextureRotation(MediaPlayer360.TextureRotation.CW_180);
            }
            
            if (GUILayout.Button("逆时针 90°"))
            {
                rotationProp.enumValueIndex = (int)MediaPlayer360.TextureRotation.CCW_90;
                serializedObject.ApplyModifiedProperties();
                mediaPlayer.SetTextureRotation(MediaPlayer360.TextureRotation.CCW_90);
            }
            
            EditorGUILayout.EndHorizontal();

            // 设置辅助功能
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("设置辅助", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("设置全景材质"))
            {
                SetupPanoramicMaterial(mediaPlayer.gameObject);
            }
            
            if (GUILayout.Button("翻转球体法线"))
            {
                FlipSphereNormals(mediaPlayer.gameObject);
            }
            
            EditorGUILayout.EndHorizontal();

            // 相机辅助功能
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("相机设置辅助", EditorStyles.boldLabel);
            
            if (GUILayout.Button("在球体中心创建相机"))
            {
                CreateCameraInSphere(mediaPlayer.gameObject);
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(mediaPlayer);
            }
        }
        
        /// <summary>
        /// 设置全景材质
        /// </summary>
        private void SetupPanoramicMaterial(GameObject sphere)
        {
            MeshRenderer renderer = sphere.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                Debug.LogError("对象没有MeshRenderer组件");
                return;
            }
            
            // 查找Skybox/Panoramic着色器
            Shader panoramicShader = Shader.Find("Skybox/Panoramic");
            if (panoramicShader == null)
            {
                Debug.LogError("未找到Skybox/Panoramic着色器，请确保使用了支持此着色器的Unity版本");
                return;
            }
            
            // 创建新材质
            Material panoramicMaterial = new Material(panoramicShader);
            panoramicMaterial.name = "360PanoramicMaterial";
            
            // 应用材质
            renderer.sharedMaterial = panoramicMaterial;
            
            // 标记为脏，确保更改被保存
            EditorUtility.SetDirty(renderer);
            
            Debug.Log("全景材质已成功应用");
        }
        
        /// <summary>
        /// 翻转球体的法线，使其朝内而不是朝外
        /// </summary>
        private void FlipSphereNormals(GameObject sphere)
        {
            MeshFilter meshFilter = sphere.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogError("对象没有MeshFilter组件或网格为空");
                return;
            }
            
            // 复制网格以避免修改原始资产
            Mesh originalMesh = meshFilter.sharedMesh;
            Mesh mesh = new Mesh();
            mesh.name = originalMesh.name + "_Inverted";
            
            // 复制顶点和UV
            mesh.vertices = originalMesh.vertices;
            mesh.uv = originalMesh.uv;
            mesh.uv2 = originalMesh.uv2;
            mesh.colors = originalMesh.colors;
            
            // 复制但反转三角形顺序
            int[] triangles = originalMesh.triangles;
            int[] newTriangles = new int[triangles.Length];
            
            for (int i = 0; i < triangles.Length; i += 3)
            {
                newTriangles[i] = triangles[i];
                newTriangles[i + 1] = triangles[i + 2];
                newTriangles[i + 2] = triangles[i + 1];
            }
            
            mesh.triangles = newTriangles;
            
            // 反转法线
            Vector3[] normals = originalMesh.normals;
            Vector3[] newNormals = new Vector3[normals.Length];
            
            for (int i = 0; i < normals.Length; i++)
            {
                newNormals[i] = -normals[i];
            }
            
            mesh.normals = newNormals;
            
            // 重新计算边界
            mesh.RecalculateBounds();
            
            // 应用到网格过滤器
            meshFilter.mesh = mesh;
            
            // 标记为脏，确保更改被保存
            EditorUtility.SetDirty(meshFilter);
            
            Debug.Log("球体法线已成功翻转，现在视角将在球体内部");
        }
        
        /// <summary>
        /// 在球体中心创建相机
        /// </summary>
        private void CreateCameraInSphere(GameObject sphere)
        {
            // 检查是否已经存在子相机
            Transform existingCamera = sphere.transform.Find("360Camera");
            if (existingCamera != null)
            {
                Debug.Log("球体中已存在360相机");
                Selection.activeGameObject = existingCamera.gameObject;
                return;
            }
            
            // 创建新相机
            GameObject cameraObj = new GameObject("360Camera");
            cameraObj.transform.SetParent(sphere.transform);
            cameraObj.transform.localPosition = Vector3.zero;
            cameraObj.transform.localRotation = Quaternion.identity;
            
            // 添加相机组件
            Camera cam = cameraObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 90f;
            cam.nearClipPlane = 0.01f;
            
            // 添加相机控制器组件
            cameraObj.AddComponent<CameraController360>();
            
            Debug.Log("360相机已创建，并自动添加了相机控制器组件");
            
            // 选中新创建的相机
            Selection.activeGameObject = cameraObj;
        }
    }
} 