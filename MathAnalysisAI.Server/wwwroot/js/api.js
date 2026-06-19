window.MathAnalysisAuthStorageKeys = window.MathAnalysisAuthStorageKeys || {
  accessToken: "math_analysis_access_token",
  accessTokenExpiresAt: "math_analysis_access_token_expires_at",
  impersonatedRole: "math_analysis_impersonated_role",
  oidcState: "math_analysis_oidc_state",
  oidcVerifier: "math_analysis_oidc_verifier",
  oidcRedirectPath: "math_analysis_oidc_redirect_path"
};

window.Api = (function () {
  function readAccessToken() {
    var token = "";
    try {
      if (window.Auth && typeof window.Auth.getAccessToken === "function") {
        token = window.Auth.getAccessToken() || "";
      }
    } catch (_) {
      token = "";
    }

    if (!token && window.MathAnalysisAuthStorageKeys) {
      try {
        token = sessionStorage.getItem(window.MathAnalysisAuthStorageKeys.accessToken) || "";
      } catch (_) {
        token = "";
      }
    }

    return token;
  }

  function buildHeaders(extraHeaders, contentType) {
    var headers = new Headers(extraHeaders || {});
    var token = String(readAccessToken() || "").trim();
    if (token && token.length > 10 && !headers.has("Authorization")) {
      headers.set("Authorization", "Bearer " + token);
    }
    if (contentType && !headers.has("Content-Type")) {
      headers.set("Content-Type", contentType);
    }
    return headers;
  }

  function enrichError(res, data, meta) {
    var err = new Error("HTTP " + res.status);
    err.status = res.status;
    err.data = data;
    err.traceId = meta && meta.traceId ? meta.traceId : "";
    err.errorCode = data && data.errorCode ? String(data.errorCode) : "";
    err.isRetryable = !!(data && data.isRetryable);
    err.serverMessage = data && data.message ? String(data.message) : "";
    err.isAuthRequired = res.status === 401;
    err.isForbidden = res.status === 403;
    err.isConflict = res.status === 409;
    err.isRateLimited = res.status === 429;
    err.isServiceUnavailable = res.status === 503;

    if (res.status === 429) {
      err.rateLimitMessage = (data && data.message) || "请求过于频繁，请稍后重试。";
      var retryAfter = data && data.retryAfter ? Number(data.retryAfter) : null;
      if (retryAfter && Number.isFinite(retryAfter) && retryAfter > 0) {
        err.retryAfter = retryAfter;
      }
    }

    return err;
  }

  async function parseResponse(res) {
    var text = await res.text();
    var data = null;

    try {
      data = text ? JSON.parse(text) : null;
    } catch (_) {
      data = null;
    }

    var meta = {
      degraded: res.headers.get("X-Degraded-Response") === "true",
      traceId: res.headers.get("X-Trace-Id") || "",
      status: res.status
    };

    if (!res.ok) {
      var err = enrichError(res, data, meta);
      if (err.isAuthRequired && window.Auth && typeof window.Auth.handleUnauthorizedResponse === "function") {
        window.Auth.handleUnauthorizedResponse(err);
      }
      throw err;
    }

    return { data: data, meta: meta };
  }

  async function requestDetailed(url, init) {
    var requestInit = Object.assign({}, init || {});
    requestInit.headers = buildHeaders(requestInit.headers, null);
    var res = await fetch(url, requestInit);
    return parseResponse(res);
  }

  async function requestJsonDetailed(method, url, payload) {
    return requestDetailed(url, {
      method: method,
      headers: buildHeaders(null, "application/json"),
      body: payload == null ? null : JSON.stringify(payload)
    });
  }

  async function requestFormDataDetailed(url, formData) {
    return requestDetailed(url, {
      method: "POST",
      headers: buildHeaders(null, null),
      body: formData
    });
  }

  async function getJson(url) {
    var result = await requestDetailed(url, { method: "GET" });
    return result.data;
  }

  async function getJsonDetailed(url) {
    return requestDetailed(url, { method: "GET" });
  }

  async function postJson(url, payload) {
    var result = await requestJsonDetailed("POST", url, payload);
    return result.data;
  }

  async function postJsonDetailed(url, payload) {
    return requestJsonDetailed("POST", url, payload);
  }

  async function putJson(url, payload) {
    var result = await requestJsonDetailed("PUT", url, payload);
    return result.data;
  }

  async function putJsonDetailed(url, payload) {
    return requestJsonDetailed("PUT", url, payload);
  }

  async function postFormData(url, formData) {
    var result = await requestFormDataDetailed(url, formData);
    return result.data;
  }

  async function postFormDataDetailed(url, formData) {
    return requestFormDataDetailed(url, formData);
  }

  async function del(url) {
    var result = await requestDetailed(url, { method: "DELETE" });
    return result.data;
  }

  function fetchWithAuth(url, init) {
    var requestInit = Object.assign({}, init || {});
    requestInit.headers = buildHeaders(requestInit.headers, null);
    return fetch(url, requestInit);
  }

  return {
    getJson: getJson,
    getJsonDetailed: getJsonDetailed,
    postJson: postJson,
    postJsonDetailed: postJsonDetailed,
    postFormData: postFormData,
    postFormDataDetailed: postFormDataDetailed,
    putJson: putJson,
    putJsonDetailed: putJsonDetailed,
    delete: del,
    fetchWithAuth: fetchWithAuth
  };
})();
