window.UI = {
  qs(selector) { return document.querySelector(selector); },
  qsa(selector) { return Array.from(document.querySelectorAll(selector)); },
  setText(el, text) { if (el) el.textContent = text == null ? "" : String(text); },
  showStatus(el, text, isError) {
    if (!el) return;
    el.className = isError ? "status error" : "status";
    el.textContent = text || "";
  },
  escapeHtml(str) {
    return String(str ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");
  },
  safeList(v) {
    if (!v) return [];
    return Array.isArray(v) ? v : [String(v)];
  },
  renderList(items) {
    const arr = this.safeList(items);
    if (!arr.length) return "<div class='status'>暂无</div>";
    return "<ul class='list'>" + arr.map(x => "<li>" + this.escapeHtml(x) + "</li>").join("") + "</ul>";
  },
  formatRateLimitMessage(err) {
    var msg = (err && err.rateLimitMessage) || "请求过于频繁，请稍后重试。";
    var ra = (err && err.retryAfter) ? Number(err.retryAfter) : null;
    if (ra && Number.isFinite(ra) && ra > 0) {
      return msg + " 请约 " + ra + " 秒后重试。";
    }
    return msg;
  },
  formatApiErrorMessage(err, context) {
    if (err && err.isRateLimited) {
      return this.formatRateLimitMessage(err);
    }

    var errorCode = err && err.errorCode ? String(err.errorCode) : "";
    var status = err && Number.isFinite(Number(err.status)) ? Number(err.status) : null;
    var serverMessage = err && err.serverMessage ? String(err.serverMessage) : "";
    var normalizedContext = String(context || "default").toLowerCase();

    var errorMap = {
      auth_not_logged_in: "当前未登录，请先登录后再继续。",
      auth_invalid_username: "用户名不存在或当前不可用。",
      auth_username_required: "请输入用户名。",
      auth_mode_disabled: "当前部署未启用登录入口，请联系管理员。",
      auth_mode_local_password_not_available: "当前部署要求密码登录，但服务器尚未启用该登录入口。",
      auth_mode_oidc_not_available: "当前部署要求统一认证登录，请联系管理员接入 OIDC 登录入口。",
      auth_mode_unavailable: "当前部署未启用开发期用户名登录。",
      llm_timeout: "模型分析超时，请稍后重试。",
      llm_temporary_unavailable: "模型服务暂时不可用，请稍后重试。",
      missing_base_url: "模型服务尚未完成配置，请联系管理员。",
      missing_api_key: "模型服务尚未完成配置，请联系管理员。",
      invalid_api_key: "模型服务配置无效，请联系管理员。",
      missing_litellm_base_url: "模型服务尚未完成配置，请联系管理员。",
      missing_litellm_api_key: "模型服务尚未完成配置，请联系管理员。",
      invalid_litellm_api_key: "模型服务配置无效，请联系管理员。",
      response_parse_failed: "模型返回格式异常，本次分析未能可靠生成。",
      litellm_response_parse_failed: "模型返回格式异常，本次分析未能可靠生成。",
      request_canceled: "本次请求已取消，请重新发起。",
      ocr_timeout: "OCR 识别超时，请稍后重试或改为手动输入。",
      ocr_temporary_unavailable: "OCR 服务暂时不可用，请稍后重试或改为手动输入。",
      ocr_config_error: "OCR 服务尚未完成配置，请联系管理员或改为手动输入。",
      ocr_response_parse_failed: "OCR 返回格式异常，请手动检查后重试。",
      ocr_provider_failure: "OCR 服务调用失败，请稍后重试或改为手动输入。"
    };

    if (errorCode && errorMap[errorCode]) {
      return errorMap[errorCode];
    }

    if (normalizedContext === "analysis" && status === 409) {
      if (serverMessage && /ocr record is not confirmed yet/i.test(serverMessage)) {
        return "OCR 结果尚未确认，请先完成 OCR 确认后再分析。";
      }
      if (serverMessage && /ocr confirmation snapshot is incomplete/i.test(serverMessage)) {
        return "OCR 确认快照不完整，请先重新确认 OCR 结果。";
      }
      if (serverMessage && /ocr confirmation snapshot is missing formulas/i.test(serverMessage)) {
        return "OCR 公式确认快照缺失，请先重新确认 OCR 结果。";
      }
    }

    if (status === 401) {
      return normalizedContext === "login"
        ? "当前登录不可用，请检查认证配置或联系管理员。"
        : "当前未登录，请先登录后再继续。";
    }

    if (status === 403) {
      return "当前账号没有权限执行此操作。";
    }

    if (status === 404 && normalizedContext === "analysis") {
      return "关联的 OCR 记录不存在，请重新上传并确认后再分析。";
    }

    if (status === 502) {
      return normalizedContext === "ocr"
        ? "OCR 服务暂时不可用，请稍后重试或改为手动输入。"
        : "模型服务暂时不可用，请稍后重试。";
    }

    if (status === 503) {
      return serverMessage || "当前服务暂时不可用，请稍后重试。";
    }

    if (serverMessage) {
      return serverMessage;
    }

    if (normalizedContext === "ocr") {
      return "OCR 识别失败，请稍后重试或改为手动输入。";
    }
    if (normalizedContext === "ocrconfirm") {
      return "OCR 确认失败，请稍后重试。";
    }
    if (normalizedContext === "analysis") {
      return "分析失败，请稍后重试。";
    }
    if (normalizedContext === "login") {
      return "登录失败，请稍后重试。";
    }
    return "操作失败，请稍后重试。";
  },
  formatAnalysisMainIssue(value) {
    var text = String(value || "").trim();
    if (!text) {
      return "";
    }

    var mappings = [
      [/^LLM failed:\s*llm_timeout\b/i, "模型分析超时，本次结果未能稳定生成。"],
      [/^LLM failed:\s*llm_temporary_unavailable\b/i, "模型服务暂时不可用，本次结果未能稳定生成。"],
      [/^LLM failed:\s*(missing_|invalid_).*\b/i, "模型服务配置存在问题，本次结果未能生成。"],
      [/^JSON parse failed:/i, "模型返回格式异常，本次结果已降级处理，请优先人工复核。"],
      [/^llm_schema_invalid:/i, "模型返回结构不完整，本次结果已降级处理，请优先人工复核。"]
    ];

    for (var i = 0; i < mappings.length; i++) {
      if (mappings[i][0].test(text)) {
        return mappings[i][1];
      }
    }

    return text;
  },
  toJudgementText(v) {
    if (v === true) return "基本正确";
    if (v === false) return "存在问题";
    return "暂无法确定";
  }
};

document.addEventListener("DOMContentLoaded", function () {
  if (window.loadLeaderboard && UI.qs("#leaderboardContainer")) {
    window.loadLeaderboard();
  }
});
