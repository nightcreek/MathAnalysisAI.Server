function isEscapedMathDelimiter(text, index) {
  var slashCount = 0;
  for (var i = index - 1; i >= 0 && text.charAt(i) === "\\"; i--) {
    slashCount += 1;
  }
  return slashCount % 2 === 1;
}

function findClosingMathDelimiter(text, startIndex, openDelimiter, closeDelimiter, options) {
  var cursor = startIndex + openDelimiter.length;
  var opts = options || {};

  while (cursor < text.length) {
    var endIndex = text.indexOf(closeDelimiter, cursor);
    if (endIndex < 0) {
      return -1;
    }

    if (isEscapedMathDelimiter(text, endIndex)) {
      cursor = endIndex + closeDelimiter.length;
      continue;
    }

    var innerText = text.slice(startIndex + openDelimiter.length, endIndex);
    if (opts.singleLine && /[\r\n]/.test(innerText)) {
      cursor = endIndex + closeDelimiter.length;
      continue;
    }

    if (opts.requireContent && !innerText.trim()) {
      cursor = endIndex + closeDelimiter.length;
      continue;
    }

    return endIndex;
  }

  return -1;
}

function findNextMathToken(text, startIndex) {
  var delimiters = [
    { open: "$$", close: "$$", display: true, singleLine: false },
    { open: "\\[", close: "\\]", display: true, singleLine: false },
    { open: "\\(", close: "\\)", display: false, singleLine: false },
    { open: "$", close: "$", display: false, singleLine: true }
  ];

  var candidates = [];

  delimiters.forEach(function (delimiter) {
    var searchFrom = startIndex;
    while (searchFrom < text.length) {
      var openIndex = text.indexOf(delimiter.open, searchFrom);
      if (openIndex < 0) {
        return;
      }

      if (delimiter.open === "$" && text.charAt(openIndex + 1) === "$") {
        searchFrom = openIndex + 2;
        continue;
      }

      if (isEscapedMathDelimiter(text, openIndex)) {
        searchFrom = openIndex + delimiter.open.length;
        continue;
      }

      var closeIndex = findClosingMathDelimiter(text, openIndex, delimiter.open, delimiter.close, {
        requireContent: true,
        singleLine: delimiter.singleLine
      });

      if (closeIndex < 0) {
        searchFrom = openIndex + delimiter.open.length;
        continue;
      }

      candidates.push({
        type: "math",
        display: delimiter.display,
        start: openIndex,
        end: closeIndex + delimiter.close.length,
        value: text.slice(openIndex, closeIndex + delimiter.close.length)
      });
      return;
    }
  });

  if (!candidates.length) {
    return null;
  }

  candidates.sort(function (a, b) {
    if (a.start !== b.start) {
      return a.start - b.start;
    }
    return (b.end - b.start) - (a.end - a.start);
  });

  return candidates[0];
}

function extractMathTokens(text) {
  var source = String(text || "");
  if (!source) {
    return [];
  }

  var tokens = [];
  var cursor = 0;

  while (cursor < source.length) {
    var token = findNextMathToken(source, cursor);
    if (!token) {
      tokens.push({ type: "text", value: source.slice(cursor) });
      break;
    }

    if (token.start > cursor) {
      tokens.push({ type: "text", value: source.slice(cursor, token.start) });
    }

    tokens.push(token);
    cursor = token.end;
  }

  return tokens;
}

function renderPlainTextWithBreaks(text) {
  return window.UI.escapeHtml(String(text || "").replace(/\r\n?/g, "\n")).replace(/\n/g, "<br>");
}

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
  extractMathSegments(text) {
    return extractMathTokens(text);
  },
  containsMathSyntax(text) {
    return extractMathTokens(text).some(function (token) { return token.type === "math"; });
  },
  renderMixedMarkdownMath(text) {
    var tokens = extractMathTokens(text);
    if (!tokens.length) {
      return "";
    }

    return tokens.map(function (token) {
      if (token.type !== "math") {
        return renderPlainTextWithBreaks(token.value);
      }

      var className = token.display ? "math-block-fragment" : "math-inline-fragment";
      return "<span class='" + className + "'>" + window.UI.escapeHtml(token.value) + "</span>";
    }).join("");
  },
  renderMathList(items) {
    const arr = this.safeList(items).map(x => String(x || "").trim()).filter(Boolean);
    if (!arr.length) return "<div class='status'>暂无</div>";
    return "<ul class='list math-list'>" + arr.map(x =>
      "<li><div class='math-rich-text'>" + this.renderMixedMarkdownMath(x) + "</div></li>"
    ).join("") + "</ul>";
  },
  async renderMathInElement(element) {
    if (!element || !window.MathJax || typeof window.MathJax.typesetPromise !== "function") {
      return;
    }

    var renderKey = element.innerHTML;
    if (element.__mathRenderKey === renderKey) {
      return;
    }

    try {
      if (typeof window.MathJax.typesetClear === "function") {
        window.MathJax.typesetClear([element]);
      }
      await window.MathJax.typesetPromise([element]);
      element.__mathRenderKey = renderKey;
    } catch (error) {
      console.warn("MathJax render failed", error);
    }
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
