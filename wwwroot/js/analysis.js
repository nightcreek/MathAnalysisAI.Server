(function () {
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

  function initAnalysisWorkbench() {
    var manualBtn = UI.qs("#manualModeBtn");
    var ocrBtn = UI.qs("#ocrModeBtn");
    var ocrCard = UI.qs("#ocrWorkbenchCard");

    if (!manualBtn || !ocrBtn || !ocrCard) {
      return;
    }

    manualBtn.addEventListener("click", function () {
      applyWorkbenchMode("manual");
    });

    ocrBtn.addEventListener("click", function () {
      applyWorkbenchMode("ocr");
    });

    applyWorkbenchMode(getRequestedMode());
  }

  function normalizeText(value, fallback) {
    if (value == null) return fallback || "";
    return String(value);
  }

  function normalizeList(list) {
    return UI.safeList(list).map(item => normalizeText(item, "").trim()).filter(Boolean);
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
    const course = normalizeText(data.course, "未标注课程");
    const chapter = normalizeText(data.chapter, "未标注章节");
    const problemType = normalizeText(data.problemType, "unknown");
    const difficulty = normalizeText(data.difficulty, "unknown");

    return "<div class='result-summary-meta'>" +
      "<span>课程：" + UI.escapeHtml(course) + "</span>" +
      "<span>章节：" + UI.escapeHtml(chapter) + "</span>" +
      "<span>题型：" + UI.escapeHtml(problemType) + "</span>" +
      "<span>难度：" + UI.escapeHtml(difficulty) + "</span>" +
      "</div>";
  }

  function renderMistakeTags(tags) {
    const arr = normalizeList(tags);
    if (!arr.length) return "<div class='status'>暂未发现明确问题</div>";
    return "<div class='pill-row'>" + arr.map(tag =>
      "<span class='mistake-tag-pill'>" + UI.escapeHtml(tag) + "</span>"
    ).join("") + "</div>";
  }

  const KNOWLEDGE_POINT_DISPLAY_MAP = {
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
    return normalizeText(value, "")
      .trim()
      .toLowerCase()
      .replace(/\s+/g, " ");
  }

  function looksLikeCodedKnowledgePoint(value) {
    return /^ma\.[a-z0-9_.-]+$/i.test(String(value || "").trim()) || String(value || "").includes("_");
  }

  function humanizeKnowledgePointCode(value) {
    const raw = String(value || "").trim();
    if (!raw) return "";
    const lastSegment = raw.split(".").pop() || raw;
    return lastSegment.replace(/_/g, " ").replace(/\s+/g, " ").trim();
  }

  function resolveKnowledgePointLabel(item) {
    const raw = normalizeText(item, "").trim();
    if (!raw) {
      return null;
    }

    const normalizedKey = normalizeKnowledgePointKey(raw);
    const directMap = KNOWLEDGE_POINT_DISPLAY_MAP[normalizedKey];
    if (directMap) {
      return { name: directMap, code: raw, known: true };
    }

    const kp = window.KnowledgePoints && window.KnowledgePoints.formatKnowledgePoint
      ? window.KnowledgePoints.formatKnowledgePoint(raw)
      : { name: raw, code: raw, known: false };

    const kpName = normalizeText(kp.name, "").trim();
    const kpCode = normalizeText(kp.code, raw).trim();
    const kpKnown = !!kp.known;
    const kpNormalizedKey = normalizeKnowledgePointKey(kpCode);
    const aliasMap = KNOWLEDGE_POINT_DISPLAY_MAP[kpNormalizedKey];

    if (aliasMap) {
      return { name: aliasMap, code: kpCode, known: true };
    }

    if (kpKnown && kpName && kpName !== kpCode) {
      return { name: kpName, code: kpCode, known: true };
    }

    if (/[\u4e00-\u9fff]/.test(raw)) {
      return { name: raw, code: kpCode, known: true };
    }

    const humanized = humanizeKnowledgePointCode(kpCode || raw);
    return {
      name: humanized || kpName || kpCode || raw,
      code: kpCode || raw,
      known: false
    };
  }

  function renderKnowledgePoints(codes) {
    const arr = normalizeList(codes);
    if (!arr.length) return "<div class='status'>暂无关联知识点</div>";

    const tags = arr
      .map(resolveKnowledgePointLabel)
      .filter(Boolean)
      .map(item => {
        const name = UI.escapeHtml(item.name || item.code || "");
        const code = UI.escapeHtml(item.code || "");
        const showCode = item.code && item.name && item.name !== item.code && item.known !== true;
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
    const arr = UI.safeList(steps);
    if (!arr.length) return "<div class='status'>暂无标准解答步骤</div>";

    let html = "";
    arr.forEach((item, index) => {
      const step = typeof item === "object" && item ? item : null;
      const stepNo = normalizeText(step && step.step, index + 1);
      const title = normalizeText(step && step.title, "标准解答");
      const content = normalizeText(step && step.content, typeof item === "string" ? item : "");

      html += "<div class='solution-step-card'>" +
        "<div class='solution-step-head'>步骤 " + UI.escapeHtml(stepNo) + "：" + UI.escapeHtml(title) + "</div>" +
        "<div class='solution-step-content'>" + UI.escapeHtml(content).replace(/\n/g, "<br>") + "</div>" +
      "</div>";
    });

    return html;
  }

  function renderSuggestionList(list) {
    const arr = normalizeList(list);
    if (!arr.length) return "";
    return UI.renderList(arr);
  }

  function mergeSuggestions(reviewSuggestions, studentSuggestions) {
    const merged = [];
    const seen = new Set();
    const pushIfNew = (text) => {
      const normalized = normalizeText(text, "").trim();
      if (!normalized) return;
      const key = normalized.toLowerCase();
      if (seen.has(key)) return;
      seen.add(key);
      merged.push(normalized);
    };

    normalizeList(reviewSuggestions).forEach(pushIfNew);
    normalizeList(studentSuggestions).forEach(pushIfNew);
    return merged;
  }

  function renderVisualization(visualization) {
    const v = visualization && typeof visualization === "object" ? visualization : null;
    if (!v || v.shouldUse !== true) {
      return "";
    }

    const reason = normalizeText(v.reason, "");
    const caption = normalizeText(v.caption, "");
    const commands = normalizeList(v.geoGebraCommands);

    let html = "<div class='result-section'><h3>可视化建议</h3>";
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
    const reliability = normalizeText(data && data.answerReliability, "");
    if (!reliability) {
      return "";
    }

    const labels = {
      Reliable: "答案可信度：较高",
      NeedsReview: "答案需要复核",
      Uncertain: "答案可靠性不确定",
      UnsafeToUse: "答案存在明显风险，不建议直接使用"
    };

    const badgeClass = {
      Reliable: "result-status-correct",
      NeedsReview: "result-status-wrong",
      Uncertain: "result-status-unknown",
      UnsafeToUse: "result-status-wrong"
    };

    const sectionClass = {
      Reliable: "reliability-reliable",
      NeedsReview: "reliability-needs-review",
      Uncertain: "reliability-uncertain",
      UnsafeToUse: "reliability-unsafe"
    };

    const reasons = normalizeList(data && data.reliabilityReasons);
    const warnings = normalizeList(data && data.verifierWarnings);

    let html = "<div class='result-section result-reliability-section " + (sectionClass[reliability] || "") + "'><h3>答案可靠性</h3>";
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
    const review = data && data.studentSolutionReview && typeof data.studentSolutionReview === "object"
      ? data.studentSolutionReview
      : {};
    const status = resolveStatus(review.isCorrect);
    const mainIssue = normalizeText(review.mainIssue, "").trim();
    const logicGaps = normalizeList(review.logicGaps);
    const mistakeTags = normalizeList(data.mistakeTags);
    const mergedSuggestions = mergeSuggestions(data.reviewSuggestions, review.suggestions);

    let html = "";

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

    const solutionOverview = normalizeText(data.solutionOverview, "").trim();
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

  async function analyzeText() {
    if (!hasAnalysisDom()) {
      return;
    }

    const btn = UI.qs("#analyzeBtn");
    const status = UI.qs("#analyzeStatus");
    const box = UI.qs("#resultContainer");
    const problemText = UI.qs("#problemTextInput").value.trim();
    const studentSolutionText = UI.qs("#studentSolutionTextInput").value.trim();
    const chapterId = parseInt(UI.qs("#chapterSelect").value, 10);
    const mode = UI.qs("#modeSelect").value;

    if (!problemText) {
      UI.showStatus(status, "请先输入题目。", true);
      return;
    }

    let currentUserId = null;
    if (window.Auth && window.Auth.getCurrentUserId) {
      currentUserId = window.Auth.getCurrentUserId();
      if (currentUserId == null && window.Auth.loadCurrentUser) {
        await window.Auth.loadCurrentUser();
        currentUserId = window.Auth.getCurrentUserId();
      }
    }
    if (currentUserId == null && window.AppConfig && AppConfig.developmentFallbackUser) {
      currentUserId = Number(AppConfig.developmentFallbackUser.userId) || null;
    }

    var ocrRecordId = null;
    if (window.PhotoSolutionOcr && typeof window.PhotoSolutionOcr.getRecordId === "function") {
      ocrRecordId = window.PhotoSolutionOcr.getRecordId();
      if (ocrRecordId && typeof window.PhotoSolutionOcr.isConfirmed === "function" && !window.PhotoSolutionOcr.isConfirmed()) {
        UI.showStatus(status, "OCR 结果尚未确认，请先完成 OCR 确认后再分析。", true);
        return;
      }
    }

    const payload = {
      courseId: AppConfig.defaultCourseId,
      chapterId: Number.isNaN(chapterId) ? AppConfig.defaultChapterId : chapterId,
      problemText,
      studentSolutionText,
      analysisMode: mode || "review_solution",
      userId: currentUserId,
      ocrRecordId,
      formulas: (window.MathLiveOcr && typeof window.MathLiveOcr.getFormulas === "function")
        ? window.MathLiveOcr.getFormulas()
        : []
    };

    btn.disabled = true;
    UI.showStatus(status, "正在分析，请稍候……", false);

    try {
      const data = await Api.postJson("/api/learning-analysis/analyze", payload);
      box.innerHTML = renderAnalysisReport(data || {});
      UI.showStatus(status, "分析完成。", false);
    } catch (err) {
      let message;
      if (err && err.isRateLimited) {
        message = UI.formatRateLimitMessage(err);
      } else {
        message = "分析失败，请稍后重试。";
        const data = err && err.data ? err.data : null;
        const msg = data && (data.message || data.title) ? (data.message || data.title) : "";
        if (msg && (location.hostname === "localhost" || location.hostname === "127.0.0.1")) {
          message += "（开发信息：" + msg + "）";
        }
      }
      UI.showStatus(status, message, true);
    } finally {
      btn.disabled = false;
      if (window.MathJax) MathJax.typeset();
    }
  }

  window.analyzeText = analyzeText;
  window.switchAnalysisMode = applyWorkbenchMode;

  document.addEventListener("DOMContentLoaded", function () {
    initAnalysisWorkbench();
  });
})();
