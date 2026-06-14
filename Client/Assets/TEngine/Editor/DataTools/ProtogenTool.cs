
using System.Collections.Generic;

using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using UnityEditor;
using Debug = UnityEngine.Debug;

namespace TEngine.Editor
{
    public static class EditorProtogenTool
    {
        private static string protoCsFolder = "Assets/GameScripts/HotFix/GameProto/Proto";
        private static string outputFolder = "Assets/GameScripts/HotFix/GameProto/ProtoGenerated";

        private const int BaseMultiplier = 256;

        public static void GenerateProtos()
        {
            // 获取绝对路径确保路径一致性
            string protoFolder = Path.GetFullPath("../Configs/protos");
            string protogenTool = Path.GetFullPath("../Tools/protogen/protogen.exe");
            string protoScriptFolder = Path.GetFullPath("./Assets/GameScripts/HotFix/GameProto/Proto");

            // 清空代码文件夹下所有cs文件
            if (Directory.Exists(protoScriptFolder))
            {
                Directory.Delete(protoScriptFolder, true);
                Directory.CreateDirectory(protoScriptFolder); // 立即重建目录
            }
            else
            {
                Directory.CreateDirectory(protoScriptFolder);
            }

            // 获取所有proto文件（包括子目录）
            if (!Directory.Exists(protoFolder))
            {
                Debug.Log($"Proto目录不存在: {protoFolder}");
                return;
            }

            var protoFiles = Directory.GetFiles(protoFolder, "*.proto", SearchOption.AllDirectories);
            if (protoFiles.Length == 0)
            {
                Debug.LogWarning("未找到任何proto文件");
                return;
            }

            // 构建protogen命令
            StringBuilder cmdArgs = new StringBuilder();
            cmdArgs.Append(" +names=original");
            cmdArgs.Append(" --proto_path=\"");
            cmdArgs.Append(protoFolder);
            cmdArgs.Append("\"");
            cmdArgs.Append(" --csharp_out=\"");
            cmdArgs.Append(protoScriptFolder);
            cmdArgs.Append("\"");

            // 添加所有proto文件（使用相对路径）
            foreach (string file in protoFiles)
            {
                // 获取相对于proto根目录的相对路径
                string relativePath = Path.GetRelativePath(protoFolder, file)
                    .Replace(Path.DirectorySeparatorChar, '/'); // 统一使用正斜杠

                cmdArgs.Append(" \"");
                cmdArgs.Append(relativePath);
                cmdArgs.Append("\"");
            }

            Debug.Log("执行命令:" + protogenTool + cmdArgs.ToString());

            // 配置进程启动信息
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = protogenTool,
                Arguments = cmdArgs.ToString(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // 执行命令
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();

                // 输出日志
                string output = process.StandardOutput.ReadToEnd();
                string errors = process.StandardError.ReadToEnd();

                if (!string.IsNullOrEmpty(output))
                    Debug.Log("Protogen输出: " + output);

                if (process.ExitCode != 0)
                {
                    Debug.LogError($"Protogen失败! 退出代码: {process.ExitCode}");
                    if (!string.IsNullOrEmpty(errors))
                        Debug.LogError("错误信息: " + errors);
                }
                else
                {
                    Debug.Log($"成功生成 {protoFiles.Length} 个proto文件");
                }
            }

            GenerateMessageIds();

            // 刷新Unity工程
            AssetDatabase.Refresh();
        }

        private static void GenerateMessageIds()
        {
            if (!Directory.Exists(protoCsFolder))
            {
                Debug.LogError($"[ProtoMessageId] 目录不存在：{protoCsFolder}");
                EditorUtility.DisplayDialog("错误", $"目录不存在：{protoCsFolder}", "OK");
                return;
            }

            var files = Directory.GetFiles(protoCsFolder, "*.cs", SearchOption.AllDirectories);
            Debug.Log($"[ProtoMessageId] 扫描目录：{protoCsFolder}，找到 {files.Length} 个 .cs 文件（包含子目录）");

            var namespaceMap = new Dictionary<string, (int main, List<(string name, int sub)> subs)>();

            foreach (var file in files)
            {
                Debug.Log($"[ProtoMessageId] 解析文件：{file}");
                var text = File.ReadAllText(file);
                var index = 0;

                while (TryReadNamespace(text, ref index, out var namespaceName, out var body))
                {
                    ParseEnums(namespaceName, body, namespaceMap);
                }
            }

            if (namespaceMap.Count == 0)
            {
                Debug.LogWarning("[ProtoMessageId] 未解析到任何 main/sub 枚举，生成终止。");
                EditorUtility.DisplayDialog("提示", "未解析到 main/sub 枚举，请检查源文件。", "OK");
                return;
            }

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
                Debug.Log($"[ProtoMessageId] 创建输出目录：{outputFolder}");
            }

            var outputPath = Path.Combine(outputFolder, "ProtoMessageId.cs");
            File.WriteAllText(outputPath, BuildSource(namespaceMap), Encoding.UTF8);
            Debug.Log($"[ProtoMessageId] 生成成功，输出文件：{outputPath}");
        }

        private static bool TryReadNamespace(string text, ref int searchIndex, out string namespaceName, out string body)
        {
            namespaceName = string.Empty;
            body = string.Empty;

            var nsMatch = Regex.Match(text.Substring(searchIndex), @"namespace\s+([^\s{]+)\s*\{", RegexOptions.Singleline);
            if (!nsMatch.Success)
            {
                return false;
            }

            namespaceName = nsMatch.Groups[1].Value.Trim();
            var braceStart = searchIndex + nsMatch.Index + nsMatch.Length - 1;
            var braceCount = 0;

            for (var i = braceStart; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    braceCount++;
                }
                else if (text[i] == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        var start = braceStart + 1;
                        var length = i - start;
                        body = text.Substring(start, length);
                        searchIndex = i + 1;
                        Debug.Log($"[ProtoMessageId] 命中 namespace: {namespaceName}，长度: {length}");
                        return true;
                    }
                }
            }

            Debug.LogWarning($"[ProtoMessageId] namespace {namespaceName} 括号不匹配，跳过。");
            return false;
        }

        private static void ParseEnums(string namespaceName, string body,
            IDictionary<string, (int main, List<(string name, int sub)> subs)> map)
        {
            var mainMatch = Regex.Match(body, @"enum\s+main\s*\{([\s\S]*?)\}", RegexOptions.Multiline);
            var subMatch = Regex.Match(body, @"enum\s+sub\s*\{([\s\S]*?)\}", RegexOptions.Multiline);

            if (!mainMatch.Success || !subMatch.Success)
            {
                Debug.LogWarning($"[ProtoMessageId] namespace {namespaceName} 未找到 main/sub 枚举，跳过。");
                return;
            }

            var mainValue = ParseEnumFirstValue(mainMatch.Groups[1].Value);
            var subValues = ParseEnumEntries(subMatch.Groups[1].Value);

            Debug.Log($"[ProtoMessageId] namespace {namespaceName} main = {mainValue}，sub 条目 = {subValues.Count}");

            if (!map.TryGetValue(namespaceName, out var value))
            {
                value = (mainValue, new List<(string, int)>());
            }

            value.main = mainValue;
            value.subs.AddRange(subValues);
            map[namespaceName] = value;
        }

        private static int ParseEnumFirstValue(string enumBody)
        {
            var match = Regex.Match(enumBody, @"=\s*(\d+)");
            var value = match.Success ? int.Parse(match.Groups[1].Value) : 0;
            Debug.Log($"[ProtoMessageId] main 枚举首值 = {value}");
            return value;
        }

        private static List<(string name, int value)> ParseEnumEntries(string enumBody)
        {
            var list = new List<(string name, int value)>();
            foreach (Match match in Regex.Matches(enumBody, @"(\w+)\s*=\s*(\d+)", RegexOptions.Multiline))
            {
                var entry = (match.Groups[1].Value, int.Parse(match.Groups[2].Value));
                list.Add(entry);
                Debug.Log($"[ProtoMessageId] sub 枚举成员：{entry.Item1} = {entry.Item2}");
            }
            return list;
        }

        private static string BuildSource(Dictionary<string, (int main, List<(string name, int sub)> subs)> map)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("// Generated by Proto MessageId generator.");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine();

            foreach (var pair in map)
            {
                sb.AppendLine($"namespace {pair.Key}");
                sb.AppendLine("{");
                sb.AppendLine("    public static partial class MessageId");
                sb.AppendLine("    {");
                foreach (var (name, sub) in pair.Value.subs)
                {
                    var value = pair.Value.main * BaseMultiplier + sub;
                    sb.AppendLine($"        public const int {name} = {value};");
                }
                sb.AppendLine("    }");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}