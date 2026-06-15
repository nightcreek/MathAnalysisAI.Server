window.Api = (function () {
  function enrichError(res, data) {
    var err = new Error("HTTP " + res.status);
    err.status = res.status;
    err.data = data;
    err.errorCode = data && data.errorCode ? String(data.errorCode) : "";
    err.isRetryable = !!(data && data.isRetryable);
    err.attemptCount = data && Number.isFinite(Number(data.attemptCount)) ? Number(data.attemptCount) : null;
    err.statusCode = data && Number.isFinite(Number(data.statusCode)) ? Number(data.statusCode) : res.status;
    err.serverMessage = data && (data.message || data.title) ? String(data.message || data.title) : "";
    err.isAuthRequired = res.status === 401;
    err.isForbidden = res.status === 403;
    err.isConflict = res.status === 409;

    // 429 Rate Limited — extract backend message + retryAfter for friendly UI display
    if (res.status === 429) {
      err.isRateLimited = true;
      err.rateLimitMessage = (data && data.message) || "请求过于频繁，请稍后重试。";
      var ra = (data && data.retryAfter) ? Number(data.retryAfter) : null;
      if (ra && Number.isFinite(ra) && ra > 0) {
        err.retryAfter = ra;
      }
    }

    return err;
  }

  async function getJson(url) {
    var res = await fetch(url);
    var text = await res.text();
    var data = null;
    try { data = text ? JSON.parse(text) : null; } catch (_) { data = null; }
    if (!res.ok) { throw enrichError(res, data); }
    return data;
  }

  async function postJson(url, payload) {
    var res = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    var text = await res.text();
    var data = null;
    try { data = text ? JSON.parse(text) : null; } catch (_) { data = null; }
    if (!res.ok) { throw enrichError(res, data); }
    return data;
  }

  async function postFormData(url, formData) {
    var res = await fetch(url, { method: "POST", body: formData });
    var text = await res.text();
    var data = null;
    try { data = text ? JSON.parse(text) : null; } catch (_) { data = null; }
    if (!res.ok) { throw enrichError(res, data); }
    return data;
  }

  return { getJson: getJson, postJson: postJson, postFormData: postFormData };
})();
