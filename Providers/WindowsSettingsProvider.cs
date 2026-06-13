using tool_r1ng.Core;
using tool_r1ng.Utilities;

namespace tool_r1ng.Providers;

public sealed class WindowsSettingsProvider : tool_r1ng.Core.IQueryProvider
{
    private const int MaxResults = 10;
    private static readonly IReadOnlyList<WindowsSettingEntry> Entries =
    [
        new("设置主页", "Settings home", "ms-settings:", "home settings 设置"),
        new("系统", "Display, sound, notifications, power", "ms-settings:system", "system display sound power 系统 显示 声音 电源"),
        new("显示", "Display settings", "ms-settings:display", "display monitor brightness 显示 屏幕 亮度"),
        new("声音", "Sound settings", "ms-settings:sound", "sound audio volume microphone 声音 音频 麦克风"),
        new("通知", "Notification settings", "ms-settings:notifications", "notification alerts 通知 提醒"),
        new("电源和电池", "Power and battery", "ms-settings:powersleep", "power battery sleep 电源 电池 睡眠"),
        new("存储", "Storage settings", "ms-settings:storagesense", "storage disk cleanup 存储 磁盘 清理"),
        new("蓝牙和设备", "Bluetooth and devices", "ms-settings:bluetooth", "bluetooth device printer mouse keyboard 蓝牙 设备 打印机 鼠标 键盘"),
        new("鼠标", "Mouse settings", "ms-settings:mousetouchpad", "mouse touchpad 鼠标 触摸板"),
        new("键盘", "Keyboard settings", "ms-settings:keyboard", "keyboard input 键盘 输入"),
        new("打印机和扫描仪", "Printers and scanners", "ms-settings:printers", "printer scanner 打印机 扫描仪"),
        new("网络和 Internet", "Network settings", "ms-settings:network", "network internet wifi ethernet vpn 网络 互联网"),
        new("Wi-Fi", "Wi-Fi settings", "ms-settings:network-wifi", "wifi wireless wlan wi-fi 无线 网络"),
        new("以太网", "Ethernet settings", "ms-settings:network-ethernet", "ethernet lan 以太网 有线 网络"),
        new("VPN", "VPN settings", "ms-settings:network-vpn", "vpn proxy 网络"),
        new("个性化", "Personalization", "ms-settings:personalization", "personalization theme wallpaper colors 个性化 主题 壁纸 颜色"),
        new("背景", "Background settings", "ms-settings:personalization-background", "background wallpaper 背景 壁纸"),
        new("颜色", "Color settings", "ms-settings:colors", "color dark light accent 颜色 深色 浅色"),
        new("任务栏", "Taskbar settings", "ms-settings:taskbar", "taskbar tray 任务栏 托盘"),
        new("应用", "Apps settings", "ms-settings:appsfeatures", "apps uninstall default 应用 卸载 默认"),
        new("默认应用", "Default apps", "ms-settings:defaultapps", "default apps browser 默认 应用 浏览器"),
        new("账户", "Accounts settings", "ms-settings:accounts", "account sign in user 账户 用户 登录"),
        new("登录选项", "Sign-in options", "ms-settings:signinoptions", "login sign in pin hello 登录 密码 pin"),
        new("时间和语言", "Time and language", "ms-settings:dateandtime", "time language date region 时间 语言 日期 区域"),
        new("语言和区域", "Language and region", "ms-settings:regionlanguage", "language region input 中文 语言 区域 输入法"),
        new("辅助功能", "Accessibility", "ms-settings:easeofaccess", "accessibility narrator magnifier 辅助 功能 放大镜"),
        new("隐私和安全", "Privacy and security", "ms-settings:privacy", "privacy security permissions 隐私 安全 权限"),
        new("Windows 更新", "Windows Update", "ms-settings:windowsupdate", "windows update 更新"),
        new("恢复", "Recovery settings", "ms-settings:recovery", "recovery reset restore 恢复 重置"),
        new("开发者选项", "For developers", "ms-settings:developers", "developer sideload terminal 开发者")
    ];

    public string Id => "windows-settings";

    public string Name => "Settings";

    public ValueTask<IReadOnlyList<QueryResult>> QueryAsync(QueryContext context, CancellationToken cancellationToken)
    {
        if (!context.IsWindowsSettingsQuery)
        {
            return ValueTask.FromResult<IReadOnlyList<QueryResult>>(Array.Empty<QueryResult>());
        }

        var query = context.WindowsSettingsQuery;
        var results = Entries
            .Select(entry => new
            {
                Entry = entry,
                Match = string.IsNullOrWhiteSpace(query)
                    ? new FuzzyMatchResult(80, [])
                    : FuzzyMatcher.Match(entry.SearchText, query)
            })
            .Where(item => string.IsNullOrWhiteSpace(query) || item.Match.Score >= 24)
            .OrderByDescending(item => item.Match.Score)
            .ThenBy(item => item.Entry.Title)
            .Take(MaxResults)
            .Select(item => CreateResult(item.Entry, query, item.Match))
            .ToList();

        return ValueTask.FromResult<IReadOnlyList<QueryResult>>(results);
    }

    private static QueryResult CreateResult(WindowsSettingEntry entry, string query, FuzzyMatchResult match)
    {
        var titleMatch = string.IsNullOrWhiteSpace(query)
            ? FuzzyMatchResult.Empty
            : FuzzyMatcher.Match(entry.Title, query);

        return new QueryResult
        {
            Title = entry.Title,
            HighlightedTitle = HighlightBuilder.Build(entry.Title, titleMatch.MatchedIndices),
            Subtitle = entry.Subtitle,
            IconGlyph = "\uE713",
            ProviderId = "windows-settings",
            ProviderName = "Settings",
            Score = 300 + match.Score,
            ExecuteAsync = _ => ProcessLauncher.OpenAsync(entry.Uri)
        };
    }

    private sealed record WindowsSettingEntry(
        string Title,
        string Subtitle,
        string Uri,
        string Keywords)
    {
        public string SearchText => $"{Title} {Subtitle} {Keywords}";
    }
}
