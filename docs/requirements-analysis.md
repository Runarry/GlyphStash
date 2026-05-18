# GlyphStash 需求分析文档

版本：0.1  
日期：2026-05-18  
状态：第一阶段需求与技术栈确认稿

## 1. 项目概述

GlyphStash 是一个桌面字体管理工具，首版面向设计师和前端开发者，目标是在 Windows 上提供可靠、直观的字体查看、预览、管理、临时启用、字形浏览、字形合并和在线字体获取能力。项目需要在 Windows 优先的前提下保留跨平台扩展空间，未来可支持 macOS 和 Linux。

首版不把自己定位为专业字体制作软件，而是定位为“字体资产管理与使用效率工具”。专业字体编辑能力仅限于用户明确指定范围的字形子集提取与合并，并需要在界面中清晰提示授权风险。

## 2. 已确认决策

| 领域 | 决策 |
| --- | --- |
| 目标用户 | 设计师、前端开发者 |
| v1 范围 | 纳入全部核心能力：字体查看、预览、安装/卸载、临时启用、标签集合、字形浏览、字形范围合并、在线搜索下载 |
| 桌面技术栈 | Avalonia 12 + .NET 10 LTS |
| 平台策略 | Windows v1 优先，平台相关能力通过接口隔离，为 macOS/Linux 预留 |
| 字体管理权限 | 默认用户级管理，不把系统级字体管理作为 v1 默认路径 |
| 临时启用模型 | 支持单字体和集合的当前用户会话级临时激活/关闭；激活期间应尽力对其他桌面应用可见，不写入永久安装记录，用户关闭、GlyphStash 正常退出或系统会话结束后恢复 |
| 字形合并实现 | .NET 主程序 + 内置 fontTools worker |
| 在线字体来源 | 官方 API 优先，首选 Google Fonts Developer API；其他站点后续以 Provider/插件形式扩展 |
| 本地存储 | SQLite |
| 授权策略 | 展示 license，导出/合并前要求用户确认风险；应用不替用户判断商业可用性 |

## 3. 用户与核心场景

### 3.1 目标用户

1. 设计师
   - 需要快速浏览大量本地字体。
   - 需要按项目、风格、语言、授权状态整理字体。
   - 需要临时启用项目字体，避免污染系统字体列表。

2. 前端开发者
   - 需要查找可商用或开源字体。
   - 需要预览多语言文本、字重、样式和变量字体效果。
   - 需要下载字体、查看 license、生成项目可用字体资产。

### 3.2 典型用户故事

1. 作为设计师，我希望查看系统已安装字体，并用自定义文本快速预览，以便挑选适合当前项目的字体。
2. 作为设计师，我希望把字体加入项目集合，并一键在当前用户会话中临时启用或关闭整个集合，以便在不同项目之间切换，并让设计工具、编辑器等外部应用在本次工作会话中使用这些字体。
3. 作为前端开发者，我希望搜索 Google Fonts 中的字体，查看样式、授权和下载来源，以便快速引入项目。
4. 作为前端开发者，我希望查看某个字体包含的全部字形和 Unicode 覆盖范围，以便确认它是否支持目标语言。
5. 作为高级用户，我希望从两个字体中按指定 Unicode 范围合并字形，并导出一个新字体文件，以便补齐特定项目所需字形。

## 4. 功能需求

### 4.1 字体扫描与索引

GlyphStash 需要扫描当前用户可见的系统字体，并建立可查询索引。

必需能力：
- 枚举 Windows 当前可用字体族、字重、字宽、样式和对应字体文件。
- 区分系统字体、用户级安装字体、GlyphStash 管理字体和临时启用字体。
- 提取字体基础元数据：family、subfamily、full name、PostScript name、版本、制造商、license 字段、支持格式。
- 识别常见字体格式：TTF、OTF、TTC/OTC、WOFF/WOFF2 的支持策略需在技术验证后确定。
- 支持重新扫描、增量刷新和字体缓存异常提示。

验收标准：
- 首次启动能列出当前用户可用字体。
- 字体列表中同一字体族下能看到不同字重/样式。
- 重新扫描后能发现新增或移除的用户级字体。

### 4.2 字体列表、搜索与预览

字体列表是首屏核心体验，应支持高效浏览和筛选。

必需能力：
- 字体列表展示字体族、样式数量、来源、标签、收藏状态、激活状态。
- 支持关键字搜索、标签筛选、集合筛选、来源筛选、收藏筛选。
- 支持自定义预览文本，默认包含英文、数字、常用符号和中文样例。
- 支持字号调整、预览模式切换，例如单行、段落、字符集。
- 支持缺字提示，至少在字形查看页能明确展示覆盖情况。

验收标准：
- 输入搜索词后，列表能按字体名称和元数据过滤。
- 修改预览文本后，列表和详情预览同步刷新。
- 对 CJK 大字体列表滚动时不应明显卡顿，具体性能阈值在原型阶段测量。

### 4.3 用户级安装与卸载

v1 默认只做用户级字体管理，避免默认请求管理员权限。

必需能力：
- 从本地文件导入字体。
- 将字体安装到当前用户上下文。
- 卸载由 GlyphStash 用户级安装或管理的字体。
- 对系统字体、未知来源字体、非 GlyphStash 管理字体提供保护性提示。
- 安装前检测重复字体、同名不同版本、格式不支持、文件损坏。

非目标：
- v1 不默认提供系统级字体安装/卸载。
- v1 不直接删除 Windows 系统字体。

验收标准：
- 用户导入一个有效字体文件后，字体能出现在 GlyphStash 列表中。
- 用户卸载 GlyphStash 管理的用户级字体后，列表状态正确刷新。
- 尝试卸载系统字体时，界面必须阻止或强提示。

### 4.4 会话级临时激活与关闭

临时激活用于在不持久安装字体的情况下，把字体加入当前用户桌面会话的字体资源池，使 GlyphStash 和外部桌面应用在本次工作会话中尽力可用。该能力不是仅服务 GlyphStash 内部预览，也不能被视为永久安装。

必需能力：
- 支持单个字体临时启用/关闭。
- 支持集合级临时启用/关闭。
- 临时激活范围为当前用户桌面会话；激活期间应尽力对其他桌面应用可见。
- 记录当前会话激活状态和 GlyphStash 拥有的激活引用，但不把临时启用写成永久安装。
- 支持重复激活引用计数，避免单字体激活和集合激活相互覆盖导致提前卸载。
- 应用退出时释放由 GlyphStash 临时加载的字体资源。
- 若操作系统或外部应用对临时字体可见性、刷新时机或枚举能力有限制，界面需明确说明。
- 对已运行的外部应用，GlyphStash 应提示其可能需要重新打开字体菜单、重启文档窗口或重启应用后才会看到变化。

技术策略：
- Windows v1 通过平台服务封装临时字体加载/卸载能力。
- Windows v1 的会话级临时激活优先验证 `AddFontResourceExW` / `RemoveFontResourceExW` 路径；若目标是让外部应用可见，不应使用 `FR_PRIVATE`。
- 每次添加或移除字体资源后，需要向顶层窗口广播 `WM_FONTCHANGE`，让外部应用有机会刷新字体列表。
- `RemoveFontResourceExW` 必须使用与添加时相同的 flags，并结合 GlyphStash 的引用计数决定实际卸载时机。
- DirectWrite custom font set 可作为 GlyphStash 内部预览或回退方案，但不能作为外部应用可见性的主路径。
- 需要验证不同 Windows 版本、不同外部应用和已运行/新启动应用对会话级临时字体的可见性差异。

验收标准：
- 临时启用后，GlyphStash 内部预览可使用该字体。
- 临时启用后，新启动的常见桌面应用应能看到该字体，至少覆盖记事本、Office 或设计软件代表样本、浏览器/编辑器代表样本。
- 已运行的外部应用收到字体变更广播后，能刷新则刷新；不能刷新时，UI 必须提示可能需要重启目标应用。
- 关闭临时启用后，GlyphStash 内部状态和预览恢复，新启动的外部应用不再看到该字体。
- GlyphStash 正常退出后释放所有由 GlyphStash 激活的字体资源。
- 应用重启后，临时启用状态不应被当作永久安装。

### 4.5 标签、集合与收藏

GlyphStash 使用标签和集合作为主要管理模型。

必需能力：
- 字体可添加多个标签。
- 字体可加入多个集合。
- 支持收藏字体。
- 集合支持批量会话级临时启用、批量关闭、批量导出清单。
- 标签和集合支持重命名、删除、搜索。

推荐语义：
- 标签用于描述属性，例如“衬线”“等宽”“中文”“可商用”“品牌项目”。
- 集合用于项目或任务，例如“官网改版”“游戏 UI”“PPT 字体包”。

验收标准：
- 同一字体能同时属于多个集合。
- 删除集合不会删除字体文件。
- 删除标签只移除关联关系，不影响字体。

### 4.6 字形浏览

字形浏览用于确认字体覆盖范围和具体 glyph 形状。

必需能力：
- 展示字体包含的 Unicode 映射字符。
- 支持按 Unicode 区块、搜索字符、输入文本筛选。
- 展示单个字形的基本信息：字符、Unicode、glyph name、glyph id、所属字体样式。
- 对无 Unicode 映射但存在的 glyph，需保留后续展示空间；v1 可先以“高级/未映射字形”形式处理。
- 支持复制字符、复制 Unicode 码位。

验收标准：
- 打开一个字体详情页后，可以查看该字体支持的字符集合。
- 搜索 `U+4E00` 或输入“你”时，能定位对应字符。
- 字体不包含某字符时，界面能明确反馈。

### 4.7 字形范围合并

字形合并是高级功能，首版以向导形式降低误操作风险。

必需能力：
- 选择基础字体 A 和补充字体 B。
- 指定从 B 合并到 A 的范围，至少支持 Unicode 范围输入，例如 `U+4E00-U+9FFF`。
- 合并前预览冲突：重复码位、字体单位不匹配、缺失字形、license 未确认、字体格式不支持。
- 输出新字体文件，不覆盖原始字体。
- 生成合并报告，记录输入文件、范围、冲突处理结果、输出文件和 license 确认时间。

默认冲突策略：
- v1 不自动覆盖基础字体已有码位。
- 如果 B 中的指定码位已存在于 A，默认跳过并在报告中记录。
- 后续可增加“覆盖”“重命名 glyph”“保留 OpenType layout 特性”等高级策略。

技术策略：
- 主程序负责 UI、任务编排、输入校验和报告展示。
- 内置 Python/fontTools worker 负责子集提取、合并和字体保存。
- worker 应作为受控进程运行，输入输出通过文件路径和结构化任务描述传递。

验收标准：
- 用户能从两个字体中指定一个 Unicode 范围并导出新字体。
- 原始字体文件不被修改。
- license 未确认时不能开始导出。
- 合并失败时展示可理解错误和保留诊断日志。

### 4.8 在线字体搜索与下载

首版在线来源只接 API 清晰、授权信息明确的来源。

必需能力：
- 集成 Google Fonts Developer API 作为首个字体源。
- 支持按 family 名称搜索。
- 展示字体族、样式、分类、支持子集、更新时间、license 信息、来源链接。
- 支持下载字体文件到 GlyphStash 管理目录。
- 下载后可加入标签、集合、收藏，并可选择安装或临时启用。

Provider 设计：
- 在线来源通过 `IFontSourceProvider` 抽象。
- Provider 需要声明搜索能力、下载能力、license 元数据质量、是否需要 API key、速率限制策略。
- Font Squirrel、DaFont 等站点不进入 v1 默认实现，后续根据授权和技术边界以 Provider/插件扩展。

验收标准：
- 用户能搜索 Google Fonts 字体并查看基础信息。
- 用户能下载一个字体族的选定样式。
- 下载记录中保存来源、license 和时间。

### 4.9 授权与风险提示

GlyphStash 需要尊重字体授权，但不承担法律判断。

必需能力：
- 字体详情页展示 license 字段或来源提供的 license 信息。
- 下载字体时保存 license 来源。
- 合并和导出前必须显示授权确认提示。
- 对缺失 license 的字体标记为“未知授权”。

界面原则：
- 不把“可下载”暗示为“可商用”。
- 不替用户判断某字体是否可用于商业项目。
- 对 Windows 自带字体和第三方商业字体应避免提供误导性再分发建议。

验收标准：
- 未知授权字体执行合并导出时，必须出现确认提示。
- 下载来源的 license 信息能在字体详情和下载记录中看到。

## 5. 非功能需求

### 5.1 性能

- 字体列表必须支持大量字体场景，目标是 1,000 个字体族以上仍可搜索和滚动。
- CJK 大字体的字形表需要分页、虚拟化或延迟加载。
- 字体元数据解析应缓存，避免每次启动全量重复解析。
- fontTools 合并任务必须在后台运行，不能阻塞 UI。

### 5.2 稳定性

- 字体文件损坏、格式不支持、权限不足、网络失败、API 限流、合并失败都必须有可理解错误。
- 所有修改字体状态的操作应可追踪，至少记录操作日志。
- 永久安装/卸载和会话级临时启用/关闭需要明确区分。
- 临时启用属于当前用户会话级状态，不应写入系统字体注册表或被展示为永久安装字体。

### 5.3 安全

- 不执行来自字体来源站点的任意脚本。
- 下载文件需校验扩展名和实际字体格式。
- fontTools worker 不接受未经校验的任意命令行参数。
- 所有文件写入限制在 GlyphStash 管理目录或用户明确选择的导出路径。

### 5.4 可维护性

- 平台相关代码必须隔离在 platform service 中。
- UI 不直接调用 Windows API 或 fontTools worker。
- 本地数据库 schema 需要迁移机制。
- Provider 需要可替换，避免 Google Fonts 逻辑散落在 UI 中。
- UI 样式系统和通用组件必须在 M1 建立，后续页面优先复用，不在各业务页面重复定义基础控件样式。
- 通用组件需要覆盖常见加载、空状态、错误、确认、危险操作和后台任务状态，避免每个功能模块各自实现一套反馈模式。

## 6. 技术栈分析

### 6.1 推荐技术栈

| 层 | 技术 |
| --- | --- |
| 桌面 UI | Avalonia 12 |
| Runtime | .NET 10 LTS |
| 架构 | MVVM + 服务层 + 平台适配层 |
| 本地数据库 | SQLite |
| 字体解析 | .NET 字体元数据解析库 + 必要时 fontTools 辅助 |
| 字形合并 | Python/fontTools worker |
| Windows 字体管理 | Win32 GDI/DirectWrite 封装，具体路径需原型验证 |
| 在线源 | Provider 模型，v1 实现 Google Fonts Provider |

### 6.2 选择理由

Avalonia 12 与 .NET 10 LTS 适合新桌面项目：UI 可跨平台，Windows 桌面体验成熟，并且 .NET 对本地服务、SQLite、进程编排和平台 API 封装都有稳定支持。字体管理涉及系统 API、文件系统和本地缓存，使用 .NET 比纯 Web 桌面方案更适合。

fontTools 是字体子集和合并领域成熟工具，尤其适合 OpenType/TrueType 处理。与其在 v1 自研纯 .NET 字体合并逻辑，不如通过受控 worker 复用成熟能力，把风险集中在进程封装、输入校验和错误呈现上。

### 6.3 技术风险

| 风险 | 影响 | 应对 |
| --- | --- | --- |
| Windows 会话级临时字体对不同应用刷新行为不一致 | 用户以为启用后所有应用立即可见 | 建立应用兼容性矩阵，广播 `WM_FONTCHANGE`，在 UI 明确标注刷新限制 |
| 用户级字体安装细节受 Windows 版本影响 | 安装/卸载失败或刷新延迟 | 封装平台服务并做 Windows 10/11 测试矩阵 |
| 字体合并复杂度高 | 变量字体、OpenType layout、重复 glyph 处理失败 | v1 限制默认冲突策略，输出报告，不覆盖原文件 |
| license 信息不完整 | 用户误用字体 | 显示未知授权状态，导出前确认 |
| 大型字体性能 | UI 卡顿 | 字形表虚拟化、后台解析、SQLite 缓存 |
| 跨平台字体系统差异 | 后续迁移成本 | v1 即抽象平台接口，但只实现 Windows |

## 7. 建议架构

### 7.1 分层

1. Presentation
   - Avalonia Views、ViewModels、Converters、Dialogs。
   - 只处理展示状态和用户交互，不直接操作系统字体 API。

2. Application Services
   - 字体扫描、安装、临时启用、字形查询、合并任务、下载任务、集合管理。
   - 负责业务规则、权限保护、任务编排和错误模型。

3. Domain
   - 字体、字体文件、字形、集合、标签、来源、license、任务报告等核心模型。

4. Infrastructure
   - SQLite repository。
   - Windows platform adapter。
   - fontTools worker adapter。
   - Google Fonts provider。
   - 文件系统和下载器。

### 7.2 服务接口

以下接口是需求级定义，第一阶段不要求立即写代码。

```csharp
public interface IFontInventoryService
{
    Task<IReadOnlyList<FontFamilyRecord>> ScanInstalledFontsAsync(CancellationToken cancellationToken);
    Task<FontMetadata> ReadMetadataAsync(FontFileRef fontFile, CancellationToken cancellationToken);
}

public interface IFontInstallService
{
    Task<FontInstallResult> InstallForCurrentUserAsync(FontFileRef fontFile, CancellationToken cancellationToken);
    Task<FontUninstallResult> UninstallManagedFontAsync(ManagedFontId fontId, CancellationToken cancellationToken);
}

public interface ITemporaryFontActivationService
{
    Task<ActivationResult> ActivateForCurrentUserSessionAsync(IReadOnlyList<FontFileRef> fonts, CancellationToken cancellationToken);
    Task<ActivationResult> DeactivateForCurrentUserSessionAsync(IReadOnlyList<FontFileRef> fonts, CancellationToken cancellationToken);
    Task DeactivateAllOwnedActivationsAsync(CancellationToken cancellationToken);
}

public interface IFontMetadataStore
{
    Task SaveFontIndexAsync(IReadOnlyList<FontRecord> fonts, CancellationToken cancellationToken);
    Task<IReadOnlyList<FontRecord>> SearchAsync(FontSearchQuery query, CancellationToken cancellationToken);
}

public interface IGlyphCatalogService
{
    Task<GlyphPage> GetGlyphsAsync(FontFaceId faceId, GlyphQuery query, CancellationToken cancellationToken);
}

public interface IFontMergeService
{
    Task<FontMergePreview> PreviewAsync(FontMergeRequest request, CancellationToken cancellationToken);
    Task<FontMergeResult> MergeAsync(FontMergeRequest request, CancellationToken cancellationToken);
}

public interface IFontSourceProvider
{
    string ProviderId { get; }
    Task<IReadOnlyList<RemoteFontFamily>> SearchAsync(RemoteFontSearchQuery query, CancellationToken cancellationToken);
    Task<RemoteFontDownloadResult> DownloadAsync(RemoteFontDownloadRequest request, CancellationToken cancellationToken);
}
```

### 7.3 项目与目录规划

GlyphStash 建议采用“少量核心项目 + 明确引用方向”的结构。v1 功能较多，但不宜过早拆成大量微项目；应优先保证 Domain、Application、Presentation、Infrastructure、Platform 的边界清晰，后续再按实际复杂度拆分 Provider 或插件。

推荐解决方案结构：

```text
GlyphStash.sln
src/
  GlyphStash.Desktop/
  GlyphStash.Presentation/
  GlyphStash.Application/
  GlyphStash.Domain/
  GlyphStash.Infrastructure/
  GlyphStash.Platform.Windows/
tools/
  fonttools-worker/
tests/
  GlyphStash.Domain.Tests/
  GlyphStash.Application.Tests/
  GlyphStash.Infrastructure.Tests/
  GlyphStash.Platform.Windows.Tests/
  GlyphStash.Presentation.Tests/
docs/
build/
packaging/
```

项目职责：

| 项目/目录 | 职责 |
| --- | --- |
| `GlyphStash.Desktop` | Avalonia 可执行入口、`Program`、`App.axaml`、主窗口、依赖注入、配置加载、应用生命周期、全局异常处理。作为 composition root 连接 UI、服务和平台实现。 |
| `GlyphStash.Presentation` | Avalonia Views、ViewModels、Converters、Commands、样式系统、通用组件、设计时数据。不得直接调用 Windows API、SQLite、网络下载器或 fontTools worker。 |
| `GlyphStash.Application` | 用例编排、服务接口、查询/命令模型、DTO、错误模型、任务进度模型。负责业务流程，但不依赖具体 UI、SQLite、Win32 或 HTTP 实现。 |
| `GlyphStash.Domain` | 字体、字体文件、字体族、字形、集合、标签、授权、激活记录、合并请求等核心实体和值对象，以及不依赖外部系统的领域规则。 |
| `GlyphStash.Infrastructure` | SQLite repository、文件系统访问、字体元数据解析适配、Google Fonts provider、下载器、fontTools worker 进程适配、缓存和日志落地。 |
| `GlyphStash.Platform.Windows` | Windows 字体扫描、用户级安装/卸载、会话级临时激活、`WM_FONTCHANGE` 广播、DirectWrite/GDI 封装和 Windows 版本兼容性处理。 |
| `tools/fonttools-worker` | 受控 Python/fontTools worker，提供 subset、merge、字体检查等命令。输入输出必须使用结构化协议，不直接暴露任意命令行执行能力。 |
| `tests/*` | 按项目边界组织测试。Domain/Application 以单元测试为主，Infrastructure/Platform 以集成和兼容性测试为主，Presentation 使用 Avalonia Headless 或 ViewModel 测试。 |
| `build` | 构建脚本、版本号、CI 辅助脚本、发布前检查脚本。 |
| `packaging` | Windows 安装包、升级策略、图标、清单、发布模板和后续平台打包配置。 |

推荐 `GlyphStash.Presentation` 内部目录：

```text
GlyphStash.Presentation/
  Components/
    Common/
    FontList/
    FontPreview/
    Dialogs/
    TaskStatus/
  Converters/
  DesignData/
  Styles/
    Tokens.axaml
    Typography.axaml
    Controls.axaml
    Themes/
      Light.axaml
      Dark.axaml
  ViewModels/
    Shell/
    FontLibrary/
    Collections/
    OnlineFonts/
    Glyphs/
    Merge/
    Settings/
  Views/
    Shell/
    FontLibrary/
    Collections/
    OnlineFonts/
    Glyphs/
    Merge/
    Settings/
```

推荐 `GlyphStash.Application` 内部目录：

```text
GlyphStash.Application/
  Abstractions/
    Fonts/
    Storage/
    Platform/
    Providers/
    Workers/
  Fonts/
  Collections/
  Activation/
  Glyphs/
  OnlineFonts/
  Merging/
  Licensing/
  Settings/
  Tasks/
  Diagnostics/
```

推荐 `GlyphStash.Infrastructure` 内部目录：

```text
GlyphStash.Infrastructure/
  Storage/
    Sqlite/
    Migrations/
  FontParsing/
  Providers/
    GoogleFonts/
  Downloads/
  FontTools/
  Files/
  Logging/
  Settings/
```

推荐 `GlyphStash.Platform.Windows` 内部目录：

```text
GlyphStash.Platform.Windows/
  Fonts/
    Inventory/
    Installation/
    Activation/
    FontChangeBroadcast/
  DirectWrite/
  Gdi/
  Shell/
  Diagnostics/
```

引用规则：

- `GlyphStash.Domain` 不引用任何 GlyphStash 其他项目。
- `GlyphStash.Application` 只引用 `GlyphStash.Domain`。
- `GlyphStash.Presentation` 引用 `GlyphStash.Application` 和 `GlyphStash.Domain`，只通过 Application 接口触发业务能力。
- `GlyphStash.Infrastructure` 引用 `GlyphStash.Application` 和 `GlyphStash.Domain`，实现 repository、provider、worker、文件系统等接口。
- `GlyphStash.Platform.Windows` 引用 `GlyphStash.Application` 和 `GlyphStash.Domain`，实现平台相关接口。
- `GlyphStash.Desktop` 引用 Presentation、Application、Infrastructure、Platform.Windows，并负责依赖注入装配。
- 测试项目只引用被测项目及必要的测试辅助库，不通过 Desktop 间接测试业务规则。

阶段落地建议：

- M1 先创建完整解决方案骨架和项目引用关系，但只实现 Desktop、Presentation、Application、Domain、Infrastructure 的最小闭环；Platform.Windows 先覆盖字体扫描和基础预览需要的能力。
- M1 的样式系统、通用组件和字体库原型应落在 `GlyphStash.Presentation`，不要散落在 Desktop 项目。
- M2 开始补齐 `GlyphStash.Platform.Windows/Fonts/Installation` 和 `Activation`，同时在 `Application/Activation` 中实现引用计数和清理编排。
- M3 将 Google Fonts 放在 `Infrastructure/Providers/GoogleFonts`，保留 `Application/OnlineFonts` 的 provider 抽象，避免 UI 依赖具体在线来源。
- M4 将 fontTools 进程协议和适配放在 `Infrastructure/FontTools`，Python worker 放在 `tools/fonttools-worker`，合并业务流程放在 `Application/Merging`。
- M5 重点补齐 packaging、诊断、日志导出、迁移、跨平台适配清单和发布前检查脚本。

## 8. 数据模型

### 8.1 核心实体

| 实体 | 说明 |
| --- | --- |
| FontFile | 字体文件路径、hash、格式、来源、管理状态 |
| FontFamily | 字体族名称、分类、样式数量、语言覆盖摘要 |
| FontFace | 字重、字宽、样式、PostScript name、关联文件 |
| GlyphRecord | Unicode、glyph id、glyph name、所属 face |
| Tag | 用户标签 |
| Collection | 用户集合/项目字体包 |
| FontCollectionItem | 字体与集合关系 |
| FontTag | 字体与标签关系 |
| LicenseRecord | license 名称、URL、原始文本摘要、来源 |
| DownloadRecord | provider、remote id、下载 URL、下载时间、本地文件 |
| ActivationRecord | 当前用户会话级临时激活状态、GlyphStash 拥有的激活引用、平台 flags、引用计数、清理状态 |
| MergeJob | 合并请求、输出路径、结果、报告 |

### 8.2 SQLite 存储原则

- 字体文件用 hash 辅助去重。
- 用户标签、集合和收藏状态必须持久化。
- 临时激活状态可以记录用于 UI 展示、引用计数和异常清理，但重启后不能自动视作已激活。
- ActivationRecord 需要包含字体文件 id/path/hash、activation scope、owner、平台 flags、reference count、activatedAt、lastKnownState、cleanup status。
- 下载记录必须保留 provider 和 license 信息。
- schema 必须从第一版开始支持迁移版本号。

## 9. UI 信息架构

### 9.1 主导航

1. 字体库
   - 字体列表、搜索、筛选、预览。

2. 集合
   - 项目字体集合、批量启用/关闭、集合管理。

3. 在线字体
   - Google Fonts 搜索、详情、下载、加入集合。

4. 合并工具
   - 选择基础字体、选择补充字体、设置范围、预览冲突、导出。

5. 设置
   - 管理目录、缓存、Google Fonts API key、授权提示偏好、临时字体兼容性诊断、诊断日志。

### 9.2 字体详情页

字体详情页应包含：
- 基础信息：family、style、version、format、source。
- 预览区：自定义文本、字号、样式。
- 字形表：Unicode 区块筛选、搜索、复制。
- 管理操作：收藏、标签、加入集合、安装/卸载、会话级临时启用/关闭。
- 授权信息：license、来源链接、未知授权提示。

### 9.3 合并向导

合并向导步骤：
1. 选择基础字体。
2. 选择补充字体。
3. 输入 Unicode 范围或选择区块。
4. 预览冲突和 license 状态。
5. 选择输出路径和字体命名策略。
6. 执行合并并展示报告。

### 9.4 样式系统与通用组件

v1 需要在 M1 建立基础样式系统，后续功能页面在此基础上迭代。

基础样式系统应包含：
- 颜色 token、字体层级、间距、圆角、边框、阴影、焦点态、禁用态、危险态。
- 亮色/暗色主题资源组织方式，至少保证后续可扩展，不把颜色硬编码到页面。
- 字体预览相关排版规则，包括预览文本、字体族名、样式数、来源、激活状态和缺字提示的展示层级。

通用组件应优先覆盖：
- 应用外壳、主导航、页面标题区、命令栏。
- 搜索框、筛选栏、分段控件、图标按钮、下拉菜单、确认对话框。
- 字体列表项、字体预览块、标签/集合 chip、状态 badge、任务进度条。
- 空状态、加载状态、错误状态、权限提示、授权风险提示、临时字体兼容性提示。

## 10. 测试与验收计划

### 10.1 功能验收场景

1. 扫描已安装字体
   - 启动应用后能列出当前用户可用字体。
   - 重新扫描后能识别新增和移除。

2. 字体预览
   - 修改预览文本、字号、样式后能实时更新。
   - 中英文混合文本能正常显示，缺字能被识别。

3. 标签与集合
   - 创建标签和集合。
   - 同一字体加入多个集合。
   - 集合批量在当前用户会话中临时启用和关闭。

4. 用户级安装/卸载
   - 导入本地字体并安装到当前用户。
   - 卸载 GlyphStash 管理字体。
   - 阻止或强提示系统字体卸载。

5. 字形查看
   - 查看字体完整 Unicode 字符覆盖。
   - 按 Unicode 范围和字符搜索。
   - 复制字符和 Unicode 码位。

6. 字形合并
   - 选择两个字体，输入范围，预览冲突。
   - 确认 license 后导出新字体。
   - 原字体不被修改，失败时有报告。

7. 在线搜索下载
   - 搜索 Google Fonts。
   - 查看字体元数据和 license。
   - 下载字体并加入集合。

8. 样式系统与通用组件
   - 主导航、命令栏、搜索筛选、字体列表项、状态 badge、对话框、空/加载/错误状态在不同页面复用同一套组件和样式 token。
   - 亮色和暗色主题下，核心页面不出现不可读文本、状态色冲突或控件布局错位。

### 10.2 技术可行性验证

第一轮原型必须验证：
- Windows 用户级字体安装路径、注册表记录和 FontCache 刷新行为。
- 不使用 `FR_PRIVATE` 的 `AddFontResourceExW` / `RemoveFontResourceExW` 对 GlyphStash 预览、新启动外部应用、已运行外部应用的影响。
- `WM_FONTCHANGE` 广播后，不同类型外部应用的刷新行为和是否需要重启应用。
- Avalonia 12 对本地字体文件、临时字体和大字体预览的支持方式。
- SQLite 在 1,000+ 字体族、10 万+ 字形记录下的查询和分页表现。
- fontTools subset + merge 在 TTF、OTF、CJK 字体、重复码位场景下的结果。
- Google Fonts Developer API 的元数据字段、下载 URL、license 信息和 API key 策略。

## 11. 里程碑建议

### M1：项目骨架与字体库原型

- 创建 Avalonia 12(12.0.2) + .NET 10(当前系统的.Net版本) 解决方案。
- 建立 MVVM、服务层、SQLite 基础设施。
- 建立基础样式系统，包括颜色 token、字体层级、间距、圆角、控件状态和亮色/暗色主题资源组织。
- 实现首批通用组件，包括应用外壳、主导航、页面标题区、命令栏、搜索筛选栏、图标按钮、状态 badge、确认对话框、空/加载/错误状态。
- 建立字体列表项和字体预览块的基础组件，作为字体库、集合和在线字体结果页的复用基础。
- 建立组件演示或最小预览页，用于快速检查通用组件在亮色/暗色主题和典型窗口尺寸下的表现。
- 实现 Windows 字体扫描和基础预览。

### M2：本地管理能力

- 实现用户级安装/卸载。
- 实现标签、集合、收藏。
- 实现单字体和集合批量会话级临时启用/关闭的 Windows 原型。
- 完成临时激活引用计数、退出清理、异常恢复和操作日志。
- 建立临时字体外部应用兼容性矩阵的第一版，至少覆盖新启动应用和已运行应用两类场景。
- 补齐本地管理相关确认对话框、危险操作提示、失败重试和权限不足提示。

### M3：字形与在线字体

- 实现字形浏览和 Unicode 区块筛选。
- 实现 Google Fonts Provider。
- 实现下载、license 保存和导入流程。
- 实现在线字体下载队列、失败重试、API 限流提示和离线/网络错误状态。
- 将在线字体结果页复用 M1 字体列表项、预览块、标签/集合入口和授权提示组件。
- 完成字形表分页、虚拟化或延迟加载策略的性能验证。

### M4：字形合并

- 封装 fontTools worker。
- 实现合并向导、冲突预览、导出报告。
- 完成失败处理和授权确认流程。
- 支持合并任务进度、取消、后台执行和详细错误报告。
- 明确变量字体、TTC/OTC、重复码位、OpenType layout 冲突的 v1 处理策略和 UI 提示。
- 确保合并向导复用通用步骤页、确认对话框、授权风险提示和任务报告组件。

### M5：稳定化

- 完成性能优化、错误模型、日志、设置页。
- 补齐 Windows 10/11 测试。
- 整理后续 macOS/Linux 适配清单。
- 完成亮色/暗色主题、键盘导航、焦点态、基础无障碍和高 DPI 检查。
- 完成会话级临时字体兼容性矩阵，并在设置页提供诊断入口。
- 完成数据库迁移、缓存清理、日志导出、崩溃恢复和备份/恢复策略检查。
- 完成安装包、升级路径、首次启动引导、发布说明和已知限制说明。

## 12. 明确不做或后置

- v1 不默认管理系统级字体。
- v1 不提供专业字体绘制、轮廓编辑、kerning 编辑或 OpenType feature 编辑。
- v1 不承诺任意第三方字体网站都可搜索下载。
- v1 不保证所有外部应用实时刷新临时字体；v1 目标是当前用户桌面会话内的系统级临时激活，并通过兼容性矩阵说明支持程度。
- v1 不自动判断字体是否可商用。

## 13. 参考资料

- Avalonia 12 发布说明：https://avaloniaui.net/blog/avalonia-12/
- Avalonia 12 breaking changes：https://docs.avaloniaui.net/docs/avalonia12-breaking-changes
- .NET releases and support：https://learn.microsoft.com/en-us/dotnet/core/releases-and-support
- Windows AddFontResourceExW：https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-addfontresourceexw
- Windows Font Installation and Deletion：https://learn.microsoft.com/en-us/windows/win32/gdi/font-installation-and-deletion
- DirectWrite Custom Font Sets：https://learn.microsoft.com/en-us/windows/win32/directwrite/custom-font-sets-win10
- fontTools merge：https://fonttools.readthedocs.io/en/stable/merge.html
- fontTools subset：https://fonttools.readthedocs.io/en/stable/subset/index.html
- Google Fonts Developer API：https://developers.google.com/fonts/docs/developer_api
