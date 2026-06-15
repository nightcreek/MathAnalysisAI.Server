(function () {
  function toDisplayStatus(parseStatus) {
    const normalized = String(parseStatus || "").toLowerCase();
    if (normalized === "success") return "解析成功";
    if (normalized === "processing") return "解析中";
    if (normalized === "pending") return "等待解析";
    if (normalized === "failed") return "解析失败";
    if (normalized === "ocr_pending") return "需要 OCR";
    return parseStatus || "未知";
  }

  function toDisplayKind(kind) {
    const normalized = String(kind || "").toLowerCase();
    if (normalized === "textbook") return "教材";
    if (normalized === "lecture_note") return "讲义";
    if (normalized === "exercise_book") return "习题册";
    if (normalized === "handout") return "资料";
    if (normalized === "user_note") return "个人笔记";
    if (normalized === "other") return "其他";
    return kind || "未知";
  }

  function formatDateTime(value) {
    if (!value) return "-";
    const dt = new Date(value);
    if (Number.isNaN(dt.getTime())) return "-";
    return dt.toLocaleString("zh-CN", { hour12: false });
  }

  function renderList(items) {
    const container = UI.qs("#materialsListContainer");
    if (!container) return;

    const arr = Array.isArray(items) ? items : [];
    if (!arr.length) {
      container.className = "status";
      container.textContent = "暂无课程资料，上传 PDF 后会显示在这里。";
      return;
    }

    let html = "";
    arr.forEach((item) => {
      const statusText = toDisplayStatus(item.parseStatus);
      const kindText = toDisplayKind(item.materialKind);
      const parseMessage = item.parseMessage ? item.parseMessage : "无";
      const chunkCount = item.chunkCount == null ? 0 : item.chunkCount;
      const showOcrHint = String(item.parseStatus || "").toLowerCase() === "ocr_pending";

      html += "<div class='result-section'>";
      html += "<div><strong>标题：</strong>" + UI.escapeHtml(item.title || "-") + "</div>";
      html += "<div><strong>类型：</strong>" + UI.escapeHtml(kindText) + "</div>";
      html += "<div><strong>文件名：</strong>" + UI.escapeHtml(item.originalFileName || "-") + "</div>";
      html += "<div><strong>解析状态：</strong><span class='status-chip'>" + UI.escapeHtml(statusText) + "</span></div>";
      html += "<div><strong>分块数量：</strong>" + UI.escapeHtml(chunkCount) + "</div>";
      html += "<div><strong>上传时间：</strong>" + UI.escapeHtml(formatDateTime(item.uploadedAt)) + "</div>";
      html += "<div><strong>解析说明：</strong>" + UI.escapeHtml(parseMessage) + "</div>";
      if (item.downloadUrl) {
        html += "<div class='actions'><a class='jump-link' href='" + UI.escapeHtml(item.downloadUrl) + "' target='_blank' rel='noopener'>下载/查看原文件 →</a></div>";
      }
      if (showOcrHint) {
        html += "<div class='hint warning-hint'>该 PDF 可能是扫描版，后续需要 OCR 处理。</div>";
      }
      html += "</div>";
    });

    container.className = "";
    container.innerHTML = html;
  }

  const DEFAULT_NETWORK_RESOURCES = [
    {
      category: "教材与参考书",
      title: "华东师范大学 · 数学分析（第四版）",
      description: "国内经典数学分析教材，适合本科基础阶段系统学习。",
      link: "https://book.douban.com/subject/26802081/"
    },
    {
      category: "教材与参考书",
      title: "陶哲轩 · Analysis I / Analysis II",
      description: "从自然数出发的现代分析入门，强调直觉与严格性的结合。",
      link: "https://terrytao.wordpress.com/books/analysis-i/"
    },
    {
      category: "在线课程",
      title: "MIT 18.100 Real Analysis (OCW)",
      description: "MIT OpenCourseWare 上的实分析公开课，含完整视频与习题。",
      link: "https://ocw.mit.edu/courses/18-100a-real-analysis-fall-2020/"
    },
    {
      category: "在线课程",
      title: "可汗学院 · Calculus",
      description: "直观视频讲解微积分核心概念，作为先修或复习非常合适。",
      link: "https://www.khanacademy.org/math/calculus-1"
    },
    {
      category: "交互式工具",
      title: "Desmos Graphing Calculator",
      description: "在线图形计算器，可输入函数直观查看收敛、连续性、极值等分析行为。",
      link: "https://www.desmos.com/calculator"
    },
    {
      category: "交互式工具",
      title: "Wolfram MathWorld",
      description: "权威的在线数学百科，定义、定理与例子可以作为参考书补充。",
      link: "https://mathworld.wolfram.com/"
    },
    {
      category: "习题与讨论",
      title: "Math Stack Exchange",
      description: "遇到具体题目或概念困惑时，可以在这里搜索类似问题或提问。",
      link: "https://math.stackexchange.com/"
    },
    {
      category: "符号计算",
      title: "SymPy - Python Symbolic Mathematics",
      description: "免费的 Python 符号计算库，可以辅助推导极限、积分与级数。",
      link: "https://www.sympy.org/"
    },
    {
      category: "中文开放课程",
      title: "中国大学 MOOC · 数学分析",
      description: "国内多所高校在 MOOC 平台开设的数学分析公开课程，适合中文环境学习。",
      link: "https://www.icourse163.org/search.htm?search=%E6%95%B0%E5%AD%A6%E5%88%86%E6%9E%90"
    }
  ];

  function renderNetworkResources(resources) {
    const container = UI.qs("#networkResourcesGrid");
    if (!container) return;

    const arr = Array.isArray(resources) ? resources : DEFAULT_NETWORK_RESOURCES;
    if (!arr.length) {
      container.innerHTML = "<div class='hint'>暂无推荐的外部资源。</div>";
      return;
    }

    let html = "";
    arr.forEach((item) => {
      html += "<div class='usage-item'>";
      html += "<div class='section-title-compact section-title'>" + UI.escapeHtml(item.category || "未分类") + "</div>";
      html += "<strong style='margin-top:4px; display:block;'>" + UI.escapeHtml(item.title || "") + "</strong>";
      html += "<p>" + UI.escapeHtml(item.description || "") + "</p>";
      if (item.link) {
        html += "<a class='jump-link' href='" + UI.escapeHtml(item.link) + "' target='_blank' rel='noopener'>前往查看 →</a>";
      }
      html += "</div>";
    });
    container.innerHTML = html;
  }

  async function loadCourseMaterials() {
    const statusEl = UI.qs("#materialsListStatus");
    const chapterSelect = UI.qs("#materialsListChapterSelect");
    const parseStatusSelect = UI.qs("#materialsListStatusSelect");
    const container = UI.qs("#materialsListContainer");

    if (!statusEl || !chapterSelect || !parseStatusSelect || !container) {
      return;
    }

    UI.showStatus(statusEl, "加载中…", false);

    const params = new URLSearchParams();
    const courseId = window.AppConfig && window.AppConfig.resolveCourseId ? window.AppConfig.resolveCourseId() : null;
    if (!courseId) {
      const courses = window.AppConfig && window.AppConfig.getCachedCourses ? window.AppConfig.getCachedCourses() : [];
      if (courses && courses.length) {
        params.set("courseId", String(courses[0].id));
      }
    } else {
      params.set("courseId", String(courseId));
    }
    params.set("take", "50");

    const chapterValue = chapterSelect.value;
    if (chapterValue) params.set("chapterId", chapterValue);

    const parseStatusValue = parseStatusSelect.value;
    if (parseStatusValue) params.set("parseStatus", parseStatusValue);

    try {
      const data = await Api.getJson("/api/course-materials?" + params.toString());
      renderList(data);
      UI.showStatus(statusEl, "已刷新。", false);
    } catch (_) {
      container.className = "status error";
      container.textContent = "资料列表加载失败，请稍后重试。";
      UI.showStatus(statusEl, "加载失败。", true);
    }
  }

  async function loadMaterialsChapters() {
    const courseId = window.AppConfig && window.AppConfig.resolveCourseId ? window.AppConfig.resolveCourseId() : null;
    if (!courseId) {
      const courses = window.AppConfig && window.AppConfig.getCachedCourses ? window.AppConfig.getCachedCourses() : [];
      if (courses && courses.length) {
        await loadChaptersForSelect(String(courses[0].id));
      }
      return;
    }
    await loadChaptersForSelect(String(courseId));
  }

  async function loadChaptersForSelect(courseId) {
    try {
      const chapters = await Api.getJson("/api/courses/" + courseId + "/chapters");
      const listSelect = UI.qs("#materialsListChapterSelect");
      if (listSelect) {
        let listHtml = "<option value=''>全部章节</option>";
        if (Array.isArray(chapters)) {
          chapters.forEach(function (ch) {
            listHtml += "<option value='" + ch.id + "'>" + UI.escapeHtml(ch.title || ch.name || "") + "</option>";
          });
        }
        listSelect.innerHTML = listHtml;
      }
    } catch (_) {
      console.warn("Failed to load chapters.");
    }
  }

  function switchMaterialsPane(view) {
    document.querySelectorAll(".materials-pane").forEach(function (pane) {
      const match = pane.getAttribute("data-materials-pane") === view;
      pane.style.display = match ? "" : "none";
    });
    document.querySelectorAll("[data-materials-view]").forEach(function (btn) {
      const match = btn.getAttribute("data-materials-view") === view;
      if (match) btn.classList.add("is-active");
      else btn.classList.remove("is-active");
    });
  }

  function bindMaterialEvents() {
    const refreshBtn = UI.qs("#materialsRefreshBtn");
    if (refreshBtn) refreshBtn.addEventListener("click", loadCourseMaterials);

    const chapterSelect = UI.qs("#materialsListChapterSelect");
    if (chapterSelect) chapterSelect.addEventListener("change", loadCourseMaterials);

    const statusSelect = UI.qs("#materialsListStatusSelect");
    if (statusSelect) statusSelect.addEventListener("change", loadCourseMaterials);

    document.querySelectorAll("[data-materials-view]").forEach(function (btn) {
      btn.addEventListener("click", function () {
        const view = btn.getAttribute("data-materials-view");
        switchMaterialsPane(view);
      });
    });
  }

  function initMaterialsPage() {
    if (!UI.qs("#materialsListContainer")) return;
    bindMaterialEvents();
    loadMaterialsChapters();
    loadCourseMaterials();
    renderNetworkResources(DEFAULT_NETWORK_RESOURCES);
  }

  document.addEventListener("DOMContentLoaded", function () {
    initMaterialsPage();
  });
})();
