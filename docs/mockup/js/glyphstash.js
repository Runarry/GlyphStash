const glyphstashData = {
  fonts: [
    {
      id: "inter",
      name: "Inter",
      previewClass: "preview-sans",
      styles: "18 styles",
      source: "GlyphStash 管理",
      state: "已临时启用",
      stateClass: "ok",
      license: "SIL Open Font License 1.1",
      tags: ["无衬线", "UI", "可商用"],
      collections: ["官网改版", "Design System"],
      version: "4.001",
      manufacturer: "Rasmus Andersson",
      path: "C:\\Users\\you\\GlyphStash\\fonts\\Inter\\Inter.ttf",
      hash: "sha256: 82f4...91aa",
      coverage: "Latin, Greek, Cyrillic",
      removable: true
    },
    {
      id: "noto",
      name: "Noto Serif CJK SC",
      previewClass: "preview-serif",
      styles: "7 styles",
      source: "系统字体",
      state: "已安装",
      stateClass: "blue",
      license: "OFL 摘要来自系统元数据",
      tags: ["中文", "衬线"],
      collections: ["品牌手册"],
      version: "2.004",
      manufacturer: "Google",
      path: "C:\\Windows\\Fonts\\NotoSerifCJKsc-Regular.otf",
      hash: "sha256: 31ac...be09",
      coverage: "CJK Unified, Kana, Latin",
      removable: false
    },
    {
      id: "cascadia",
      name: "Cascadia Code",
      previewClass: "preview-mono",
      styles: "6 styles",
      source: "用户级安装",
      state: "已安装",
      stateClass: "ok",
      license: "SIL Open Font License 1.1",
      tags: ["等宽", "代码"],
      collections: ["开发文档"],
      version: "2404.23",
      manufacturer: "Microsoft",
      path: "C:\\Users\\you\\AppData\\Local\\Microsoft\\Windows\\Fonts\\CascadiaCode.ttf",
      hash: "sha256: f318...a440",
      coverage: "Latin, Powerline, Symbols",
      removable: true
    },
    {
      id: "sourcehan",
      name: "Source Han Sans SC",
      previewClass: "preview-ui",
      styles: "14 styles",
      source: "临时字体",
      state: "未启用",
      stateClass: "warn",
      license: "未知授权",
      tags: ["中文", "项目A"],
      collections: ["游戏 UI"],
      version: "2.005",
      manufacturer: "Adobe",
      path: "D:\\ProjectFonts\\SourceHanSansSC-Regular.otf",
      hash: "sha256: 89b5...7cc1",
      coverage: "CJK Unified, Latin, Kana",
      removable: false
    }
  ],
  glyphs: [
    ["你", "U+4F60", "uni4F60", "14882"],
    ["好", "U+597D", "uni597D", "18419"],
    ["字", "U+5B57", "uni5B57", "19003"],
    ["形", "U+5F62", "uni5F62", "20488"],
    ["A", "U+0041", "A", "36"],
    ["g", "U+0067", "g", "74"],
    ["1", "U+0031", "one", "18"],
    ["&", "U+0026", "ampersand", "5"],
    ["中", "U+4E2D", "uni4E2D", "14112"],
    ["文", "U+6587", "uni6587", "22598"],
    ["界", "U+754C", "uni754C", "26710"],
    ["面", "U+9762", "uni9762", "33940"]
  ]
};

function qs(selector, root = document) {
  return root.querySelector(selector);
}

function qsa(selector, root = document) {
  return Array.from(root.querySelectorAll(selector));
}

function showToast(message) {
  let toast = qs(".toast");
  if (!toast) {
    toast = document.createElement("div");
    toast.className = "toast";
    document.body.appendChild(toast);
  }
  toast.textContent = message;
  toast.classList.add("show");
  window.clearTimeout(showToast.timer);
  showToast.timer = window.setTimeout(() => toast.classList.remove("show"), 2600);
}

function openModal(id) {
  const modal = document.getElementById(id);
  if (modal) modal.classList.add("show");
}

function closeModal(id) {
  const modal = document.getElementById(id);
  if (modal) modal.classList.remove("show");
}

function renderFontList() {
  const list = qs("[data-font-list]");
  if (!list) return;
  const search = (qs("[data-font-search]")?.value || "").toLowerCase();
  const source = qs("[data-source-filter]")?.value || "全部来源";
  const state = qs("[data-state-filter]")?.value || "全部状态";
  const filtered = glyphstashData.fonts.filter(font => {
    const hitSearch = font.name.toLowerCase().includes(search) || font.tags.join(" ").toLowerCase().includes(search);
    const hitSource = source === "全部来源" || font.source === source;
    const hitState = state === "全部状态" || font.state === state;
    return hitSearch && hitSource && hitState;
  });
  list.innerHTML = filtered.map((font, index) => `
    <article class="row selectable ${index === 0 ? "active" : ""}" data-font-id="${font.id}">
      <div class="row-head">
        <div>
          <h3>${font.name}</h3>
          <div class="chips" style="margin-top:7px">
            <span class="pill ${font.stateClass}">${font.state}</span>
            <span class="pill">${font.source}</span>
            <span class="pill">${font.styles}</span>
          </div>
        </div>
        <button class="btn ghost" data-favorite="${font.id}" aria-label="收藏字体">${font.id === "inter" ? "已收藏" : "收藏"}</button>
      </div>
      <p class="font-preview ${font.previewClass}" data-preview-line>${previewText()} </p>
      <div class="chips">${font.tags.map(tag => `<span class="chip">${tag}</span>`).join("")}</div>
    </article>
  `).join("") || `<div class="empty">当前筛选没有匹配字体。<br><button class="btn" data-clear-filters style="margin-top:12px">清空筛选</button></div>`;
  qsa("[data-font-id]", list).forEach(row => row.addEventListener("click", () => selectFont(row.dataset.fontId)));
  qsa("[data-favorite]", list).forEach(button => button.addEventListener("click", event => {
    event.stopPropagation();
    button.textContent = button.textContent === "收藏" ? "已收藏" : "收藏";
    showToast(`${button.textContent === "已收藏" ? "已加入收藏" : "已取消收藏"}`);
  }));
  qs("[data-clear-filters]")?.addEventListener("click", () => {
    qsa("[data-font-search], [data-source-filter], [data-state-filter]").forEach(input => {
      if (input.tagName === "SELECT") input.selectedIndex = 0;
      else input.value = "";
    });
    renderFontList();
  });
  selectFont(filtered[0]?.id || "inter");
}

function previewText() {
  return qs("[data-preview-text]")?.value || "GlyphStash 字体预览 Aa 123 你好";
}

function selectFont(id) {
  const font = glyphstashData.fonts.find(item => item.id === id);
  if (!font) return;
  qsa("[data-font-id]").forEach(row => row.classList.toggle("active", row.dataset.fontId === id));
  qsa("[data-detail-name]").forEach(el => el.textContent = font.name);
  qsa("[data-detail-source]").forEach(el => el.textContent = font.source);
  qsa("[data-detail-state]").forEach(el => {
    el.textContent = font.state;
    el.className = `pill ${font.stateClass}`;
  });
  qsa("[data-detail-license]").forEach(el => {
    el.textContent = font.license;
    el.className = font.license.includes("未知") ? "pill warn" : "pill ok";
  });
  qsa("[data-detail-preview]").forEach(el => {
    el.className = `font-preview ${font.previewClass}`;
    el.style.fontSize = `${qs("[data-font-size]")?.value || 30}px`;
    el.textContent = previewText();
  });
  const meta = qs("[data-detail-meta]");
  if (meta) {
    meta.innerHTML = `
      <div class="meta"><span>样式</span>${font.styles}</div>
      <div class="meta"><span>版本</span>${font.version}</div>
      <div class="meta"><span>制造商</span>${font.manufacturer}</div>
      <div class="meta"><span>覆盖</span>${font.coverage}</div>
      <div class="meta"><span>文件 Hash</span>${font.hash}</div>
      <div class="meta"><span>文件路径</span>${font.path}</div>
    `;
  }
  const uninstall = qs("[data-uninstall-font]");
  if (uninstall) {
    uninstall.disabled = !font.removable;
    uninstall.textContent = font.removable ? "卸载管理字体" : "系统字体不可卸载";
  }
}

function initLibrary() {
  if (!qs("[data-font-list]")) return;
  renderFontList();
  qsa("[data-font-search], [data-source-filter], [data-state-filter]").forEach(input => input.addEventListener("input", renderFontList));
  qs("[data-preview-text]")?.addEventListener("input", () => {
    qsa("[data-preview-line], [data-detail-preview]").forEach(el => el.textContent = previewText());
  });
  qs("[data-font-size]")?.addEventListener("input", event => {
    qsa("[data-preview-line], [data-detail-preview]").forEach(el => el.style.fontSize = `${event.target.value}px`);
    qs("[data-font-size-label]").textContent = `${event.target.value}px`;
  });
  qsa("[data-preview-mode]").forEach(button => button.addEventListener("click", () => {
    qsa("[data-preview-mode]").forEach(item => item.classList.remove("active"));
    button.classList.add("active");
    showToast(`预览模式已切换为：${button.textContent}`);
  }));
  qs("[data-start-scan]")?.addEventListener("click", () => {
    qs("[data-scan-status]").textContent = "正在扫描 C:\\Windows\\Fonts 与用户字体目录...";
    window.setTimeout(() => {
      qs("[data-scan-status]").textContent = "扫描完成：新增 2 个字体族，1 个缓存条目已刷新";
      showToast("字体索引已刷新");
    }, 900);
  });
  qs("[data-open-import]")?.addEventListener("click", () => openModal("import-modal"));
  qsa("[data-open-tags]").forEach(button => button.addEventListener("click", () => openModal("tags-modal")));
  qs("[data-uninstall-font]")?.addEventListener("click", () => openModal("uninstall-modal"));
  qs("[data-confirm-uninstall]")?.addEventListener("click", () => {
    closeModal("uninstall-modal");
    showToast("卸载任务已加入队列，完成后会刷新字体索引");
  });
  qs("[data-start-import]")?.addEventListener("click", () => {
    closeModal("import-modal");
    showToast("2 个字体已导入，1 个损坏文件已跳过");
  });
}

function initCollections() {
  const collectionRows = qsa("[data-collection]");
  if (!collectionRows.length) return;
  collectionRows.forEach(row => row.addEventListener("click", () => {
    collectionRows.forEach(item => item.classList.remove("active"));
    row.classList.add("active");
    qs("[data-collection-title]").textContent = row.dataset.collection;
    qs("[data-collection-note]").textContent = row.dataset.note;
  }));
  qsa("[data-bulk-action]").forEach(button => button.addEventListener("click", () => showToast(button.dataset.bulkAction)));
}

function initOnlineFonts() {
  if (!qs("[data-remote-results]")) return;
  qsa("[data-remote-font]").forEach(row => row.addEventListener("click", () => {
    qsa("[data-remote-font]").forEach(item => item.classList.remove("active"));
    row.classList.add("active");
    qs("[data-remote-title]").textContent = row.dataset.family;
    qs("[data-remote-category]").textContent = row.dataset.category;
    qs("[data-remote-license]").textContent = row.dataset.license;
  }));
  qsa("[data-remote-search]").forEach(button => button.addEventListener("click", () => showToast("已向 Google Fonts Provider 发起搜索")));
  qs("[data-download-font]")?.addEventListener("click", () => {
    qs("[data-download-state]").textContent = "正在下载 Regular / 600 / 700 到 GlyphStash 管理目录...";
    window.setTimeout(() => {
      qs("[data-download-state]").textContent = "下载完成：已保存来源、license 与本地文件记录，可继续安装或临时启用。";
      showToast("远程字体已加入本地管理");
    }, 900);
  });
}

function initGlyphBrowser() {
  const grid = qs("[data-glyph-grid]");
  if (!grid) return;
  function renderGlyphs(query = "") {
    const normalized = query.toLowerCase();
    const items = glyphstashData.glyphs.filter(item => item.join(" ").toLowerCase().includes(normalized));
    grid.innerHTML = items.map((glyph, index) => `
      <button class="glyph ${index === 0 ? "active" : ""}" data-glyph="${glyph.join("|")}">
        <b>${glyph[0]}</b><span>${glyph[1]}</span>
      </button>
    `).join("") || `<div class="empty" style="grid-column:1/-1">当前字体不包含该字符或码位。</div>`;
    qsa("[data-glyph]", grid).forEach(button => button.addEventListener("click", () => selectGlyph(button)));
    qs("[data-glyph]", grid)?.click();
  }
  function selectGlyph(button) {
    qsa("[data-glyph]", grid).forEach(item => item.classList.remove("active"));
    button.classList.add("active");
    const [char, code, name, id] = button.dataset.glyph.split("|");
    qs("[data-glyph-char]").textContent = char;
    qs("[data-glyph-code]").textContent = code;
    qs("[data-glyph-name]").textContent = name;
    qs("[data-glyph-id]").textContent = id;
  }
  renderGlyphs();
  qs("[data-glyph-search]")?.addEventListener("input", event => renderGlyphs(event.target.value));
  qsa("[data-copy]").forEach(button => button.addEventListener("click", () => showToast(`${button.dataset.copy} 已复制`)));
}

function initWizard() {
  const pages = qsa("[data-wizard-page]");
  if (!pages.length) return;
  let current = 0;
  function updateWizard() {
    pages.forEach((page, index) => page.classList.toggle("active", index === current));
    qsa("[data-step]").forEach((step, index) => step.classList.toggle("active", index === current));
    qs("[data-prev-step]").disabled = current === 0;
    qs("[data-next-step]").textContent = current === pages.length - 1 ? "完成" : current === 3 ? "开始导出" : "下一步";
  }
  qs("[data-next-step]")?.addEventListener("click", () => {
    if (current === 3 && !qs("[data-license-confirm]").checked) {
      showToast("需要先确认授权风险，才能开始导出");
      return;
    }
    if (current < pages.length - 1) {
      current += 1;
      if (current === 4) {
        qs("[data-merge-report]").textContent = "导出成功：跳过重复码位 128 个，合并 1,936 个字形，报告已写入输出目录。";
      }
      updateWizard();
    } else {
      showToast("合并报告已保留，可从诊断日志再次打开");
    }
  });
  qs("[data-prev-step]")?.addEventListener("click", () => {
    current = Math.max(0, current - 1);
    updateWizard();
  });
  qs("[data-range-input]")?.addEventListener("input", event => {
    qs("[data-range-summary]").textContent = `${event.target.value || "未输入范围"} · 将预检查补充字体覆盖、重复码位和授权状态`;
  });
  updateWizard();
}

function initSettings() {
  if (!qs("[data-settings]")) return;
  qsa("[data-setting-action]").forEach(button => button.addEventListener("click", () => showToast(button.dataset.settingAction)));
  qs("[data-api-key]")?.addEventListener("input", event => {
    qs("[data-api-status]").textContent = event.target.value.length > 10 ? "已填写，待验证" : "未配置";
  });
}

document.addEventListener("DOMContentLoaded", () => {
  initLibrary();
  initCollections();
  initOnlineFonts();
  initGlyphBrowser();
  initWizard();
  initSettings();
  qsa("[data-close-modal]").forEach(button => button.addEventListener("click", () => closeModal(button.dataset.closeModal)));
});
