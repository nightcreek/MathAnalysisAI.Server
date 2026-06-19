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

  function renderBootstrapFailure(message, traceId) {
    UI.renderBootstrapError("#materialsListContainer", message, function () {
      window.AppConfig.clearCourseCache();
      initMaterialsPage();
    }, traceId);
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
      const state = window.AppConfig.getCourseLoadState();
      renderBootstrapFailure(state.message || "课程列表尚未准备好。", state.traceId || "");
      UI.showStatus(statusEl, "加载失败。", true);
      return;
    }

    params.set("courseId", String(courseId));
    params.set("take", "50");

    if (chapterSelect.value) params.set("chapterId", chapterSelect.value);
    if (parseStatusSelect.value) params.set("parseStatus", parseStatusSelect.value);

    try {
      const data = await Api.getJson("/api/course-materials?" + params.toString());
      renderList(data);
      UI.showStatus(statusEl, "已刷新。", false);
    } catch (err) {
      if (err && err.isAuthRequired) {
        UI.renderLoginRequired(container, UI.formatApiErrorMessage(err, "materials"), function () {
          window.location.href = "/login.html";
        });
      } else {
        UI.renderErrorPanel(container, {
          title: "资料列表加载失败",
          message: UI.formatApiErrorMessage(err, "materials"),
          traceId: err && err.traceId ? err.traceId : "",
          actionLabel: "重试",
          onAction: loadCourseMaterials
        });
      }
      UI.showStatus(statusEl, "加载失败。", true);
    }
  }

  async function loadMaterialsChapters() {
    const courseState = window.AppConfig.getCourseLoadState();
    if (courseState.status === "degraded") {
      renderBootstrapFailure(courseState.message, courseState.traceId);
      return;
    }

    let courseId = window.AppConfig && window.AppConfig.resolveCourseId ? window.AppConfig.resolveCourseId() : null;
    if (!courseId) {
      try {
        const courses = await window.AppConfig.fetchCourses();
        if (courses && courses.length) {
          courseId = courses[0].id;
        }
      } catch (_) {
        renderBootstrapFailure(window.AppConfig.getCourseLoadState().message, window.AppConfig.getCourseLoadState().traceId);
        return;
      }
    }

    if (!courseId) {
      renderBootstrapFailure("当前没有可用课程。", "");
      return;
    }

    try {
      const result = await Api.getJsonDetailed("/api/courses/" + courseId + "/chapters");
      const chapters = Array.isArray(result.data) ? result.data : [];
      const listSelect = UI.qs("#materialsListChapterSelect");
      if (!listSelect) return;

      if (result.meta && result.meta.degraded) {
        UI.renderBootstrapError("#materialsListContainer", "章节列表当前处于降级状态，请稍后重试。", loadMaterialsChapters, result.meta.traceId);
      }

      let listHtml = "<option value=''>全部章节</option>";
      chapters.forEach(function (ch) {
        listHtml += "<option value='" + ch.id + "'>" + UI.escapeHtml(ch.title || ch.name || "") + "</option>";
      });
      listSelect.innerHTML = listHtml;
    } catch (err) {
      UI.renderBootstrapError("#materialsListContainer", UI.formatApiErrorMessage(err, "bootstrap"), loadMaterialsChapters, err && err.traceId ? err.traceId : "");
    }
  }

  function switchMaterialsPane(view) {
    document.querySelectorAll(".materials-pane").forEach(function (pane) {
      pane.style.display = pane.getAttribute("data-materials-pane") === view ? "" : "none";
    });
    document.querySelectorAll("[data-materials-view]").forEach(function (btn) {
      btn.classList.toggle("is-active", btn.getAttribute("data-materials-view") === view);
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
        switchMaterialsPane(btn.getAttribute("data-materials-view"));
      });
    });
  }

  async function fetchNetworkResources() {
    var container = UI.qs("#networkResourcesGrid");
    if (!container) return;

    container.innerHTML = "<div class='hint'>加载中…</div>";

    var courseId = window.AppConfig && window.AppConfig.resolveCourseId ? window.AppConfig.resolveCourseId() : null;
    var params = courseId ? ("?courseId=" + encodeURIComponent(courseId)) : "";

    try {
      var result = await Api.getJsonDetailed("/api/resources" + params);
      if (result.meta && result.meta.degraded) {
        UI.renderBootstrapError(container, "网络资源当前处于降级状态，请稍后重试。", fetchNetworkResources, result.meta.traceId);
        return;
      }
      renderNetworkResources(result.data);
    } catch (err) {
      UI.renderErrorPanel(container, {
        title: "网络资源加载失败",
        message: UI.formatApiErrorMessage(err, "materials"),
        traceId: err && err.traceId ? err.traceId : "",
        actionLabel: "重试",
        onAction: fetchNetworkResources
      });
    }
  }

  async function initMaterialsPage() {
    if (!UI.qs("#materialsListContainer")) return;
    bindMaterialEvents();

    try {
      await window.AppConfig.fetchCourses();
    } catch (_) {}

    var courseState = window.AppConfig.getCourseLoadState();
    if (courseState.status === "degraded" || courseState.status === "error") {
      renderBootstrapFailure(courseState.message || "课程列表加载失败。", courseState.traceId || "");
      return;
    }

    await loadMaterialsChapters();
    await loadCourseMaterials();
    await fetchNetworkResources();
  }

  document.addEventListener("DOMContentLoaded", function () {
    initMaterialsPage();
  });
})();
