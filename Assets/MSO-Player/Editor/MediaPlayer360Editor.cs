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

            if (GUI.changed)
            {
                EditorUtility.SetDirty(mediaPlayer);
            }
        }
    }
} 