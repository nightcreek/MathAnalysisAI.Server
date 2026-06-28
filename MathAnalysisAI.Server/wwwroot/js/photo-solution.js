(function () {
  var state = {
    recordId: null,
    status: "",
    needsManualReview: false,
    isConfirmed: false,
    reviewReasons: [],
    latestResponse: null,
    dirty: false
  };

  function hasPhotoOcrDom() {
    return !!(
      UI.qs("#photoOcrBtn") &&
      UI.qs("#photoOcrConfirmBtn") &&
      UI.qs("#photoOcrStatus") &&
      UI.qs("#photoOcrSummary") &&
      UI.qs("#photoSolutionFile") &&
      UI.qs("#photoSolutionHint") &&
      UI.qs("#chapterSelect") &&
      UI.qs("#problemTextInput") &&
      UI.qs("#studentSolutionTextInput")
    );
  }

  function getFormulaPayload() {
    if (window.MathLiveOcr && typeof window.MathLiveOcr.getFormulas === "function") {
      return window.MathLiveOcr.getFormulas();
    }

    return [];
  }

  function normalizeList(list) {
    return UI.safeList(list).map(function (item) {
      return String(item || "").trim();
    }).filter(Boolean);
  }

  function sanitizeErrorDetail(text) {
    var normalized = String(text || "")
      .replace(/\s+/g, " ")
      .replace(/\s+at\s+.+$/i, "")
      .trim();

    if (!normalized || /^HTTP\s+\d+$/i.test(normalized)) {
      return "";
    }

    if (normalized.length > 180) {
      return normalized.slice(0, 177) + "...";
    }

    return normalized;
  }

  function extractErrorDetail(err) {
    var data = err && err.data && typeof err.data === "object" ? err.data : null;
    var candidates = [
      err && err.serverMessage,
      data && data.message,
      data && data.errorMessage,
      data && data.detail,
      data && data.error,
      err && err.message
    ];

    for (var i = 0; i < candidates.length; i++) {
      var detail = sanitizeErrorDetail(candidates[i]);
      if (detail) {
        return detail;
      }
    }

    return "";
  }

  function formatOcrRequestError(err) {
    if (err && String(err.errorCode || "").toUpperCase() === "OCR_EMPTY_RESULT") {
      return "OCR 识别失败：返回结果为空，请尝试更清晰的图片或改为手动输入。";
    }

    var status = err && Number.isFinite(Number(err.status)) ? Number(err.status) : null;
    var detail = extractErrorDetail(err);
    var messageText = detail || String((err && err.message) || "");

    if (!status) {
      if (/timeout|timed out|abort/i.test(messageText)) {
        return "OCR 识别失败：请求超时，请稍后重试。";
      }
      if (/failed to fetch|networkerror|load failed|network request failed/i.test(messageText.toLowerCase())) {
        return "OCR 识别失败：网络连接异常，请检查网络后重试。";
      }
      return detail
        ? "OCR 识别失败：" + detail
        : "OCR 识别失败：网络连接异常，请检查网络后重试。";
    }

    if (status === 413) {
      return "OCR 识别失败：服务器返回 413，图片可能过大。";
    }

    if (status === 415) {
      return "OCR 识别失败：服务器返回 415，请上传 JPG、PNG 或 WebP 图片。";
    }

    if (status === 408 || status === 504) {
      return "OCR 识别失败：服务器返回 " + status + "，请求超时，请稍后重试。";
    }

    if (status >= 500) {
      return detail
        ? "OCR 识别失败：服务器返回 " + status + "，" + detail
        : "OCR 识别失败：服务器返回 " + status + "，模型调用失败。";
    }

    if (detail) {
      return "OCR 识别失败：服务器返回 " + status + "，" + detail;
    }

    return "OCR 识别失败：服务器返回 " + status + "。";
  }

  function buildOcrErrorHint(err) {
    if (err && String(err.errorCode || "").toUpperCase() === "OCR_EMPTY_RESULT") {
      return "建议重新拍摄更清晰的题目图片，或切换到手动输入模式继续分析。";
    }

    var status = err && Number.isFinite(Number(err.status)) ? Number(err.status) : null;
    var detail = extractErrorDetail(err);

    if (status === 413) {
      return "建议压缩图片大小、裁剪无关区域后再重试。";
    }

    if (status === 415) {
      return "请确认上传的是标准图片文件，而不是 PDF、截图压缩包或其它格式。";
    }

    if (status && status >= 500 && detail) {
      return "详细信息：" + detail;
    }

    if (!status && detail) {
      return "详细信息：" + detail;
    }

    return "如果连续失败，可以改为手动输入题目和解答继续使用。";
  }

  function setAnalyzeButtonDisabled(disabled) {
    var analyzeBtn = UI.qs("#analyzeBtn");
    if (!analyzeBtn) return;
    analyzeBtn.disabled = !!disabled;
  }

  function refreshActionState() {
    var confirmBtn = UI.qs("#photoOcrConfirmBtn");
    var hasRecord = !!state.recordId;
    if (confirmBtn) {
      confirmBtn.disabled = !hasRecord || state.isConfirmed;
      confirmBtn.textContent = state.isConfirmed
        ? "OCR 已确认"
        : "确认 OCR 并允许分析";
    }

    if (!hasRecord) {
      setAnalyzeButtonDisabled(false);
      return;
    }

    setAnalyzeButtonDisabled(!state.isConfirmed);
  }

  function markDirty() {
    if (!state.recordId) {
      return;
    }

    state.dirty = true;
    state.isConfirmed = false;
    UI.showStatus(UI.qs("#photoOcrStatus"), "OCR 结果已修改，请重新确认后再分析。", true);
    refreshActionState();
  }

  function showReviewNotice(ocrResponse) {
    var notice = UI.qs("#photoOcrReviewNotice");
    if (!notice) return;

    if (!ocrResponse || typeof ocrResponse !== "object") {
      notice.style.display = "none";
      notice.textContent = "";
      return;
    }

    var warnings = normalizeList(ocrResponse.warnings);
    var reasons = normalizeList(ocrResponse.reviewReasons);
    var texts = [ocrResponse.isConfirmed === true
      ? "OCR 已确认，当前内容可以进入分析。"
      : "识别结果已回填，请检查题目、我的解答和公式。确认无误后再点击“确认 OCR 并允许分析”。"];

    if (ocrResponse.isConfirmed !== true && ocrResponse.needsManualReview === true) {
      texts.push("当前 OCR 结果需要人工复核，请优先修正题目与解答。");
      notice.className = "hint ocr-review-card ocr-warning-note";
    } else {
      notice.className = "hint ocr-review-card";
    }

    if (ocrResponse.isConfirmed !== true && warnings.indexOf("section_split_uncertain") >= 0) {
      texts.push("题目与解答分区可能不完全准确，请重点检查。");
      notice.className = "hint ocr-review-card ocr-warning-note";
    }

    if (ocrResponse.isConfirmed !== true && String(ocrResponse.studentSolutionText || "").trim() === "[unclear]") {
      texts.push("未能可靠识别我的解答，请手动补充或修改后再分析。");
      notice.className = "hint ocr-review-card ocr-warning-note";
    }

    if (ocrResponse.isConfirmed !== true && reasons.length) {
      texts.push("复核原因：" + reasons.join("，"));
    }

    notice.textContent = texts.join(" ");
    notice.style.display = "block";
  }

  function setResponseState(data) {
    state.recordId = data && data.ocrRecordId ? Number(data.ocrRecordId) : null;
    state.status = String(data && data.status ? data.status : "");
    state.needsManualReview = !!(data && data.needsManualReview);
    state.isConfirmed = !!(data && data.isConfirmed);
    state.reviewReasons = normalizeList(data && data.reviewReasons);
    state.latestResponse = data || null;
    state.dirty = false;
    refreshActionState();
  }

  function applyRecognizedPayload(data) {
    UI.qs("#problemTextInput").value = (data.problemText || "").trim();
    UI.qs("#studentSolutionTextInput").value = (data.studentSolutionText || "").trim();

    if (window.MathAnalysis && typeof window.MathAnalysis.refreshInputPreviews === "function") {
      window.MathAnalysis.refreshInputPreviews();
    }

    if (window.MathLiveOcr && window.MathLiveOcr.renderFormulas) {
      window.MathLiveOcr.renderFormulas(Array.isArray(data.formulas) ? data.formulas : [], data);
    }

    var warningCount = Array.isArray(data.warnings) ? data.warnings.length : 0;
    var formulaCount = Array.isArray(data.formulas) ? data.formulas.length : 0;
    var sectionCount = Array.isArray(data.detectedSections) ? data.detectedSections.length : 0;
    var reviewCount = Array.isArray(data.reviewReasons) ? data.reviewReasons.length : 0;

    UI.setText(
      UI.qs("#photoOcrSummary"),
      (data.isConfirmed === true ? "确认完成：" : "识别完成：") +
        "已回填题目与解答。分段 " + sectionCount + " 条，公式 " + formulaCount + " 条，警告 " + warningCount + " 条，复核原因 " + reviewCount + " 条。"
    );
    UI.showStatus(
      UI.qs("#photoOcrStatus"),
      data.isConfirmed === true
        ? "OCR 已确认，可以开始分析。"
        : (data.needsManualReview ? "识别完成，需要人工复核。" : "识别完成，请确认后再分析。"),
      false
    );
    showReviewNotice(data);
    setResponseState(data);
  }

  async function recognizePhotoSolution() {
    if (!hasPhotoOcrDom()) {
      return;
    }

    var btn = UI.qs("#photoOcrBtn");
    var status = UI.qs("#photoOcrStatus");
    var summary = UI.qs("#photoOcrSummary");
    var fileInput = UI.qs("#photoSolutionFile");
    var hintInput = UI.qs("#photoSolutionHint");

    var file = (fileInput.files && fileInput.files[0]) ? fileInput.files[0] : null;
    if (!file) {
      UI.showStatus(status, "请先选择作业图片。", true);
      return;
    }

    var chapterId = parseInt(UI.qs("#chapterSelect").value, 10);
    var form = new FormData();
    var courseId = (window.AppConfig && window.AppConfig.resolveCourseId ? window.AppConfig.resolveCourseId() : null);
    if (!courseId && window.AppConfig && window.AppConfig.getCachedCourses) {
      var courses = window.AppConfig.getCachedCourses();
      if (courses && courses.length) courseId = courses[0].id;
    }
    form.append("courseId", String(courseId || ""));
    if (!Number.isNaN(chapterId)) {
      form.append("chapterId", String(chapterId));
    } else if (AppConfig.defaultChapterId != null) {
      form.append("chapterId", String(AppConfig.defaultChapterId));
    }
    if (hintInput.value && hintInput.value.trim()) {
      form.append("userHint", hintInput.value.trim());
    }
    form.append("file", file);

    btn.disabled = true;
    UI.showStatus(status, "正在识别，请稍候……", false);
    UI.setText(summary, "");

    try {
      var data = await window.BackendApi.photoSolutions.runOcr(form);
      if (!data || typeof data !== "object") {
        var emptyError = new Error("OCR_EMPTY_RESULT");
        emptyError.errorCode = "OCR_EMPTY_RESULT";
        throw emptyError;
      }
      applyRecognizedPayload(data || {});
    } catch (err) {
      UI.showStatus(status, formatOcrRequestError(err), true);
      UI.setText(summary, buildOcrErrorHint(err));
      showReviewNotice(null);
      setResponseState({
        ocrRecordId: null,
        status: "",
        needsManualReview: false,
        isConfirmed: false,
        reviewReasons: []
      });
      if (window.MathLiveOcr && window.MathLiveOcr.renderFormulas) {
        window.MathLiveOcr.renderFormulas([], null);
      }
    } finally {
      btn.disabled = false;
    }
  }

  async function confirmPhotoSolutionOcr() {
    if (!state.recordId) {
      UI.showStatus(UI.qs("#photoOcrStatus"), "请先完成 OCR 识别。", true);
      return;
    }

    var problemText = String(UI.qs("#problemTextInput").value || "").trim();
    if (!problemText) {
      UI.showStatus(UI.qs("#photoOcrStatus"), "题目不能为空，请先修正 OCR 结果。", true);
      return;
    }

    var btn = UI.qs("#photoOcrConfirmBtn");
    btn.disabled = true;
    UI.showStatus(UI.qs("#photoOcrStatus"), "正在保存 OCR 确认结果……", false);

    try {
      var payload = {
        problemText: problemText,
        studentSolutionText: String(UI.qs("#studentSolutionTextInput").value || "").trim() || null,
        formulas: getFormulaPayload()
      };

      var data = await window.BackendApi.photoSolutions.confirmOcr(state.recordId, payload);
      applyRecognizedPayload(data || {});
      state.isConfirmed = true;
      state.dirty = false;
      UI.showStatus(UI.qs("#photoOcrStatus"), "OCR 已确认，可以开始分析。", false);
    } catch (err) {
      UI.showStatus(UI.qs("#photoOcrStatus"), UI.formatApiErrorMessage(err, "ocrConfirm"), true);
    } finally {
      refreshActionState();
    }
  }

  function bindDirtyListeners() {
    var problemText = UI.qs("#problemTextInput");
    var solutionText = UI.qs("#studentSolutionTextInput");
    if (problemText) {
      problemText.addEventListener("input", markDirty);
    }
    if (solutionText) {
      solutionText.addEventListener("input", markDirty);
    }

    document.addEventListener("photo-solution-ocr-changed", markDirty);
  }

  window.PhotoSolutionOcr = {
    getRecordId: function () {
      return state.recordId;
    },
    isConfirmed: function () {
      return !!state.isConfirmed;
    },
    hasRecord: function () {
      return !!state.recordId;
    },
    isDirty: function () {
      return !!state.dirty;
    },
    getState: function () {
      return {
        recordId: state.recordId,
        status: state.status,
        needsManualReview: state.needsManualReview,
        isConfirmed: state.isConfirmed,
        dirty: state.dirty,
        reviewReasons: state.reviewReasons.slice()
      };
    }
  };

  window.recognizePhotoSolution = recognizePhotoSolution;
  window.confirmPhotoSolutionOcr = confirmPhotoSolutionOcr;

  function bindOcrEvents() {
    var photoOcrBtn = UI.qs("#photoOcrBtn");
    var photoOcrConfirmBtn = UI.qs("#photoOcrConfirmBtn");

    if (photoOcrBtn) {
      photoOcrBtn.addEventListener("click", recognizePhotoSolution);
    }
    if (photoOcrConfirmBtn) {
      photoOcrConfirmBtn.addEventListener("click", confirmPhotoSolutionOcr);
    }
  }

  document.addEventListener("DOMContentLoaded", function () {
    bindDirtyListeners();
    bindOcrEvents();
    refreshActionState();
  });
})();
