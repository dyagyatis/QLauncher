using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MinecraftLauncher.Helpers;
using MinecraftLauncher.Services.LaunchEngine.Models;

namespace MinecraftLauncher.Services.LaunchEngine
{
    public class MinecraftArgumentBuilder
    {
        public List<string> BuildArguments(
            MojangVersionInfo versionInfo,
            LaunchOptions options,
            List<string> classpathJars,
            string nativesDirectory)
        {
            var args = new List<string>();

            args.Add($"-Xmx{options.RamMb}M");
            args.Add($"-Xms{Math.Max(512, options.RamMb / 2)}M");

            var presetFlags = JvmOptimizationHelper.GetOptimizedJvmArguments(options.RamMb, options.JvmPreset, options.CustomJvmArgs);
            foreach (var flag in presetFlags)
            {
                if (!string.IsNullOrWhiteSpace(flag) && !args.Contains(flag))
                {
                    args.Add(flag);
                }
            }

            string classpathStr = string.Join(Path.PathSeparator.ToString(), classpathJars);
            var variables = BuildVariableMap(versionInfo, options, classpathStr, nativesDirectory);

            bool hasClasspathArg = false;
            int javaMajorVer = JavaService.DetectJavaMajorVersion(options.JavaPath);

            if (versionInfo.Arguments?.Jvm != null && versionInfo.Arguments.Jvm.Count > 0)
            {
                var jvmArgList = ParseArgumentList(versionInfo.Arguments.Jvm, variables, options);
                foreach (var arg in jvmArgList)
                {
                    if (javaMajorVer < 24 && arg.StartsWith("--sun-misc-unsafe-memory-access", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (arg == "-cp" || arg == "-classpath" || arg.Contains(classpathStr))
                    {
                        hasClasspathArg = true;
                    }
                    args.Add(arg);
                }
            }

            if (!hasClasspathArg)
            {
                if (!args.Exists(a => a.StartsWith("-Djava.library.path=")))
                {
                    args.Add($"-Djava.library.path={nativesDirectory}");
                }
                if (!args.Exists(a => a.StartsWith("-Dminecraft.launcher.brand=")))
                {
                    args.Add("-Dminecraft.launcher.brand=QLauncher");
                    args.Add("-Dminecraft.launcher.version=2.0.0");
                }
                args.Add("-cp");
                args.Add(classpathStr);
            }

            args.Add(versionInfo.MainClass);

            if (versionInfo.Arguments?.Game != null && versionInfo.Arguments.Game.Count > 0)
            {
                var gameArgList = ParseArgumentList(versionInfo.Arguments.Game, variables, options);
                args.AddRange(gameArgList);
            }
            else if (!string.IsNullOrEmpty(versionInfo.MinecraftArguments))
            {
                string legacyArgs = ReplaceVariables(versionInfo.MinecraftArguments, variables);
                foreach (var token in legacyArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    args.Add(token);
                }
            }

            if (options.IsFullScreen && !args.Contains("--fullscreen"))
            {
                args.Add("--fullscreen");
            }
            if (options.ScreenWidth > 0 && !args.Contains("--width"))
            {
                args.Add("--width");
                args.Add(options.ScreenWidth.ToString());
            }
            if (options.ScreenHeight > 0 && !args.Contains("--height"))
            {
                args.Add("--height");
                args.Add(options.ScreenHeight.ToString());
            }

            if (!string.IsNullOrWhiteSpace(options.ServerIp))
            {
                args.Add("--server");
                args.Add(options.ServerIp);

                if (options.ServerPort.HasValue && options.ServerPort.Value > 0)
                {
                    args.Add("--port");
                    args.Add(options.ServerPort.Value.ToString());
                }
            }

            return args;
        }

        private static Dictionary<string, string> BuildVariableMap(
            MojangVersionInfo versionInfo,
            LaunchOptions options,
            string classpath,
            string nativesDir)
        {
            string gameDir = !string.IsNullOrEmpty(options.InstancePath) && Directory.Exists(options.InstancePath)
                ? options.InstancePath
                : options.GameRootPath;

            string assetsRoot = Path.Combine(options.GameRootPath, "assets");
            string assetIndex = versionInfo.AssetIndex?.Id ?? versionInfo.Assets ?? "legacy";
            string uuid = string.IsNullOrEmpty(options.Uuid) ? Guid.NewGuid().ToString("N") : options.Uuid;
            string token = string.IsNullOrEmpty(options.AccessToken) ? "offline" : options.AccessToken;

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["auth_player_name"] = options.PlayerName,
                ["version_name"] = versionInfo.Id,
                ["game_directory"] = gameDir,
                ["assets_root"] = assetsRoot,
                ["game_assets"] = assetsRoot,
                ["assets_index_name"] = assetIndex,
                ["auth_uuid"] = uuid,
                ["auth_access_token"] = token,
                ["clientid"] = "00000000402b5328",
                ["auth_xuid"] = "0",
                ["user_type"] = "mojang",
                ["version_type"] = versionInfo.Type ?? "release",
                ["natives_directory"] = nativesDir,
                ["library_directory"] = Path.Combine(options.GameRootPath, "libraries"),
                ["classpath_separator"] = Path.PathSeparator.ToString(),
                ["launcher_name"] = "QLauncher",
                ["launcher_version"] = "2.0.0",
                ["classpath"] = classpath,
                ["resolution_width"] = options.ScreenWidth.ToString(),
                ["resolution_height"] = options.ScreenHeight.ToString(),
                ["quickPlayPath"] = "",
                ["quickPlaySingleplayer"] = options.QuickPlaySingleplayer ?? "",
                ["quickPlayMultiplayer"] = options.QuickPlayMultiplayer ?? "",
                ["quickPlayRealms"] = options.QuickPlayRealms ?? ""
            };

            foreach (var kv in options.CustomVariables)
            {
                map[kv.Key] = kv.Value;
            }

            return map;
        }

        private static List<string> ParseArgumentList(
            List<object> rawList,
            Dictionary<string, string> variables,
            LaunchOptions options)
        {
            var result = new List<string>();

            foreach (var item in rawList)
            {
                if (item is JsonElement el)
                {
                    if (el.ValueKind == JsonValueKind.String)
                    {
                        string str = el.GetString() ?? "";
                        result.Add(ReplaceVariables(str, variables));
                    }
                    else if (el.ValueKind == JsonValueKind.Object)
                    {
                        if (EvaluateRuleElement(el, options))
                        {
                            if (el.TryGetProperty("value", out var valEl))
                            {
                                if (valEl.ValueKind == JsonValueKind.String)
                                {
                                    result.Add(ReplaceVariables(valEl.GetString() ?? "", variables));
                                }
                                else if (valEl.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var arrItem in valEl.EnumerateArray())
                                    {
                                        result.Add(ReplaceVariables(arrItem.GetString() ?? "", variables));
                                    }
                                }
                            }
                        }
                    }
                }
                else if (item is string s)
                {
                    result.Add(ReplaceVariables(s, variables));
                }
            }

            return result;
        }

        private static bool EvaluateRuleElement(JsonElement obj, LaunchOptions options)
        {
            if (!obj.TryGetProperty("rules", out var rulesEl)) return true;

            bool allowed = false;
            foreach (var rule in rulesEl.EnumerateArray())
            {
                string action = rule.TryGetProperty("action", out var a) ? a.GetString() ?? "allow" : "allow";
                bool matches = true;

                if (rule.TryGetProperty("os", out var osEl))
                {
                    if (osEl.TryGetProperty("name", out var osName))
                    {
                        if (osName.GetString() != "windows")
                        {
                            matches = false;
                        }
                    }
                    if (osEl.TryGetProperty("arch", out var archEl))
                    {
                        string arch = archEl.GetString() ?? "";
                        bool is64 = Environment.Is64BitOperatingSystem;
                        if ((arch == "x86" && is64) || (arch == "x64" && !is64))
                        {
                            matches = false;
                        }
                    }
                }

                if (rule.TryGetProperty("features", out var featEl))
                {
                    foreach (var feat in featEl.EnumerateObject())
                    {
                        bool expected = feat.Value.GetBoolean();
                        bool actual = false;

                        if (feat.Name == "is_demo_user")
                        {
                            actual = options.IsDemo;
                        }
                        else if (feat.Name == "has_custom_resolution")
                        {
                            actual = options.ScreenWidth > 0 && options.ScreenHeight > 0;
                        }
                        else if (feat.Name == "has_quick_plays_support")
                        {
                            actual = false;
                        }
                        else if (feat.Name == "is_quick_play_singleplayer")
                        {
                            actual = !string.IsNullOrEmpty(options.QuickPlaySingleplayer);
                        }
                        else if (feat.Name == "is_quick_play_multiplayer")
                        {
                            actual = !string.IsNullOrEmpty(options.QuickPlayMultiplayer);
                        }
                        else if (feat.Name == "is_quick_play_realms")
                        {
                            actual = !string.IsNullOrEmpty(options.QuickPlayRealms);
                        }

                        if (actual != expected)
                        {
                            matches = false;
                            break;
                        }
                    }
                }

                if (action == "allow" && matches) allowed = true;
                if (action == "disallow" && matches) allowed = false;
            }

            return allowed;
        }

        private static string ReplaceVariables(string input, Dictionary<string, string> variables)
        {
            if (string.IsNullOrEmpty(input)) return input;

            string result = input;
            foreach (var kv in variables)
            {
                result = result.Replace($"${{{kv.Key}}}", kv.Value, StringComparison.OrdinalIgnoreCase);
            }
            return result;
        }
    }
}
