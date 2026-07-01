using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Security.Cryptography;
using System.Text;

namespace GlyphStash.Localization;

public static class AppText
{
    public const string DefaultCultureCode = "zh-CN";
    public const string EnglishCultureCode = "en-US";

    public static IReadOnlyList<SupportedLanguage> SupportedLanguages { get; } =
    [
        new(DefaultCultureCode, "简体中文"),
        new(EnglishCultureCode, "English")
    ];

    private static readonly ResourceManager ResourceManager = new(
        "GlyphStash.Localization.Resources.AppStrings",
        typeof(AppText).Assembly);

    private static CultureInfo s_currentCulture = ResolveCulture(CultureInfo.CurrentUICulture.Name);

    static AppText()
    {
        ApplyCulture(s_currentCulture);
    }

    public static CultureInfo CurrentCulture => s_currentCulture;

    public static string CurrentCultureCode => s_currentCulture.Name;

    public static event EventHandler? CultureChanged;

    public static CultureInfo ResolveCulture(string? cultureCode)
    {
        if (!string.IsNullOrWhiteSpace(cultureCode))
        {
            foreach (var language in SupportedLanguages)
            {
                if (string.Equals(cultureCode, language.CultureCode, StringComparison.OrdinalIgnoreCase)
                    || cultureCode.StartsWith(language.CultureCode.Split('-')[0], StringComparison.OrdinalIgnoreCase))
                {
                    return CultureInfo.GetCultureInfo(language.CultureCode);
                }
            }
        }

        var current = CultureInfo.CurrentUICulture.Name;
        if (current.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo(EnglishCultureCode);
        }

        return CultureInfo.GetCultureInfo(DefaultCultureCode);
    }

    public static void SetCulture(string? cultureCode)
    {
        var culture = ResolveCulture(cultureCode);
        if (string.Equals(s_currentCulture.Name, culture.Name, StringComparison.OrdinalIgnoreCase))
        {
            ApplyCulture(culture);
            return;
        }

        s_currentCulture = culture;
        ApplyCulture(culture);
        CultureChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Get(string key)
    {
        var value = ResourceManager.GetString(key, s_currentCulture)
                    ?? ResourceManager.GetString(key, CultureInfo.GetCultureInfo(DefaultCultureCode));
        return string.IsNullOrEmpty(value) ? key : value;
    }

    public static string Format(string key, params object?[] args) =>
        string.Format(s_currentCulture, Get(key), args);

    public static string FormatLiteral(string zhTemplate, string enTemplate, params object?[] args) =>
        string.Format(
            s_currentCulture,
            s_currentCulture.Name == EnglishCultureCode ? enTemplate : zhTemplate,
            args);

    public static string TranslateLiteral(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || s_currentCulture.Name == DefaultCultureCode)
        {
            return text;
        }

        return EnglishLiterals.TryGetValue(text, out var translated) ? translated : text;
    }

    public static IReadOnlyDictionary<string, string> GetCurrentStrings()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var neutralSet = ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
        if (neutralSet is null)
        {
            return result;
        }

        foreach (DictionaryEntry entry in neutralSet)
        {
            if (entry.Key is string key)
            {
                result[key] = Get(key);
            }
        }

        return result;
    }

    public static string GetLiteralResourceKey(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return "L.Text." + Convert.ToHexString(hash[..8]);
    }

    public static IReadOnlyDictionary<string, string> GetCurrentLiteralStrings()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var literal in EnglishLiterals.Keys)
        {
            result[GetLiteralResourceKey(literal)] = TranslateLiteral(literal);
        }

        return result;
    }

    public static IReadOnlySet<string> GetResourceKeys(string cultureCode)
    {
        var culture = string.Equals(cultureCode, EnglishCultureCode, StringComparison.OrdinalIgnoreCase)
            ? CultureInfo.GetCultureInfo(EnglishCultureCode)
            : CultureInfo.InvariantCulture;
        var set = ResourceManager.GetResourceSet(culture, true, false);
        if (set is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in set)
        {
            if (entry.Key is string key)
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    public static string AssemblyName => typeof(AppText).GetTypeInfo().Assembly.GetName().Name ?? "GlyphStash.Localization";

    private static void ApplyCulture(CultureInfo culture)
    {
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    private static readonly IReadOnlyDictionary<string, string> EnglishLiterals = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["字体资产工作台"] = "Font asset workbench",
        ["全部来源"] = "All sources",
        ["全部状态"] = "All states",
        ["全部标签"] = "All tags",
        ["全部集合"] = "All collections",
        ["全部区块"] = "All blocks",
        ["全部子集"] = "All subsets",
        ["全部分类"] = "All categories",
        ["GlyphStash 字体预览 Aa 123 你好"] = "GlyphStash font preview Aa 123 Hello",
        ["字体库"] = "Font Library",
        ["重新扫描"] = "Rescan",
        ["导入字体"] = "Import Fonts",
        ["搜索字体或标签"] = "Search fonts or tags",
        ["Inter、中文、等宽"] = "Inter, CJK, monospace",
        ["来源"] = "Source",
        ["激活状态"] = "Activation",
        ["标签"] = "Tags",
        ["集合"] = "Collections",
        ["管理标签"] = "Manage Tags",
        ["本地字体"] = "Local Fonts",
        ["列表区分系统字体、用户级安装、GlyphStash 管理和临时字体。"] = "The list separates system fonts, user-installed fonts, GlyphStash-managed fonts, and temporary fonts.",
        ["单行"] = "Single line",
        ["段落"] = "Paragraph",
        ["字符集"] = "Character set",
        ["正在加载字体索引..."] = "Loading font index...",
        ["重新扫描字体"] = "Rescan fonts",
        ["预览文本"] = "Preview text",
        ["字号"] = "Size",
        ["GlyphStash 管理目录"] = "GlyphStash managed directory",
        ["选择目录"] = "Choose directory",
        ["加入 GlyphStash 管理目录"] = "Add to GlyphStash managed directory",
        ["用户级安装"] = "Install for current user",
        ["当前会话临时启用"] = "Temporarily activate for this session",
        ["导入标签：无衬线, UI"] = "Import tags: sans-serif, UI",
        ["加入集合：官网改版"] = "Add to collection: website refresh",
        ["取消"] = "Cancel",
        ["开始导入"] = "Start import",
        ["优先勾选已有标签和集合；也可以在输入框中新增。删除标签或集合关系不会删除字体文件。"] = "Select existing tags and collections first; new names can also be added in the text fields. Deleting tags or collection links does not delete font files.",
        ["已有标签"] = "Existing tags",
        ["已有集合"] = "Existing collections",
        ["删除"] = "Delete",
        ["新增标签：衬线、中文、项目A"] = "New tags: serif, CJK, Project A",
        ["新增集合：官网改版、游戏 UI"] = "New collections: website refresh, game UI",
        ["删除标签只移除标签和字体的关联，不删除字体。"] = "Deleting a tag only removes tag relationships; font files are not deleted.",
        ["保存"] = "Save",
        ["删除标签"] = "Delete tag",
        ["此操作会从所有字体移除该标签，不删除字体文件。"] = "This removes the tag from all fonts. Font files are not deleted.",
        ["请选择字体"] = "Select a font",
        ["从字体库列表中选择一个字体族后，这里会显示预览、管理操作、基础信息和样式列表。"] = "Select a font family from the library to see preview, management actions, basic information, and styles here.",
        ["管理操作"] = "Management actions",
        ["卸载管理字体"] = "Uninstall managed font",
        ["查看字形"] = "View glyphs",
        ["临时启用属于当前用户会话，退出应用后会释放；外部应用可见性取决于 Windows 和目标应用刷新行为。"] = "Temporary activation belongs to the current user session and is released when the app exits. Visibility in other apps depends on Windows and how the target app refreshes fonts.",
        ["基础信息"] = "Basic information",
        ["样式"] = "Styles",
        ["格式"] = "Format",
        ["版本"] = "Version",
        ["制造商"] = "Manufacturer",
        ["覆盖"] = "Coverage",
        ["文件 Hash"] = "File hash",
        ["文件路径"] = "File path",
        ["样式列表"] = "Styles",
        ["标签/集合"] = "Tags / Collections",
        ["当前预览/字形样式"] = "Current preview/glyph style",
        ["卸载字体"] = "Uninstall font",
        ["将卸载 GlyphStash 管理的用户级字体。系统字体不会被删除，非 GlyphStash 管理字体会被阻止或提示失败。"] = "Uninstalls a GlyphStash-managed per-user font. System fonts are not deleted, and non-managed fonts are blocked or reported as failed.",
        ["确认卸载"] = "Uninstall",
        ["项目字体包、批量临时启用、关闭与导出清单。"] = "Project font packs, bulk temporary activation, deactivation, and manifest export.",
        ["新建集合名称"] = "New collection name",
        ["新建集合"] = "Create collection",
        ["搜索集合"] = "Search collections",
        ["官网、项目、文档"] = "Website, project, docs",
        ["临时启用全部"] = "Activate all temporarily",
        ["关闭全部"] = "Deactivate all",
        ["导出清单"] = "Export manifest",
        ["删除集合"] = "Delete collection",
        ["删除集合或移除字体只删除关系，不删除字体文件；批量关闭不会执行卸载。"] = "Deleting a collection or removing fonts only deletes relationships, not font files. Bulk deactivation does not uninstall fonts.",
        ["移除"] = "Remove",
        ["集合摘要"] = "Collection summary",
        ["用于在项目之间切换字体资产，不污染永久安装列表。"] = "Use collections to switch font assets between projects without polluting the permanent installation list.",
        ["字体数量"] = "Fonts",
        ["未知授权"] = "Unknown license",
        ["最近导出"] = "Last exported",
        ["当前会话"] = "Current session",
        ["按引用计数关闭"] = "Close by reference count",
        ["授权提示"] = "License note",
        ["集合导出只生成清单，不代表其中字体可再分发或可商用。"] = "Collection export only creates a manifest. It does not mean the included fonts are redistributable or commercially usable.",
        ["集合操作记录会写入诊断日志"] = "Collection actions are written to the diagnostic log",
        ["只会删除集合和字体关联，不会删除字体文件，也不会卸载字体。"] = "Only the collection and font relationships are deleted. Font files are not deleted or uninstalled.",
        ["确认删除"] = "Delete",
        ["返回字体详情"] = "Back to font details",
        ["搜索字符 / Unicode / 输入文本"] = "Search character / Unicode / text",
        ["你、U+4F60"] = "A, U+0041",
        ["Unicode 区块"] = "Unicode block",
        ["映射状态"] = "Mapping state",
        ["Unicode 映射字形"] = "Unicode-mapped glyphs",
        ["未映射字形（后续）"] = "Unmapped glyphs (later)",
        ["分页"] = "Pages",
        ["大型 CJK 字体按页渲染，避免一次性绘制全部字形。"] = "Large CJK fonts are rendered by page to avoid drawing every glyph at once.",
        ["上一页"] = "Previous",
        ["下一页"] = "Next",
        ["字形网格"] = "Glyph grid",
        ["固定单元 78px"] = "Fixed cell 78px",
        ["选中字形"] = "Selected glyph",
        ["所属样式"] = "Style",
        ["复制字符"] = "Copy character",
        ["复制 Unicode"] = "Copy Unicode",
        ["缺字搜索会显示当前字体不包含该字符/码位；未映射 glyph 入口已预留。"] = "Missing-glyph search shows when the current font does not contain the character/code point; unmapped glyph entry points are reserved.",
        ["在线字体"] = "Online Fonts",
        ["官方字体源：Google Fonts Developer API。"] = "Official font source: Google Fonts Developer API.",
        ["搜索 family 名称"] = "Search family name",
        ["子集"] = "Subset",
        ["分类"] = "Category",
        ["排序"] = "Sort",
        ["能力"] = "Capabilities",
        ["搜索在线字体"] = "Search online fonts",
        ["正在搜索..."] = "Searching...",
        ["请选择一个在线字体结果。"] = "Select an online font result.",
        ["下载不代表可商用，license 信息以来源页面为准，并会随下载记录保存。"] = "Downloading does not imply commercial usability. License information is based on the source page and is saved with the download record.",
        ["支持子集"] = "Supported subsets",
        ["更新日期"] = "Last modified",
        ["本地状态"] = "Local state",
        ["未下载"] = "Not downloaded",
        ["License 来源"] = "License source",
        ["下载后操作"] = "After download",
        ["收藏"] = "Favorite",
        ["标签：开源, UI"] = "Tags: open source, UI",
        ["集合：官网改版"] = "Collection: website refresh",
        ["下载所选样式"] = "Download selected styles",
        ["下载队列"] = "Download queue",
        ["重试"] = "Retry",
        ["只显示 v1 已接入的官方 API 来源；网络失败、限流和权限错误会保留诊断入口。"] = "Only official API sources connected in v1 are shown. Network failures, rate limits, and permission errors keep diagnostic entry points.",
        ["合并工具"] = "Merge Tool",
        ["按指定 Unicode 范围从补充字体合并字形，并生成可追踪报告。"] = "Merge glyphs from a supplemental font by Unicode range and generate a traceable report.",
        ["查看历史报告"] = "View history report",
        ["1 选择字体"] = "1 Select fonts",
        ["2 指定范围"] = "2 Specify ranges",
        ["3 预览冲突"] = "3 Preview conflicts",
        ["4 授权与导出"] = "4 License and export",
        ["5 报告"] = "5 Report",
        ["基础字体 A"] = "Base font A",
        ["补充字体 B"] = "Supplemental font B",
        ["保留优先"] = "Keep preferred",
        ["仅取指定范围"] = "Range only",
        ["搜索本地字体"] = "Search local fonts",
        ["Unicode 范围输入"] = "Unicode ranges",
        ["查看实际范围"] = "View actual range",
        ["快速选择"] = "Quick presets",
        ["按语言或用途快速填写范围；选择后仍可手动编辑。"] = "Fill ranges by language or use case; you can still edit the input after choosing.",
        ["合并模式"] = "Merge mode",
        ["范围摘要"] = "Range summary",
        ["点击下一步会执行 dry-run，检查补充字体覆盖、重复码位和阻止级问题。"] = "Next runs a dry-run to check supplemental coverage, duplicate code points, and blocking issues.",
        ["冲突摘要"] = "Conflict summary",
        ["预检查码位"] = "Prechecked code points",
        ["默认策略"] = "Default strategy",
        ["状态"] = "Status",
        ["明细数量"] = "Details",
        ["码位"] = "Code point",
        ["字符"] = "Character",
        ["基础字体状态"] = "Base state",
        ["补充字体状态"] = "Supplemental state",
        ["默认处理"] = "Default action",
        ["尚未生成冲突明细。"] = "No conflict details have been generated.",
        ["授权确认"] = "License confirmation",
        ["GlyphStash 不判断商业可用性；合并和导出前必须由用户确认输入字体授权风险。"] = "GlyphStash does not judge commercial usability. You must confirm the license risk before merging and exporting fonts.",
        ["我确认有权执行合并和导出，并理解未知授权不代表可商用。"] = "I confirm I have the right to merge and export these fonts and understand unknown license does not mean commercially usable.",
        ["输出设置"] = "Output settings",
        ["输出字体名称"] = "Output font name",
        ["输出文件路径"] = "Output file path",
        ["选择"] = "Choose",
        ["输出不能覆盖原始字体文件，也不能覆盖任何已有文件。"] = "Output cannot overwrite source font files or any existing file.",
        ["输入字体"] = "Input fonts",
        ["范围"] = "Range",
        ["输出文件"] = "Output file",
        ["license 确认时间"] = "License confirmed at",
        ["统计"] = "Stats",
        ["错误详情或诊断"] = "Error details or diagnostics",
        ["上一步"] = "Previous",
        ["fontTools worker 在后台运行，不阻塞主界面；失败时报告区保留错误与诊断入口。"] = "The fontTools worker runs in the background without blocking the UI. Failures keep errors and diagnostics in the report area.",
        ["查看实际包含范围"] = "View actual coverage ranges",
        ["按当前合并样式读取两个字体的 Unicode cmap 覆盖，选择后替换范围输入。"] = "Read Unicode cmap coverage for both fonts using the current merge style; selecting ranges replaces the range input.",
        ["关闭"] = "Close",
        ["实际连续段"] = "Actual continuous segments",
        ["正在读取实际覆盖..."] = "Reading actual coverage...",
        ["没有读取到可选择的覆盖段。请确认两个字体都有本地字体文件，并查看上方状态。"] = "No selectable coverage segments were found. Confirm both fonts have local font files and check the status above.",
        ["当前区块没有可选择的连续覆盖段。"] = "This block has no selectable continuous coverage segments.",
        ["替换范围输入"] = "Replace range input",
        ["设置"] = "Settings",
        ["管理目录、在线字体源、临时字体兼容性矩阵和诊断日志。"] = "Managed directory, online font source, temporary font compatibility matrix, and diagnostic log.",
        ["选择管理目录"] = "Choose managed directory",
        ["本地管理目录"] = "Local managed directory",
        ["应用更新"] = "App updates",
        ["检查更新"] = "Check for updates",
        ["当前版本"] = "Current version",
        ["更新源"] = "Update source",
        ["稍后"] = "Later",
        ["下载并重启更新"] = "Download and restart",
        ["尚未检查应用更新。"] = "App updates have not been checked.",
        ["正在检查应用更新..."] = "Checking for app updates...",
        ["当前运行的是开发或便携构建，无法应用自动更新。"] = "The current build is a development or portable build, so automatic updates cannot be applied.",
        ["当前已是最新版本。"] = "The current version is up to date.",
        ["发现新版本，可以下载并重启更新。"] = "A new version is available. Download and restart to update.",
        ["未知版本"] = "Unknown version",
        ["该版本没有发布说明。"] = "This version does not include release notes.",
        ["正在下载应用更新..."] = "Downloading app update...",
        ["更新已下载，正在重启应用。"] = "The update has been downloaded. Restarting the app.",
        ["已暂缓本次应用更新。"] = "This app update was deferred.",
        ["所有导入文件默认限制在 GlyphStash 管理目录；用户级安装会再复制到 Windows 当前用户字体目录。"] = "All imported files are constrained to the GlyphStash managed directory by default. Per-user install also copies them to the current Windows user font directory.",
        ["M2 支持格式"] = "M2 supported formats",
        ["WOFF/WOFF2 仅识别"] = "WOFF/WOFF2 recognition only",
        ["WOFF/WOFF2 不执行 Windows 本地安装或会话级临时启用。"] = "WOFF/WOFF2 are not installed locally on Windows or temporarily activated for the session.",
        ["在线字体源"] = "Online font source",
        ["v1 只接入 Google Fonts Developer API；license 以字体来源页面链接为准。"] = "v1 only connects to the Google Fonts Developer API. License information is based on font source page links.",
        ["保存 API key"] = "Save API key",
        ["临时字体外部应用兼容性矩阵"] = "Temporary font external app compatibility matrix",
        ["记事本：覆盖新启动与已运行应用，验证 WM_FONTCHANGE 后字体菜单刷新。"] = "Notepad: covers newly started and already running apps; verify the font menu refreshes after WM_FONTCHANGE.",
        ["VS Code：覆盖常见开发工具，新启动应可见，已运行窗口可能需要重新打开字体列表。"] = "VS Code: covers common development tools; newly started windows should see fonts, while running windows may need the font list reopened.",
        ["Edge / Chrome：覆盖浏览器与前端预览，验证 CSS font-family 选择。"] = "Edge / Chrome: covers browsers and frontend previews; verify CSS font-family selection.",
        ["Word / PowerPoint：覆盖 Office 文档场景，已运行应用可能需要重启文档窗口。"] = "Word / PowerPoint: covers Office document scenarios; running apps may need document windows restarted.",
        ["Figma / Adobe 设计工具：覆盖设计工作流，未安装时标记为未验证。"] = "Figma / Adobe design tools: covers design workflows; mark as unverified when not installed.",
        ["诊断日志"] = "Diagnostic log",
        ["当前路径：Windows 用户级字体管理；临时启用不会写成永久安装。"] = "Current path: Windows per-user font management. Temporary activation is not written as a permanent install.",
        ["该页面不属于 M1 真实交互范围"] = "This page is outside the real M1 interaction scope",
        ["当前仅保留导航和占位，避免误导为已实现业务功能。"] = "Navigation and placeholder content are kept only to avoid presenting the page as implemented business functionality.",
        ["组件演示"] = "Component Demo",
        ["普通按钮"] = "Normal button",
        ["主操作"] = "Primary action",
        ["危险操作"] = "Danger action",
        ["已安装"] = "Installed",
        ["系统字体"] = "System font",
        ["GlyphStash 管理"] = "GlyphStash managed",
        ["临时字体"] = "Temporary font",
        ["未知来源"] = "Unknown source",
        ["阻止操作"] = "Block action",
        ["已临时启用"] = "Temporarily enabled",
        ["未启用"] = "Not enabled",
        ["未知状态"] = "Unknown state",
        ["未知"] = "Unknown",
        ["无法解析"] = "Unable to parse",
        ["未设置标签"] = "No tags",
        ["待解析"] = "Pending parse",
        ["覆盖范围待解析"] = "Coverage pending",
        ["待计算"] = "Pending",
        ["系统枚举字体，未解析文件路径"] = "System-enumerated font; file path not parsed",
        ["已收藏"] = "Favorited",
        ["关闭临时启用"] = "Disable temporary activation",
        ["临时启用"] = "Temporarily activate",
        ["已安装，无需临时启用"] = "Installed; temporary activation is not needed",
        ["授权完整"] = "Licenses complete",
        ["未导出"] = "Not exported",
        ["合并"] = "Merge",
        ["跳过"] = "Skip",
        ["记录缺失"] = "Record missing",
        ["阻止"] = "Blocked",
        ["存在"] = "Present",
        ["缺失"] = "Missing",
        ["提示"] = "Warning",
        ["信息"] = "Info",
        ["仅 A"] = "A only",
        ["仅 B"] = "B only",
        ["未绑定字体"] = "No fonts bound",
        ["等待下载"] = "Waiting",
        ["未选择样式"] = "No styles selected",
        ["排队中"] = "Queued",
        ["下载中"] = "Downloading",
        ["已完成"] = "Completed",
        ["失败"] = "Failed",
        ["正在下载"] = "Downloading",
        ["下载完成"] = "Download complete",
        ["下载失败"] = "Download failed",
        ["等待重试"] = "Waiting to retry",
        ["未声明子集"] = "No subsets declared",
        ["未知更新日期"] = "Unknown update date",
        ["字体索引为空"] = "Font index is empty",
        ["字体索引为空。请重新扫描字体。"] = "Font index is empty. Rescan fonts.",
        ["字形浏览"] = "Glyph Browser",
        ["查看 Unicode 映射字符，搜索字符或码位，并复制字形信息。"] = "View Unicode-mapped characters, search by character or code point, and copy glyph details.",
        ["用户级安装后已安装，无需临时启用"] = "Already installed after per-user install; temporary activation is not needed",
        ["补全（追加）模式只合并基础字体缺失、补充字体存在的码位，基础字体已存在的码位会跳过。"] = "Supplement (append) mode only merges code points missing from the base font and present in the supplemental font. Existing base code points are skipped.",
        ["覆盖模式会用补充字体替换指定范围内基础字体已存在的码位，同时补齐基础字体缺失的码位。"] = "Overwrite mode replaces existing base-font code points in the selected range with the supplemental font and also fills missing base code points.",
        ["尚未执行冲突预览。"] = "Conflict preview has not run.",
        ["开始导出"] = "Start export",
        ["完成"] = "Done",
        ["下一步"] = "Next",
        ["正在读取 SQLite 字体索引..."] = "Reading SQLite font index...",
        ["准备加载字体索引"] = "Preparing to load font index",
        ["最近操作：等待字体索引"] = "Recent action: waiting for font index",
        ["请选择字体文件并选择 GlyphStash 管理目录。"] = "Select font files and choose the GlyphStash managed directory.",
        ["首次启动：正在扫描 Windows 字体目录..."] = "First launch: scanning Windows font directories...",
        ["字体索引加载失败，可尝试重新扫描"] = "Font index failed to load. Try rescanning.",
        ["正在扫描 C:\\Windows\\Fonts 与用户字体目录..."] = "Scanning C:\\Windows\\Fonts and user font directories...",
        ["最近操作：字体索引已刷新"] = "Recent action: font index refreshed",
        ["字体索引已刷新"] = "Font index refreshed",
        ["扫描失败，详情已写入状态栏"] = "Scan failed. Details were written to the status bar.",
        ["请先选择基础字体 A 和补充字体 B。"] = "Select base font A and supplemental font B first.",
        ["请选择两个字体"] = "Select two fonts",
        ["请选择基础字体 A 文件和补充字体 B 文件。"] = "Select base font A file and supplemental font B file.",
        ["请选择两个字体文件"] = "Select two font files",
        ["请选择基础字体 A 文件。"] = "Select base font A file.",
        ["请选择补充字体 B 文件。"] = "Select supplemental font B file.",
        ["选择基础字体 A"] = "Select base font A",
        ["选择补充字体 B"] = "Select supplemental font B",
        ["正在读取字体元数据..."] = "Reading font metadata...",
        ["已读取字体元数据。"] = "Font metadata loaded.",
        ["合并输入只支持 .ttf 或 .otf 字体文件。"] = "Merge input only supports .ttf or .otf font files.",
        ["字体文件读取失败"] = "Font file read failed",
        ["合并报告已保留"] = "Merge report kept",
        ["字形覆盖服务未装配。"] = "Glyph coverage service is not wired.",
        ["请选择基础字体和补充字体。"] = "Select a base font and supplemental font.",
        ["正在读取两个字体的 Unicode cmap 覆盖..."] = "Reading Unicode cmap coverage from both fonts...",
        ["两个字体都没有可选择的 Unicode 映射覆盖。"] = "Neither font has selectable Unicode mapping coverage.",
        ["请至少选择一个实际覆盖段。"] = "Select at least one actual coverage segment.",
        ["合并服务不可用。"] = "Merge service is unavailable.",
        ["预览"] = "Preview",
        ["正在预检查输入。"] = "Prechecking input.",
        ["冲突预览存在阻止级问题，请检查提示。"] = "Conflict preview found blocking issues. Check the notices.",
        ["冲突预览完成，可继续授权与导出。"] = "Conflict preview complete. Continue to license confirmation and export.",
        ["合并预览失败"] = "Merge preview failed",
        ["准备"] = "Preparing",
        ["正在准备合并任务。"] = "Preparing merge task.",
        ["等待导出"] = "Waiting for export",
        ["待生成"] = "Pending",
        ["合并导出完成"] = "Merge export complete",
        ["合并导出失败"] = "Merge export failed",
        ["正在取消合并任务..."] = "Canceling merge task...",
        ["历史合并报告已加载。"] = "Historical merge report loaded.",
        ["合并报告加载失败"] = "Merge report load failed",
        ["已加入收藏"] = "Added to favorites",
        ["已取消收藏"] = "Removed from favorites",
        ["请选择字体文件。"] = "Select font files.",
        ["导入前必须先选择 GlyphStash 管理目录。"] = "Choose a GlyphStash managed directory before importing.",
        ["没有可导入的字体文件。"] = "No importable font files.",
        ["管理目录已选择，可以开始导入。"] = "Managed directory selected. Import can start.",
        ["管理目录已更新"] = "Managed directory updated",
        ["Google Fonts API key 已清空。"] = "Google Fonts API key cleared.",
        ["Google Fonts API key 已保存，可以搜索在线字体。"] = "Google Fonts API key saved. Online font search is available.",
        ["Google Fonts API key 已保存"] = "Google Fonts API key saved",
        ["请在设置页配置 Google Fonts API key 后搜索在线字体。"] = "Configure a Google Fonts API key in Settings before searching online fonts.",
        ["在线字体服务未装配。"] = "Online font service is not wired.",
        ["正在搜索 Google Fonts..."] = "Searching Google Fonts...",
        ["没有找到匹配的在线字体。"] = "No matching online fonts found.",
        ["请先选择一个在线字体。"] = "Select an online font first.",
        ["请选择至少一个可下载样式。"] = "Select at least one downloadable style.",
        ["下载失败，可重试"] = "Download failed. You can retry.",
        ["请先选择一个字体"] = "Select a font first",
        ["字形浏览服务未装配。"] = "Glyph browser service is not wired.",
        ["请从字体详情进入字形浏览。"] = "Open glyph browser from font details.",
        ["当前字体没有可读取的样式。"] = "The current font has no readable styles.",
        ["正在读取字体字形..."] = "Reading font glyphs...",
        ["请先选择字体文件"] = "Select font files first",
        ["导入前必须先选择管理目录"] = "Choose a managed directory before importing",
        ["导入失败，详情已写入导入窗口"] = "Import failed. Details were written to the import window.",
        ["标签和集合已更新"] = "Tags and collections updated",
        ["标签已删除，字体文件不会被删除"] = "Tag deleted. Font files are not deleted.",
        ["集合已创建"] = "Collection created",
        ["请先选择一个集合"] = "Select a collection first",
        ["集合已删除，字体文件不会被删除"] = "Collection deleted. Font files are not deleted.",
        ["已从集合移除字体"] = "Font removed from collection",
        ["当前状态不需要临时启用"] = "Current state does not need temporary activation",
        ["集合持有的临时启用引用已释放"] = "Temporary activation references held by the collection were released",
        ["集合清单已导出为 CSV，包含字体、样式、状态和 license"] = "Collection manifest exported as CSV with fonts, styles, states, and license",
        ["未读取基础字体 A。"] = "Base font A has not been read.",
        ["未读取补充字体 B。"] = "Supplemental font B has not been read.",
        ["合并结果：成功"] = "Merge result: success",
        ["合并结果：失败"] = "Merge result: failed",
        ["成功"] = "Success",
        ["未生成输出字体"] = "No output font generated",
        ["未记录"] = "Not recorded",
        ["未确认"] = "Not confirmed",
        ["覆盖模式：指定范围内补充字体存在的码位覆盖基础字体"] = "Overwrite mode: supplemental code points in the selected range overwrite the base font",
        ["补全模式：基础字体已有码位默认跳过"] = "Supplement mode: existing base-font code points are skipped by default",
        ["报告"] = "Report",
        ["不可导入"] = "Cannot import",
        ["不支持的字体格式"] = "Unsupported font format",
        ["当前来源没有持有该字体的临时启用引用。"] = "The current source does not hold a temporary activation reference for this font.",
        ["当前字体不包含该字符/码位。"] = "The current font does not contain that character/code point.",
        ["当前字体库不支持该格式导入，仅支持 TTF、OTF、TTC、OTC。"] = "The current font library cannot import this format. Only TTF, OTF, TTC, and OTC are supported.",
        ["当前字体没有 Unicode 映射覆盖。"] = "The current font has no Unicode mapping coverage.",
        ["当前字体没有可读取的本地文件路径。"] = "The current font has no readable local file path.",
        ["当前字体缺少 Unicode cmap 表。"] = "The current font is missing a Unicode cmap table.",
        ["该字体没有可用于临时启用的本地文件路径。"] = "This font has no local file path available for temporary activation.",
        ["合并报告文件不存在。"] = "Merge report file does not exist.",
        ["合并报告文件格式无效。"] = "Merge report file format is invalid.",
        ["合并报告已生成。"] = "Merge report generated.",
        ["合并任务存在阻止级问题。"] = "Merge task contains blocking issues.",
        ["合并任务已取消。"] = "Merge task canceled.",
        ["合并预览已取消。"] = "Merge preview canceled.",
        ["基础字体和补充字体不能是同一个文件。"] = "Base font and supplemental font cannot be the same file.",
        ["可导入"] = "Importable",
        ["请输入 Unicode 范围。"] = "Enter Unicode ranges.",
        ["请输入输出字体名称。"] = "Enter an output font name.",
        ["请选择输出文件路径。"] = "Choose an output file path.",
        ["输出不能覆盖原始字体文件。"] = "Output cannot overwrite source font files.",
        ["输出路径已存在，M4 v1 不覆盖任何已有文件。"] = "Output path already exists. M4 v1 does not overwrite existing files.",
        ["输出目录不存在。"] = "Output directory does not exist.",
        ["输出文件扩展名必须是 .ttf 或 .otf。"] = "Output file extension must be .ttf or .otf.",
        ["未完成授权确认，不能开始导出。"] = "License confirmation is incomplete, so export cannot start.",
        ["未选择用户级安装。"] = "Per-user install was not selected.",
        ["未找到可卸载字体文件。"] = "No uninstallable font file was found.",
        ["未找到内置 fontTools worker，也未找到开发脚本 fallback。"] = "Bundled fontTools worker was not found, and no development script fallback was found.",
        ["未找到已安装字体路径。"] = "Installed font path was not found.",
        ["未知样式"] = "Unknown style",
        ["文件不存在"] = "File does not exist",
        ["无法打开当前用户字体注册表。"] = "Cannot open the current user's font registry.",
        ["系统字体已安装，无需执行用户级安装。"] = "System font is already installed; per-user install is not needed.",
        ["系统字体在 v1 中不支持卸载。"] = "System fonts cannot be uninstalled in v1.",
        ["响应体为空。"] = "Response body is empty.",
        ["需要先选择 GlyphStash 管理目录。"] = "Choose a GlyphStash managed directory first.",
        ["需要先在设置页配置 Google Fonts API key。"] = "Configure a Google Fonts API key in Settings first.",
        ["已安装，无需临时启用。"] = "Already installed; temporary activation is not needed.",
        ["已释放 GlyphStash 持有的临时启用引用。"] = "Released temporary activation references held by GlyphStash.",
        ["应用启动时将上次会话的临时启用记录标记为已过期。"] = "Marked temporary activation records from the previous session as expired on app startup.",
        ["正在启动 fontTools worker..."] = "Starting fontTools worker...",
        ["正在写入合并报告..."] = "Writing merge report...",
        ["重复字体，可导入但会复用相同文件 hash"] = "Duplicate font. It can be imported and will reuse the same file hash.",
        ["字体 cmap 表不完整。"] = "Font cmap table is incomplete.",
        ["字体临时启用已关闭，并已广播 WM_FONTCHANGE。"] = "Temporary font activation was disabled and WM_FONTCHANGE was broadcast.",
        ["字体缺少有效 name 表。"] = "Font is missing a valid name table.",
        ["字体文件不存在。"] = "Font file does not exist.",
        ["字体文件不存在或不是本地物理路径。"] = "Font file does not exist or is not a local physical path.",
        ["字体文件过小或已损坏。"] = "Font file is too small or corrupted.",
        ["字体文件已复制并写入注册表，但 Windows 未立即加载该字体。"] = "Font file was copied and written to the registry, but Windows did not load it immediately.",
        ["字体选择"] = "Font selection",
        ["字体已安装到当前用户。"] = "Font installed for the current user.",
        ["字体已从当前用户安装位置卸载并删除 GlyphStash 副本。"] = "Font uninstalled from the current user's install location and the GlyphStash copy was deleted.",
        ["字体已从当前用户字体注册表卸载。"] = "Font uninstalled from the current user's font registry.",
        ["字体已临时启用，并已广播 WM_FONTCHANGE。"] = "Font temporarily activated and WM_FONTCHANGE was broadcast.",
        ["字体已由当前 GlyphStash 会话持有，已增加引用。"] = "Font is already held by the current GlyphStash session; reference count increased.",
        ["fontTools worker 未返回合并结果。"] = "fontTools worker did not return a merge result.",
        ["fontTools worker 未返回预览结果。"] = "fontTools worker did not return a preview result.",
        ["fontTools worker 未生成输出字体文件。"] = "fontTools worker did not generate the output font file.",
        ["fontTools worker 未写入响应文件。"] = "fontTools worker did not write a response file.",
        ["fontTools worker 响应格式无效。"] = "fontTools worker response format is invalid.",
        ["Google Fonts 请求"] = "Google Fonts request",
        ["Google Fonts 下载"] = "Google Fonts download",
        ["Google Fonts API key 无效或没有权限。"] = "Google Fonts API key is invalid or lacks permission.",
        ["M2 仅识别 WOFF/WOFF2；Windows 本地安装和临时启用暂不支持该格式。"] = "M2 only recognizes WOFF/WOFF2; Windows local install and temporary activation do not support that format yet.",
        ["M2 仅支持 TTF、OTF、TTC、OTC 的本地安装和临时启用。"] = "M2 only supports local install and temporary activation for TTF, OTF, TTC, and OTC.",
        ["OpenType 表目录不完整。"] = "OpenType table directory is incomplete.",
        ["OpenType 字体表头不完整。"] = "OpenType font header is incomplete.",
        ["TTC/OTC 文件没有包含字体。"] = "TTC/OTC file does not contain any fonts.",
        ["TTC/OTC 文件头不完整。"] = "TTC/OTC file header is incomplete.",
        ["TTC/OTC 字体偏移无效。"] = "TTC/OTC font offset is invalid.",
        ["Unicode 范围"] = "Unicode ranges",
        ["Windows 未能加载该字体资源。"] = "Windows failed to load this font resource.",
        ["Windows 未确认释放该字体资源。"] = "Windows did not confirm release of this font resource.",
        ["当前筛选没有匹配字体。扫描失败、下载失败、权限不足和授权风险都复用页面内状态区或确认对话框。"] = "No fonts match the current filters. Scan failures, download failures, insufficient permissions, and license risks reuse inline state areas or confirmation dialogs."
    };
}
