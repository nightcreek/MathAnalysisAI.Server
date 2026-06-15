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

  function renderNetworkResources(resources) {
    const container = UI.qs("#networkResourcesGrid");
    if (!container) return;

    const arr = Array.isArray(resources) ? resources : [];
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
    fetchNetworkResources();
  }

  async function fetchNetworkResources() {
    var container = UI.qs("#networkResourcesGrid");
    if (!container) return;

    container.innerHTML = "<div class='hint'>加载中…</div>";

    var courseId = window.AppConfig && window.AppConfig.resolveCourseId ? window.AppConfig.resolveCourseId() : null;
    var params = "";
    if (courseId) {
      params = "?courseId=" + encodeURIComponent(courseId);
    }

    try {
      var data = await Api.getJson("/api/resources" + params);
      renderNetworkResources(data);
    } catch (_) {
      container.innerHTML = "<div class='hint'>网络资源加载失败，请稍后重试。</div>";
    }
  }

  document.addEventListener("DOMContentLoaded", function () {
    initMaterialsPage();
  });
})();
