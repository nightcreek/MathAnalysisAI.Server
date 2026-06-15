(function () {
  var mathAnalysis = window.MathAnalysis || {};

  function getRequestedMode() {
    try {
      var params = new URLSearchParams(window.location.search || "");
      var mode = String(params.get("mode") || "").toLowerCase();
      return mode === "ocr" ? "ocr" : "manual";
    } catch (_) {
      return "manual";
    }
  }

  function hasAnalysisDom() {
    return !!(
      UI.qs("#analyzeBtn") &&
      UI.qs("#analyzeStatus") &&
      UI.qs("#resultContainer") &&
      UI.qs("#problemTextInput") &&
      UI.qs("#studentSolutionTextInput") &&
      UI.qs("#chapterSelect") &&
      UI.qs("#modeSelect")
    );
  }

  function applyWorkbenchMode(mode) {
    var normalized = mode === "ocr" ? "ocr" : "manual";
    var ocrCard = UI.qs("#ocrWorkbenchCard");
    var hint = UI.qs("#analysisWorkbenchHint");
    var manualBtn = UI.qs("#manualModeBtn");
    var ocrBtn = UI.qs("#ocrModeBtn");

    if (ocrCard) {
      ocrCard.style.display = normalized === "ocr" ? "block" : "none";
    }

    if (hint) {
      hint.textContent = normalized === "ocr"
        ? "当前为拍照识别模式：先上传图片并检查 OCR 回填和公式卡片，再手动开始分析。"
        : "当前为手动输入模式：直接填写题目和我的解答，然后开始分析。";
    }

    if (manualBtn) {
      manualBtn.classList.toggle("is-active", normalized === "manual");
      manualBtn.setAttribute("aria-pressed", normalized === "manual" ? "true" : "false");
    }

    if (ocrBtn) {
      ocrBtn.classList.toggle("is-active", normalized === "ocr");
      ocrBtn.setAttribute("aria-pressed", normalized === "ocr" ? "true" : "false");
    }
  }

  function normalizeText(value, fallback) {
    if (value == null) return fallback || "";
    return String(value);
  }

  function normalizeList(list) {
    return UI.safeList(list).map(function (item) { return normalizeText(item, "").trim(); }).filter(Boolean);
  }

  function resolveStatus(isCorrect) {
    if (isCorrect === true) {
      return { text: "正确", className: "result-status-correct" };
    }
    if (isCorrect === false) {
      return { text: "需要修正", className: "result-status-wrong" };
    }
    return { text: "待判断", className: "result-status-unknown" };
  }

  function renderHeaderMeta(data) {
    var course = normalizeText(data.course, "未标注课程");
    var chapter = normalizeText(data.chapter, "未标注章节");
    var problemType = normalizeText(data.problemType, "unknown");
    var difficulty = normalizeText(data.difficulty, "unknown");

    return "<div class='result-summary-meta'>" +
      "<span>课程：" + UI.escapeHtml(course) + "</span>" +
      "<span>章节：" + UI.escapeHtml(chapter) + "</span>" +
      "<span>题型：" + UI.escapeHtml(problemType) + "</span>" +
      "<span>难度：" + UI.escapeHtml(difficulty) + "</span>" +
      "</div>";
  }

  function renderMistakeTags(tags) {
    var arr = normalizeList(tags);
    if (!arr.length) return "<div class='status'>暂未发现明确问题</div>";
    return "<div class='pill-row'>" + arr.map(function (tag) {
      return "<span class='mistake-tag-pill'>" + UI.escapeHtml(tag) + "</span>";
    }).join("") + "</div>";
  }

  var KNOWLEDGE_POINT_DISPLAY_MAP = {
    "ma.multiple_integral.double_integral": "二重积分",
    "ma.multiple_integral.triple_integral": "三重积分",
    "ma.multiple_integral.integration_order": "积分区域与积分次序",
    "ma.multiple_integral.change_of_variables": "重积分变量替换",
    "ma.multiple_integral.polar_coordinates": "极坐标下的重积分",
    "ma.multiple_integral.cylindrical_coordinates": "柱坐标下的重积分",
    "ma.multiple_integral.spherical_coordinates": "球坐标下的重积分",
    "ma.line_integral.first_kind": "第一类曲线积分",
    "ma.line_integral.second_kind": "第二类曲线积分",
    "ma.line_integral.path_independence": "路径无关性与保守场",
    "ma.line_integral.green_formula": "Green 公式",
    "ma.surface_integral.first_kind": "第一类曲面积分",
    "ma.surface_integral.second_kind": "第二类曲面积分",
    "ma.surface_integral.gauss_formula": "Gauss 公式",
    "ma.surface_integral.stokes_formula": "Stokes 公式",
    "ma.function_series.uniform_convergence": "函数项级数一致收敛",
    "ma.function_series.pointwise_vs_uniform": "逐点收敛与一致收敛区分",
    "ma.power_series.endpoint_convergence": "幂级数端点收敛",
    "ma.taylor.remainder": "泰勒公式余项",
    "ma.improper_integral.singularity_split": "反常积分瑕点拆分",
    "ma.improper_integral.convergence_criteria": "反常积分收敛判别",
    "ma.improper_integral.comparison_test": "比较判别法",
    "ma.improper_integral.infinite_interval": "无穷限反常积分",
    "ma.improper_integral.unbounded_function": "无界函数反常积分",
    "double integral": "二重积分",
    "triple integral": "三重积分",
    "multiple integral": "重积分",
    "order of integration": "积分区域与积分次序",
    "change of variables": "重积分变量替换",
    "polar coordinates": "极坐标下的重积分",
    "cylindrical coordinates": "柱坐标下的重积分",
    "spherical coordinates": "球坐标下的重积分",
    "line integral": "曲线积分",
    "scalar line integral": "第一类曲线积分",
    "vector line integral": "第二类曲线积分",
    "path independent": "路径无关性与保守场",
    "conservative field": "路径无关性与保守场",
    "surface integral": "曲面积分",
    "scalar surface integral": "第一类曲面积分",
    "flux integral": "第二类曲面积分",
    "uniform convergence": "函数项级数一致收敛",
    "pointwise convergence": "逐点收敛",
    "endpoint convergence": "幂级数端点收敛",
    "remainder term": "泰勒公式余项",
    "singularity split": "反常积分瑕点拆分",
    "improper integral singularity": "反常积分瑕点拆分",
    "mean value theorem conditions": "中值定理条件检查",
    "interchange of limit and integral": "极限与积分交换条件"
  };

  function normalizeKnowledgePointKey(value) {
    return normalizeText(value, "").trim().toLowerCase().replace(/\s+/g, " ");
  }

  function humanizeKnowledgePointCode(value) {
    var raw = String(value || "").trim();
    if (!raw) return "";
    var lastSegment = raw.split(".").pop() || raw;
    return lastSegment.replace(/_/g, " ").replace(/\s+/g, " ").trim();
  }

  function lookupFromMap(raw) {
    var normalizedKey = normalizeKnowledgePointKey(raw);
    var directMap = KNOWLEDGE_POINT_DISPLAY_MAP[normalizedKey];
    if (directMap) {
      return { name: directMap, code: raw, known: true };
    }
    return null;
  }

  function lookupFromKpModule(raw) {
    if (!(window.KnowledgePoints && window.KnowledgePoints.formatKnowledgePoint)) {
      return null;
    }

    var kp = window.KnowledgePoints.formatKnowledgePoint(raw);
    var kpName = normalizeText(kp.name, "").trim();
    var kpCode = normalizeText(kp.code, raw).trim();
    var kpKnown = !!kp.known;

    var kpNormalizedKey = normalizeKnowledgePointKey(kpCode);
    var aliasMap = KNOWLEDGE_POINT_DISPLAY_MAP[kpNormalizedKey];
    if (aliasMap) {
      return { name: aliasMap, code: kpCode, known: true };
    }

    if (kpKnown && kpName && kpName !== kpCode) {
      return { name: kpName, code: kpCode, known: true };
    }

    return null;
  }

  function fallbackHumanize(raw) {
    if (/[\u4e00-\u9fff]/.test(raw)) {
      return { name: raw, code: raw, known: true };
    }

    var humanized = humanizeKnowledgePointCode(raw);
    return { name: humanized || raw, code: raw, known: false };
  }

  function resolveKnowledgePointLabel(item) {
    var raw = normalizeText(item, "").trim();
    if (!raw) {
      return null;
    }

    var mapResult = lookupFromMap(raw);
    if (mapResult) return mapResult;

    var moduleResult = lookupFromKpModule(raw);
    if (moduleResult) return moduleResult;

    return fallbackHumanize(raw);
  }

  function renderKnowledgePoints(codes) {
    var arr = normalizeList(codes);
    if (!arr.length) return "<div class='status'>暂无关联知识点</div>";

    var tags = arr
      .map(resolveKnowledgePointLabel)
      .filter(Boolean)
      .map(function (item) {
        var name = UI.escapeHtml(item.name || item.code || "");
        var code = UI.escapeHtml(item.code || "");
        var showCode = item.code && item.name && item.name !== item.code && item.known !== true;
        return "<span class='knowledge-pill'>" +
          "<span class='knowledge-name'>" + name + "</span>" +
          (showCode ? "<span class='knowledge-code'>" + code + "</span>" : "") +
        "</span>";
      });

    if (!tags.length) {
      return "<div class='status'>暂无关联知识点</div>";
    }

    return "<div class='pill-row knowledge-tag-row'>" + tags.join("") + "</div>";
  }

  function renderSolutionSteps(steps) {
    var arr = UI.safeList(steps);
    if (!arr.length) return "<div class='status'>暂无标准解答步骤</div>";

    var html = "";
    arr.forEach(function (item, index) {
      var step = typeof item === "object" && item ? item : null;
      var stepNo = normalizeText(step && step.step, index + 1);
      var title = normalizeText(step && step.title, "标准解答");
      var content = normalizeText(step && step.content, typeof item === "string" ? item : "");

      html += "<div class='solution-step-card'>" +
        "<div class='solution-step-head'>步骤 " + UI.escapeHtml(stepNo) + "：" + UI.escapeHtml(title) + "</div>" +
        "<div class='solution-step-content'>" + UI.escapeHtml(content).replace(/\n/g, "<br>") + "</div>" +
      "</div>";
    });

    return html;
  }

  function renderSuggestionList(list) {
    var arr = normalizeList(list);
    if (!arr.length) return "";
    return UI.renderList(arr);
  }

  function mergeSuggestions(reviewSuggestions, studentSuggestions) {
    var merged = [];
    var seen = new Set();
    var pushIfNew = function (text) {
      var normalized = normalizeText(text, "").trim();
      if (!normalized) return;
      var key = normalized.toLowerCase();
      if (seen.has(key)) return;
      seen.add(key);
      merged.push(normalized);
    };

    normalizeList(reviewSuggestions).forEach(pushIfNew);
    normalizeList(studentSuggestions).forEach(pushIfNew);
    return merged;
  }

  function renderVisualization(visualization) {
    var v = visualization && typeof visualization === "object" ? visualization : null;
    if (!v || v.shouldUse !== true) {
      return "";
    }

    var reason = normalizeText(v.reason, "");
    var caption = normalizeText(v.caption, "");
    var commands = normalizeList(v.geoGebraCommands);

    var html = "<div class='result-section'><h3>可视化建议</h3>";
    if (caption) {
      html += "<div><strong>图示标题：</strong>" + UI.escapeHtml(caption) + "</div>";
    }
    if (reason) {
      html += "<div><strong>说明：</strong>" + UI.escapeHtml(reason) + "</div>";
    }

    if (commands.length) {
      html += "<div style='margin-top:8px;'><strong>GeoGebra 命令：</strong></div>";
      html += UI.renderList(commands);
    } else {
      html += "<div class='status'>未返回可执行命令。</div>";
    }

    html += "</div>";
    return html;
  }

  function renderReliability(data) {
    var reliability = normalizeText(data && data.answerReliability, "");
    if (!reliability) {
      return "";
    }

    var labels = {
      Reliable: "答案可信度：较高",
      NeedsReview: "答案需要复核",
      Uncertain: "答案可靠性不确定",
      UnsafeToUse: "答案存在明显风险，不建议直接使用"
    };

    var badgeClass = {
      Reliable: "result-status-correct",
      NeedsReview: "result-status-wrong",
      Uncertain: "result-status-unknown",
      UnsafeToUse: "result-status-wrong"
    };

    var sectionClass = {
      Reliable: "reliability-reliable",
      NeedsReview: "reliability-needs-review",
      Uncertain: "reliability-uncertain",
      UnsafeToUse: "reliability-unsafe"
    };

    var reasons = normalizeList(data && data.reliabilityReasons);
    var warnings = normalizeList(data && data.verifierWarnings);

    var html = "<div class='result-section result-reliability-section " + (sectionClass[reliability] || "") + "'><h3>答案可靠性</h3>";
    html += "<div class='result-status-pill " + (badgeClass[reliability] || "result-status-unknown") + "'>" +
      UI.escapeHtml(labels[reliability] || reliability) +
      "</div>";

    if (reasons.length) {
      html += "<div style='margin-top:8px;'><strong>可靠性原因：</strong></div>" + UI.renderList(reasons);
    }
    if (warnings.length) {
      html += "<div style='margin-top:8px;'><strong>自检提示：</strong></div>" + UI.renderList(warnings);
    }

    html += "</div>";
    return html;
  }

  function renderAnalysisReport(data) {
    var review = data && data.studentSolutionReview && typeof data.studentSolutionReview === "object"
      ? data.studentSolutionReview
      : {};
    var status = resolveStatus(review.isCorrect);
    var mainIssue = UI.formatAnalysisMainIssue(normalizeText(review.mainIssue, "").trim());
    var logicGaps = normalizeList(review.logicGaps);
    var mistakeTags = normalizeList(data.mistakeTags);
    var mergedSuggestions = mergeSuggestions(data.reviewSuggestions, review.suggestions);

    var html = "";

    html += renderReliability(data);

    html += "<div class='result-summary-card'>" +
      "<div class='result-summary-header'>" +
      "<span class='result-status-pill " + status.className + "'>" + UI.escapeHtml(status.text) + "</span>" +
      "</div>" +
      "<div class='result-summary-main'>" + UI.escapeHtml(mainIssue || "已完成分析，请查看下方详细反馈。") + "</div>" +
      renderHeaderMeta(data) +
      "</div>";

    html += "<div class='result-section result-knowledge-section'><h3>关联知识点</h3>" + renderKnowledgePoints(data.knowledgePoints) + "</div>";

    html += "<div class='result-section'><h3>我的解答问题</h3>";
    html += "<div><strong>主要问题：</strong>" + UI.escapeHtml(mainIssue || "暂未发现明确问题") + "</div>";
    if (logicGaps.length) {
      html += "<div style='margin-top:8px;'><strong>逻辑漏洞：</strong></div>" + UI.renderList(logicGaps);
    }
    html += "<div style='margin-top:8px;'><strong>错误标签：</strong></div>" + renderMistakeTags(mistakeTags);
    html += "</div>";

    html += "<div class='result-section'><h3>标准解答</h3>" + renderSolutionSteps(data.standardSolution) + "</div>";

    var solutionOverview = normalizeText(data.solutionOverview, "").trim();
    if (solutionOverview) {
      html += "<div class='result-section'><h3>解题思路概览</h3><div>" +
        UI.escapeHtml(solutionOverview).replace(/\n/g, "<br>") +
      "</div></div>";
    }

    if (mergedSuggestions.length) {
      html += "<div class='result-section'><h3>下一步建议</h3>" + renderSuggestionList(mergedSuggestions) + "</div>";
    }

    html += renderVisualization(data.visualization);
    return html;
  }

  async function ensureUserAuthenticated() {
    var currentUserId = null;
    if (window.Auth && window.Auth.getCurrentUserId) {
      currentUserId = window.Auth.getCurrentUserId();
      if (currentUserId == null && window.Auth.loadCurrentUser) {
        await window.Auth.loadCurrentUser();
        currentUserId = window.Auth.getCurrentUserId();
      }
    }
    return currentUserId;
  }

  function validateOcrConfirmed(status) {
    var ocrRecordId = null;
    if (window.PhotoSolutionOcr && typeof window.PhotoSolutionOcr.getRecordId === "function") {
      ocrRecordId = window.PhotoSolutionOcr.getRecordId();
      if (ocrRecordId && typeof window.PhotoSolutionOcr.isConfirmed === "function" && !window.PhotoSolutionOcr.isConfirmed()) {
        UI.showStatus(status, "OCR 结果尚未确认，请先完成 OCR 确认后再分析。", true);
        return null;
      }
    }
    return ocrRecordId;
  }

  function buildAnalysisPayload(currentUserId, ocrRecordId) {
    var problemText = UI.qs("#problemTextInput").value.trim();
    var studentSolutionText = UI.qs("#studentSolutionTextInput").value.trim();
    var chapterId = parseInt(UI.qs("#chapterSelect").value, 10);
    var mode = UI.qs("#modeSelect").value;

    return {
      courseId: (window.AppConfig && window.AppConfig.resolveCourseId ? window.AppConfig.resolveCourseId() : null) || (window.AppConfig && window.AppConfig.getCachedCourses ? (window.AppConfig.getCachedCourses()[0] || {}).id : null),
      chapterId: Number.isNaN(chapterId) ? AppConfig.defaultChapterId : chapterId,
      problemText: problemText,
      studentSolutionText: studentSolutionText,
      analysisMode: mode || "review_solution",
      userId: currentUserId,
      ocrRecordId: ocrRecordId,
      formulas: (window.MathLiveOcr && typeof window.MathLiveOcr.getFormulas === "function")
        ? window.MathLiveOcr.getFormulas()
        : []
    };
  }

  async function analyzeText() {
    if (!hasAnalysisDom()) {
      return;
    }

    var btn = UI.qs("#analyzeBtn");
    var status = UI.qs("#analyzeStatus");
    var box = UI.qs("#resultContainer");
    var problemText = UI.qs("#problemTextInput").value.trim();

    if (!problemText) {
      UI.showStatus(status, "请先输入题目。", true);
      return;
    }

    var currentUserId = await ensureUserAuthenticated();

    var ocrRecordId = validateOcrConfirmed(status);
    if (ocrRecordId === null) return;

    var payload = buildAnalysisPayload(currentUserId, ocrRecordId);

    btn.disabled = true;
    UI.showStatus(status, "正在分析，请稍候……", false);

    try {
      var data = await Api.postJson("/api/learning-analysis/analyze", payload);
      box.innerHTML = renderAnalysisReport(data || {});
      UI.showStatus(status, "分析完成。", false);
    } catch (err) {
      UI.showStatus(status, UI.formatApiErrorMessage(err, "analysis"), true);
    } finally {
      btn.disabled = false;
      if (window.MathJax) MathJax.typeset();
    }
  }

  async function analyzeTextStream() {
    if (!hasAnalysisDom()) {
      return;
    }

    var btn = UI.qs("#analyzeBtn");
    var status = UI.qs("#analyzeStatus");
    var box = UI.qs("#resultContainer");
    var problemText = UI.qs("#problemTextInput").value.trim();

    if (!problemText) {
      UI.showStatus(status, "请先输入题目。", true);
      return;
    }

    var currentUserId = await ensureUserAuthenticated();

    var ocrRecordId = validateOcrConfirmed(status);
    if (ocrRecordId === null) return;

    var payload = buildAnalysisPayload(currentUserId, ocrRecordId);

    btn.disabled = true;
    UI.showStatus(status, "正在流式分析……", false);
    box.innerHTML = '<div class="streaming-container"><div class="streaming-indicator"><span class="streaming-dot"></span> 正在生成分析结果…</div><pre class="streaming-content"></pre></div>';

    try {
      var response = await fetch("/api/learning-analysis/analyze/stream", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });

      if (!response.ok) {
        var errorText = "";
        try { errorText = await response.text(); } catch (_) {}
        var errorData = null;
        try { errorData = errorText ? JSON.parse(errorText) : null; } catch (_) {}
        var err = new Error("HTTP " + response.status);
        err.status = response.status;
        err.data = errorData;
        err.isAuthRequired = response.status === 401;
        throw err;
      }

      if (!response.body) {
        throw new Error("浏览器不支持流式读取。");
      }

      var reader = response.body.getReader();
      var decoder = new TextDecoder();
      var buffer = "";
      var accumulatedText = "";
      var streamingEl = box.querySelector(".streaming-content");
      var indicatorEl = box.querySelector(".streaming-indicator");

      while (true) {
        var readResult = await reader.read();
        if (readResult.done) break;

        buffer += decoder.decode(readResult.value, { stream: true });

        var lines = buffer.split("\n");
        buffer = lines.pop() || "";

        for (var i = 0; i < lines.length; i++) {
          var line = lines[i].trim();
          if (!line || !line.startsWith("data: ")) continue;

          var data = line.substring(6);
          if (data === "[DONE]") break;

          try {
            var chunk = JSON.parse(data);
            accumulatedText += chunk;
            streamingEl.textContent = accumulatedText;
          } catch (_) {
          }
        }
      }

      if (indicatorEl) indicatorEl.style.display = "none";

      try {
        var parsed = JSON.parse(accumulatedText);
        box.innerHTML = renderAnalysisReport(parsed || {});
      } catch (_) {
        box.innerHTML = '<div class="streaming-result-raw"><div class="result-section"><h3>流式分析结果</h3><pre class="streaming-content-raw">' + UI.escapeHtml(accumulatedText) + '</pre></div></div>';
      }

      UI.showStatus(status, "分析完成。", false);
    } catch (err) {
      UI.showStatus(status, UI.formatApiErrorMessage(err, "analysis"), true);
      if (box.querySelector(".streaming-container")) {
        box.innerHTML = '<div class="empty-state"><h3>分析失败</h3><p>' + UI.escapeHtml(err.message || "未知错误") + '</p></div>';
      }
    } finally {
      btn.disabled = false;
      if (window.MathJax) MathJax.typeset();
    }
  }

  async function loadChapters() {
     var chapterSelect = UI.qs("#chapterSelect");
     if (!chapterSelect) return;

     var courseId = window.AppConfig && window.AppConfig.resolveCourseId ? window.AppConfig.resolveCourseId() : null;
     if (!courseId) {
       try {
         var courses = await window.AppConfig.fetchCourses();
         if (courses && courses.length) courseId = courses[0].id;
       } catch (_) {}
     }
     if (!courseId) { console.warn("No course available for chapter loading."); return; }

     try {
       var chapters = await Api.getJson("/api/courses/" + courseId + "/chapters");
       if (!chapters || !chapters.length) {
         chapterSelect.innerHTML = '<option value="">暂无章节</option>';
         return;
       }

       chapterSelect.innerHTML = "";
       chapters.forEach(function (chapter) {
         var option = document.createElement("option");
         option.value = chapter.id;
         option.textContent = chapter.name;
         chapterSelect.appendChild(option);
       });

       var defaultChapterId = (window.AppConfig && window.AppConfig.defaultChapterId) ? window.AppConfig.defaultChapterId : null;
       if (defaultChapterId) {
         chapterSelect.value = defaultChapterId;
       }
     } catch (err) {
       console.warn("Failed to load chapters:", err);
     }
   }

  function bindEvents() {
    loadChapters();
    var manualBtn = UI.qs("#manualModeBtn");
    var ocrBtn = UI.qs("#ocrModeBtn");
    var analyzeBtn = UI.qs("#analyzeBtn");

    if (manualBtn) {
      manualBtn.addEventListener("click", function () {
        applyWorkbenchMode("manual");
      });
    }

    if (ocrBtn) {
      ocrBtn.addEventListener("click", function () {
        applyWorkbenchMode("ocr");
      });
    }

    if (analyzeBtn) {
      analyzeBtn.addEventListener("click", analyzeTextStream);
    }
  }

  mathAnalysis.switchAnalysisMode = applyWorkbenchMode;
  mathAnalysis.analyzeText = analyzeText;
  mathAnalysis.analyzeTextStream = analyzeTextStream;
  window.MathAnalysis = mathAnalysis;

  document.addEventListener("DOMContentLoaded", function () {
    bindEvents();
    applyWorkbenchMode(getRequestedMode());
  });
})();
