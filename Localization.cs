using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;

namespace CodexTokenWidget;

public static class Localization
{
    private static readonly Dictionary<string, string> English = new()
    {
        ["Codex token 使用量"] = "Codex token usage",
        ["读取中"] = "Loading",
        ["读取中…"] = "Loading…",
        ["设置"] = "Settings",
        ["刷新"] = "Refresh",
        ["最小化到托盘"] = "Minimize to tray",
        ["退出"] = "Exit",
        ["5小时"] = "5-hour limit",
        ["周限额"] = "Weekly limit",
        ["今天 token 使用量"] = "Today's tokens",
        ["app-server 后台统计"] = "app-server statistics",
        ["近7天 token 使用量"] = "Tokens · last 7 days",
        ["累计 Token 数"] = "Lifetime tokens",
        ["峰值 Token 数"] = "Peak daily tokens",
        ["最长聊天时长"] = "Longest turn",
        ["当前连续天数"] = "Current streak",
        ["最长连续天数"] = "Longest streak",
        ["5 小时用量"] = "5-hour usage",
        ["Codex Token Widget 设置"] = "Codex Token Widget Settings",
        ["自动刷新"] = "Auto-refresh",
        ["启用自动刷新"] = "Enable auto-refresh",
        ["刷新频率"] = "Interval",
        ["每 1 分钟"] = "Every 1 minute",
        ["每 3 分钟"] = "Every 3 minutes",
        ["每 5 分钟"] = "Every 5 minutes",
        ["每 10 分钟"] = "Every 10 minutes",
        ["每 15 分钟"] = "Every 15 minutes",
        ["每 30 分钟"] = "Every 30 minutes",
        ["每 60 分钟"] = "Every 60 minutes",
        ["启动"] = "Startup",
        ["开机自启动"] = "Start with Windows",
        ["跟随 Codex 启动"] = "Show when Codex starts",
        ["外观"] = "Appearance",
        ["主题"] = "Theme",
        ["跟随系统"] = "Follow system",
        ["白天"] = "Light",
        ["黑夜"] = "Dark",
        ["边缘进度条"] = "Edge indicator",
        ["大小"] = "Size",
        ["小（默认）"] = "Small (default)",
        ["标准"] = "Standard",
        ["大"] = "Large",
        ["显示卡片"] = "Visible cards",
        ["5 小时与周限额"] = "5-hour and weekly limits",
        ["近 7 天柱状图"] = "Last 7 days chart",
        ["累计统计"] = "Statistics",
        ["取消"] = "Cancel",
        ["保存"] = "Save",
        ["语言"] = "Language",
        ["界面语言"] = "Display language",
        ["中文"] = "中文",
        ["English"] = "English",
        ["未知"] = "Unknown",
        ["版本 {0}"] = "Version {0}",
        ["今天 Token 总量：{0:N0}\n来源：{1}"] = "Today's tokens: {0:N0}\nSource: {1}",
        ["本机会话实时统计（估算）"] = "Local sessions (estimate)",
        ["app-server 后台统计（可能延迟）"] = "app-server (may be delayed)",
        ["等待 app-server 同步当天数据"] = "Waiting for daily sync",
        ["刷新 {0:HH:mm} · 限额为缓存数据"] = "Updated {0:HH:mm} · cached limits",
        ["刷新 {0:HH:mm}"] = "Updated {0:HH:mm}",
        ["读取失败"] = "Failed to load",
        ["限额暂不可用"] = "Limits unavailable",
        ["部分数据暂不可用"] = "Some data unavailable",
        ["部分数据为上次缓存"] = "Some data cached",
        ["Codex 后台查询超时，请稍后刷新。"] = "Codex query timed out. Please refresh later.",
        ["读取失败，保留上次数据"] = "Failed · showing previous data",
        ["自动刷新 {0} 分钟"] = "Auto-refresh: {0} min",
        ["自动刷新已关闭"] = "Auto-refresh off",
        ["显示"] = "Show",
        ["已最小化到系统托盘，双击图标可恢复。"] = "Minimized to tray. Double-click the icon to restore.",
        ["5 小时用量：{0}%"] = "5-hour usage: {0}%",
        ["{0}小时{1}分"] = "{0}h {1}m",
        ["{0}分{1}秒"] = "{0}m {1}s",
        ["{0}天"] = "{0} days",
        ["一天"] = "1 day",
        ["{0}: 等待后台同步"] = "{0}: waiting for sync",
        ["Codex Token Widget 启动失败"] = "Codex Token Widget failed to start",
        ["Codex app-server 在 35 秒内没有完成用量查询。请稍后刷新。 "] = "Codex app-server did not respond within 35 seconds. Please refresh later.",
        ["无法启动 Codex app-server。"] = "Unable to start Codex app-server.",
        ["找不到本机 Codex CLI。"] = "Cannot find the local Codex CLI.",
        ["Codex app-server 意外结束。{0}"] = "Codex app-server exited unexpectedly. {0}",
        ["Codex app-server 返回错误：{0}"] = "Codex app-server returned an error: {0}",
        ["Codex app-server 响应缺少 result 字段。"] = "Codex app-server response is missing the result field."
    };

    [DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();

    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("zh-CN");
    public static bool IsEnglish => Culture.TwoLetterISOLanguageName == "en";

    public static CultureInfo ResolveCulture(string? mode, CultureInfo systemCulture)
    {
        var requested = string.Equals(mode, "System", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(mode)
            ? systemCulture.Name : mode;
        return CultureInfo.GetCultureInfo(requested.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en-US" : "zh-CN");
    }

    public static void Apply(string? mode)
    {
        Culture = ResolveCulture(mode, CultureInfo.GetCultureInfo(GetUserDefaultUILanguage()));
        if (System.Windows.Application.Current?.Resources is { } resources)
        {
            resources["Loc0"] = Text("Codex token 使用量");
            resources["Loc1"] = Text("读取中");
            resources["Loc2"] = Text("读取中…");
            resources["Loc3"] = Text("设置");
            resources["Loc4"] = Text("刷新");
            resources["Loc5"] = Text("最小化到托盘");
            resources["Loc6"] = Text("退出");
            resources["Loc7"] = Text("5小时");
            resources["Loc8"] = Text("周限额");
            resources["Loc9"] = Text("今天 token 使用量");
            resources["Loc10"] = Text("app-server 后台统计");
            resources["Loc11"] = Text("近7天 token 使用量");
            resources["Loc12"] = Text("累计 Token 数");
            resources["Loc13"] = Text("峰值 Token 数");
            resources["Loc14"] = Text("最长聊天时长");
            resources["Loc15"] = Text("当前连续天数");
            resources["Loc16"] = Text("最长连续天数");
            resources["Loc17"] = Text("5 小时用量");
            resources["Loc18"] = Text("Codex Token Widget 设置");
            resources["Loc19"] = Text("自动刷新");
            resources["Loc20"] = Text("启用自动刷新");
            resources["Loc21"] = Text("刷新频率");
            resources["Loc22"] = Text("每 1 分钟");
            resources["Loc23"] = Text("每 3 分钟");
            resources["Loc24"] = Text("每 5 分钟");
            resources["Loc25"] = Text("每 10 分钟");
            resources["Loc26"] = Text("每 15 分钟");
            resources["Loc27"] = Text("每 30 分钟");
            resources["Loc28"] = Text("每 60 分钟");
            resources["Loc29"] = Text("启动");
            resources["Loc30"] = Text("开机自启动");
            resources["Loc31"] = Text("跟随 Codex 启动");
            resources["Loc32"] = Text("外观");
            resources["Loc33"] = Text("主题");
            resources["Loc34"] = Text("跟随系统");
            resources["Loc35"] = Text("白天");
            resources["Loc36"] = Text("黑夜");
            resources["Loc37"] = Text("边缘进度条");
            resources["Loc38"] = Text("大小");
            resources["Loc39"] = Text("小（默认）");
            resources["Loc40"] = Text("标准");
            resources["Loc41"] = Text("大");
            resources["Loc42"] = Text("显示卡片");
            resources["Loc43"] = Text("5 小时与周限额");
            resources["Loc44"] = Text("近 7 天柱状图");
            resources["Loc45"] = Text("累计统计");
            resources["Loc46"] = Text("取消");
            resources["Loc47"] = Text("保存");
            resources["Loc48"] = Text("语言");
            resources["Loc49"] = Text("界面语言");
            resources["Loc50"] = Text("中文");
            resources["Loc51"] = Text("English");
            resources["Loc52"] = Text("未知");
            resources["Loc53"] = Text("版本 {0}");
            resources["Loc54"] = Text("今天 Token 总量：{0:N0}\n来源：{1}");
            resources["Loc55"] = Text("本机会话实时统计（估算）");
            resources["Loc56"] = Text("app-server 后台统计（可能延迟）");
            resources["Loc57"] = Text("等待 app-server 同步当天数据");
            resources["Loc58"] = Text("刷新 {0:HH:mm} · 限额为缓存数据");
            resources["Loc59"] = Text("刷新 {0:HH:mm}");
            resources["Loc60"] = Text("读取失败");
            resources["Loc61"] = Text("读取失败，保留上次数据");
            resources["Loc62"] = Text("自动刷新 {0} 分钟");
            resources["Loc63"] = Text("自动刷新已关闭");
            resources["Loc64"] = Text("显示");
            resources["Loc65"] = Text("已最小化到系统托盘，双击图标可恢复。");
            resources["Loc66"] = Text("5 小时用量：{0}%");
            resources["Loc67"] = Text("{0}小时{1}分");
            resources["Loc68"] = Text("{0}分{1}秒");
            resources["Loc69"] = Text("{0}天");
            resources["Loc70"] = Text("{0}: 等待后台同步");
            resources["Loc71"] = Text("Codex Token Widget 启动失败");
            resources["Loc72"] = Text("Codex app-server 在 35 秒内没有完成用量查询。请稍后刷新。 ");
            resources["Loc73"] = Text("无法启动 Codex app-server。");
            resources["Loc74"] = Text("找不到本机 Codex CLI。");
            resources["Loc75"] = Text("Codex app-server 意外结束。{0}");
            resources["Loc76"] = Text("Codex app-server 返回错误：{0}");
            resources["Loc77"] = Text("Codex app-server 响应缺少 result 字段。");
        }
    }

    public static string Text(string chinese, params object[] args)
    {
        var format = IsEnglish && English.TryGetValue(chinese, out var english) ? english : chinese;
        return args.Length == 0 ? format : string.Format(Culture, format, args);
    }

    public static string FormatNumber(long? value)
    {
        if (value is null) return "--";
        var number = value.Value;
        if (IsEnglish)
        {
            if (number >= 1_000_000_000) return (number / 1_000_000_000d).ToString("0.#", Culture) + "B";
            if (number >= 1_000_000) return (number / 1_000_000d).ToString("0.#", Culture) + "M";
            if (number >= 1_000) return (number / 1_000d).ToString("0.#", Culture) + "K";
        }
        else
        {
            if (number >= 100_000_000) return (number / 100_000_000d).ToString("0.#", Culture) + "亿";
            if (number >= 10_000) return (number / 10_000d).ToString("0.#", Culture) + "万";
        }
        return number.ToString("N0", Culture);
    }

    public static string FormatDuration(long? seconds)
    {
        if (seconds is null) return "--";
        var span = TimeSpan.FromSeconds(seconds.Value);
        return span.TotalHours >= 1
            ? Text("{0}小时{1}分", (int)span.TotalHours, span.Minutes)
            : Text("{0}分{1}秒", span.Minutes, span.Seconds);
    }
}
