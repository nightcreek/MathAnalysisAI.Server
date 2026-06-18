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
  renderErrorPanel(target, options) {
    var el = typeof target === "string" ? document.querySelector(target) : target;
    if (!el) return;

    var opts = options || {};
    var title = this.escapeHtml(opts.title || "系统暂时不可用");
    var message = this.escapeHtml(opts.message || "请稍后重试。");
    var actionId = opts.actionLabel ? "error-panel-action-" + Date.now() + "-" + Math.floor(Math.random() * 1000) : "";

    el.className = opts.containerClassName || "";
    el.innerHTML =
      "<div class='result-section'>" +
      "<div class='section-title'>" + title + "</div>" +
      "<div class='status error'>" + message + "</div>" +
      (opts.traceId ? "<div class='hint'>TraceId: " + this.escapeHtml(opts.traceId) + "</div>" : "") +
      (opts.actionLabel
        ? "<div class='actions'><button type='button' class='btn-secondary' id='" + actionId + "'>" + this.escapeHtml(opts.actionLabel) + "</button></div>"
        : "") +
      "</div>";

    if (actionId && typeof opts.onAction === "function") {
      var btn = document.getElementById(actionId);
      if (btn) {
        btn.addEventListener("click", opts.onAction);
      }
    }
  },
  renderBootstrapError(target, message, onRetry, traceId) {
    this.renderErrorPanel(target, {
      title: "系统加载失败",
      message: message || "基础数据加载失败，请稍后重试。",
      traceId: traceId || "",
      actionLabel: onRetry ? "重试" : "",
      onAction: onRetry
    });
  },
  renderLoginRequired(target, message, onLogin) {
    this.renderErrorPanel(target, {
      title: "需要重新登录",
      message: message || "当前登录已失效，请重新登录后继续。",
      actionLabel: onLogin ? "重新登录" : "",
      onAction: onLogin
    });
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

    var errorCode = err && err.errorCode ? String(err.errorCode).toUpperCase() : "";
    var status = err && Number.isFinite(Number(err.status)) ? Number(err.status) : null;
    var serverMessage = err && err.serverMessage ? String(err.serverMessage) : "";
    var normalizedContext = String(context || "default").toLowerCase();

    var errorMap = {
      AUTH_NOT_LOGGED_IN: "当前未登录，请先登录后再继续。",
      AUTH_SESSION_EXPIRED: "当前登录已过期，请重新登录后再继续。",
      AUTH_INVALID_CREDENTIALS: "用户名或密码错误。",
      AUTH_USERNAME_REQUIRED: "请输入用户名。",
      AUTH_PASSWORD_REQUIRED: "请输入密码。",
      AUTH_MODE_DISABLED: "当前部署未启用登录入口，请联系管理员。",
      AUTH_MODE_OIDC_REQUIRED: "当前部署要求统一认证登录，请使用统一认证入口。",
      AUTH_MODE_UNAVAILABLE: "当前部署未启用该登录入口。",
      DATABASE_UNAVAILABLE: "数据库暂时不可用或尚未完成初始化，请稍后重试。",
      DEPENDENCY_UNAVAILABLE: "依赖服务暂时不可用，请稍后重试。",
      INTERNAL_SERVER_ERROR: "系统发生了未预期错误，请稍后重试。"
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
        ? "当前登录不可用，请检查认证配置。"
        : "当前登录已失效，请重新登录后继续。";
    }

    if (status === 403) {
      return "当前账号没有权限执行此操作。";
    }

    if (status === 404 && normalizedContext === "analysis") {
      return "关联的 OCR 记录不存在，请重新上传并确认后再分析。";
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
