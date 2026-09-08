using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Iridium.Enums;
using Iridium.Utilities;
using Iridium.Models.Minecraft;

namespace Iridium.Minecraft;

internal static class VersionArgumentRuleParser {
    public static string GetCurrentOsArch() =>
            PlatformHelper.Architecture switch {
                Architecture.X86 => "x86",
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                var architecture => architecture.ToString().ToLowerInvariant()
            };

    public static bool IsActive(IReadOnlyList<CompatibilityRule>? rules, Dictionary<string, bool> features) {
        if (rules is null || rules.Count == 0)
            return true;

        var allowed = false;
        foreach (var rule in rules) {
            if (!IsMatched(rule, features))
                continue;

            if (rule.Action == CompatibilityRuleAction.Disallow)
                return false;

            allowed = true;
        }

        return allowed;
    }

    public static string? GetNativeClassifier(IReadOnlyDictionary<string, string> natives) {
        // Minecraft version JSON uses "osx" for macOS in natives keys.
        // Try all known aliases: "osx", "macos", and the launcher's internal name.
        var osNames = new[] { "osx", "macos" };
        var currentOs = PlatformHelper.GetPlatformName();

        // Try arch-specific keys first, then plain OS keys.
        foreach (var osName in osNames) {
            var archKey = RuntimeInformation.ProcessArchitecture switch {
                Architecture.Arm64 => $"{osName}-arm64",
                Architecture.Arm => $"{osName}-arm32",
                _ => osName
            };

            if (natives.TryGetValue(archKey, out var classifier))
                return classifier;

            if (archKey != osName && natives.TryGetValue(osName, out var fallback))
                return fallback;
        }

        // Last resort: try the launcher's internal OS name.
        var launcherArchKey = RuntimeInformation.ProcessArchitecture switch {
            Architecture.Arm64 => $"{currentOs}-arm64",
            Architecture.Arm => $"{currentOs}-arm32",
            _ => currentOs
        };
        if (natives.TryGetValue(launcherArchKey, out var launcherClassifier))
            return launcherClassifier;
        if (launcherArchKey != currentOs && natives.TryGetValue(currentOs, out var launcherFallback))
            return launcherFallback;

        return null;
    }

    private static bool IsMatched(CompatibilityRule rule, Dictionary<string, bool> features) {
            if (rule.OsName is not null) {
                var currentOs = PlatformHelper.GetPlatformName();
                // Minecraft version JSON rules use "osx" for macOS, while the launcher
                // uses "macos" for directory naming. Normalize both sides for comparison.
                var normalizedRule = rule.OsName.Equals("osx", StringComparison.OrdinalIgnoreCase)
                    ? "macos"
                    : rule.OsName;
                if (!string.Equals(currentOs, normalizedRule, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (rule.OsVersion is not null &&
                !Regex.IsMatch(Environment.OSVersion.Version.ToString(), rule.OsVersion))
                return false;

            if (rule.OsArch is not null &&
                !string.Equals(GetCurrentOsArch(), rule.OsArch, StringComparison.OrdinalIgnoreCase))
                return false;

            if (rule.Features is null)
                return true;

            foreach (var (key, value) in rule.Features)
                if (!features.TryGetValue(key, out var current) || current != value)
                    return false;

            return true;
        }
}
