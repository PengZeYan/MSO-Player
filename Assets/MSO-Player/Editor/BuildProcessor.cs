using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace yan.libvlc
{
    /// <summary>
    /// LibVLC构建处理器，负责在构建应用程序后复制必要的LibVLC插件文件
    /// </summary>
    public class BuildProcessor : IPostprocessBuildWithReport
    {
        /// <summary>
        /// 回调执行顺序，值越小越早执行
        /// </summary>
        public int callbackOrder { get => 0; }

        /// <summary>
        /// 构建后处理，将LibVLC插件复制到构建目录
        /// </summary>
        /// <param name="report">构建报告</param>
        public void OnPostprocessBuild(BuildReport report)
        {
            // 仅在Windows平台执行
            if (report.summary.platform != BuildTarget.StandaloneWindows && 
                report.summary.platform != BuildTarget.StandaloneWindows64)
            {
                //Debug.Log("LibVLC插件复制仅支持Windows平台，当前构建平台跳过");
                return;
            }

            // 获取构建输出路径
            string executablePath = report.summary.outputPath;
            string outputPath = Path.GetDirectoryName(executablePath);
            string executableName = Path.GetFileNameWithoutExtension(executablePath);
            if (string.IsNullOrEmpty(outputPath) || string.IsNullOrEmpty(executableName))
            {
                Debug.LogError($"无法解析Windows构建输出路径: {executablePath}");
                return;
            }

            // 构建目标插件路径
            string targetPath = Path.Combine(outputPath, executableName + "_Data", "Plugins", "x86_64");

            // 查找LibVLC源目录
            string sourcePath = FindLibVLCSourcePath();

            if (string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogError("未能找到LibVLC插件目录，构建后处理失败！");
                return;
            }

            // 开始复制文件
            //Debug.Log($"正在复制LibVLC插件到: {targetPath}");
            CopyDirectory(sourcePath, targetPath);
            //Debug.Log("LibVLC插件复制完成");
        }

        /// <summary>
        /// 查找LibVLC源路径，首先在标准位置查找，然后在整个Assets目录递归查找
        /// </summary>
        /// <returns>LibVLC源目录路径，未找到则返回null</returns>
        private string FindLibVLCSourcePath()
        {
            // 首先查找标准位置
            string standardPath = FindLibVLCInPlugins("Assets/Plugins/x86_64");
            if (!string.IsNullOrEmpty(standardPath))
            {
                return standardPath;
            }

            // 如果标准位置未找到，在整个Assets目录递归查找
            return FindLibVLCInAllPlugins("Assets", "libvlc");
        }

        /// <summary>
        /// 在指定的插件路径中查找LibVLC目录
        /// </summary>
        /// <param name="path">插件路径</param>
        /// <returns>找到的LibVLC目录路径，未找到则返回null</returns>
        private string FindLibVLCInPlugins(string path)
        {
            string basePath = Path.Combine(Application.dataPath, "../", path);

            if (!Directory.Exists(basePath))
            {
                return null;
            }

            foreach (string dir in Directory.EnumerateDirectories(basePath))
            {
                if (dir.Contains("libvlc"))
                {
                    return dir;
                }
            }

            return null;
        }

        /// <summary>
        /// 在整个项目中递归查找LibVLC目录
        /// </summary>
        /// <param name="root">搜索根目录</param>
        /// <param name="pluginFolderName">插件文件夹名称关键字</param>
        /// <returns>找到的LibVLC目录路径，未找到则返回null</returns>
        private string FindLibVLCInAllPlugins(string root, string pluginFolderName)
        {
            string basePath = Path.Combine(Application.dataPath, "../", root);

            if (!Directory.Exists(basePath))
            {
                return null;
            }

            // 直接查找包含主库的libvlc目录，避免为每个子目录拼接无效路径。
            foreach (string dir in Directory.EnumerateDirectories(basePath, pluginFolderName, SearchOption.AllDirectories))
            {
                if (File.Exists(Path.Combine(dir, "libvlc.dll")))
                {
                    return dir;
                }
            }

            return null;
        }

        /// <summary>
        /// 递归复制目录及其内容
        /// </summary>
        /// <param name="sourceDir">源目录</param>
        /// <param name="targetDir">目标目录</param>
        private void CopyDirectory(string sourceDir, string targetDir)
        {
            // 确保目标目录存在
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // 每次构建都覆盖目标文件，避免“大小相同但内容不同”被误判为无需更新。
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                if (file.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDir, fileName);
                File.Copy(file, destFile, true);
                //Debug.Log($"复制文件: {fileName}");
            }

            // 递归复制所有子目录
            foreach (string directory in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(directory);
                string destDir = Path.Combine(targetDir, dirName);
                CopyDirectory(directory, destDir);
            }
        }
    }
}
