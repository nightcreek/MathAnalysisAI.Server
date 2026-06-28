(function () {
  var keys = window.MathAnalysisAuthStorageKeys;
  var accessTokenMemory = "";
  var accessTokenExpiresAtMemory = "";
  var currentUser = null;
  var loaded = false;
  var loadingPromise = null;
  var authInfoCache = null;
  var authInfoPromise = null;
  var lastAuthState = null;
  var loggedStorageFailures = {};
  var loggedAuthWarnings = {};

  function logStorageFailure(action, key, err) {
    var failureKey = action + ":" + key + ":" + ((err && err.name) || "error");
    if (loggedStorageFailures[failureKey]) {
      return;
    }
    loggedStorageFailures[failureKey] = true;
    console.error("[Auth] sessionStorage " + action + " failed for key \"" + key + "\".", err);
  }

  function warnOnce(id, message) {
    if (loggedAuthWarnings[id]) {
      return;
    }
    loggedAuthWarnings[id] = true;
    console.warn(message);
  }

  function safeGetSession(key) {
    try {
      return sessionStorage.getItem(key);
    } catch (err) {
      logStorageFailure("read", key, err);
      return null;
    }
  }

  function safeSetSession(key, value) {
    try {
      sessionStorage.setItem(key, value);
      return sessionStorage.getItem(key) === value;
    } catch (err) {
      logStorageFailure("write", key, err);
      return false;
    }
  }

  function safeRemoveSession(key) {
    try {
      sessionStorage.removeItem(key);
      return true;
    } catch (err) {
      logStorageFailure("remove", key, err);
      return false;
    }
  }

  function resetCache() {
    loaded = false;
    loadingPromise = null;
  }

  function setLastAuthState(state) {
    lastAuthState = state || null;
  }

  function getStoredAccessToken() {
    var token = safeGetSession(keys.accessToken);
    if (typeof token === "string" && token.length > 0) {
      accessTokenMemory = token;
      return token;
    }

    if (accessTokenMemory) {
      warnOnce("access-token-memory-fallback", "[Auth] sessionStorage token missing, falling back to in-memory token cache.");
    }

    return accessTokenMemory || "";
  }

  function getStoredAccessTokenExpiry() {
    var expiresAt = safeGetSession(keys.accessTokenExpiresAt);
    if (typeof expiresAt === "string" && expiresAt.length > 0) {
      accessTokenExpiresAtMemory = expiresAt;
      return expiresAt;
    }

    return accessTokenExpiresAtMemory || "";
  }

  function getAccessToken() {
    var expiresAt = getStoredAccessTokenExpiry();
    if (expiresAt) {
      var expiresAtMs = Date.parse(expiresAt);
      if (Number.isFinite(expiresAtMs) && expiresAtMs <= Date.now() + 5000) {
        handleUnauthorizedResponse({
          errorCode: "AUTH_SESSION_EXPIRED",
          serverMessage: "当前登录已过期，请重新登录后继续。"
        });
        return "";
      }
    }

    return getStoredAccessToken() || "";
  }

  function hasAccessToken() {
    return !!getAccessToken();
  }

  function setAccessToken(token, expiresAtUtc) {
    var normalizedToken = String(token || "").trim();
    if (!normalizedToken) {
      console.error("[Auth] Refused to persist an empty access token.");
      return false;
    }

    accessTokenMemory = normalizedToken;
    accessTokenExpiresAtMemory = expiresAtUtc ? String(expiresAtUtc) : "";

    var tokenPersisted = safeSetSession(keys.accessToken, normalizedToken);
    var expiryPersisted = true;
    if (accessTokenExpiresAtMemory) {
      expiryPersisted = safeSetSession(keys.accessTokenExpiresAt, accessTokenExpiresAtMemory);
    } else {
      safeRemoveSession(keys.accessTokenExpiresAt);
    }

    if (!tokenPersisted || !expiryPersisted) {
      console.error("[Auth] Access token persistence verification failed.", {
        tokenPersisted: tokenPersisted,
        expiryPersisted: expiryPersisted
      });
    }

    setLastAuthState(null);
    resetCache();
    return tokenPersisted && expiryPersisted;
  }

  function clearAccessToken() {
    accessTokenMemory = "";
    accessTokenExpiresAtMemory = "";
    safeRemoveSession(keys.accessToken);
    safeRemoveSession(keys.accessTokenExpiresAt);
    currentUser = null;
    resetCache();
  }

  function clearOidcTempState() {
    safeRemoveSession(keys.oidcState);
    safeRemoveSession(keys.oidcVerifier);
    safeRemoveSession(keys.oidcRedirectPath);
  }

  function getImpersonatedRole() {
    return safeGetSession(keys.impersonatedRole) || "";
  }

  function setImpersonatedRole(role) {
    if (!role) {
      safeRemoveSession(keys.impersonatedRole);
      return;
    }
    safeSetSession(keys.impersonatedRole, String(role));
  }

  function clearImpersonatedRole() {
    safeRemoveSession(keys.impersonatedRole);
  }

  async function loadAuthInfo(forceReload) {
    if (forceReload === true) {
      authInfoCache = null;
      authInfoPromise = null;
    }
    if (authInfoCache) {
      return authInfoCache;
    }
    if (authInfoPromise) {
      return authInfoPromise;
    }

    authInfoPromise = window.BackendApi.auth.getInfo()
      .then(function (info) {
        authInfoCache = info || null;
        authInfoPromise = null;
        return authInfoCache;
      })
      .catch(function (err) {
        authInfoPromise = null;
        throw err;
      });

    return authInfoPromise;
  }

  function getCurrentUser() {
    if (!currentUser) return null;

    var viewUser = Object.assign({}, currentUser);
    var impersonatedRole = getImpersonatedRole();
    if (impersonatedRole && String(currentUser.role || "").toLowerCase() === "admin") {
      viewUser.impersonatedRole = impersonatedRole;
      viewUser.role = impersonatedRole;
    } else {
      viewUser.impersonatedRole = impersonatedRole || currentUser.impersonatedRole || null;
    }
    return viewUser;
  }

  function getActualCurrentUser() {
    return currentUser;
  }

  async function loadCurrentUser(forceReload) {
    if (forceReload === true) {
      resetCache();
    }

    if (!hasAccessToken()) {
      currentUser = null;
      loaded = true;
      return null;
    }

    if (loaded) {
      return getCurrentUser();
    }

    if (loadingPromise) {
      return loadingPromise;
    }

    loadingPromise = (async function () {
      try {
        var user = await window.BackendApi.auth.getCurrentUser();
        currentUser = user || null;
        setLastAuthState(null);
      } catch (err) {
        currentUser = null;
        if (err && err.isAuthRequired) {
          handleUnauthorizedResponse(err);
        } else {
          setLastAuthState({
            type: "error",
            message: UI && UI.formatApiErrorMessage ? UI.formatApiErrorMessage(err, "auth") : "认证状态加载失败。",
            traceId: err && err.traceId ? err.traceId : ""
          });
        }
      } finally {
        loaded = true;
        loadingPromise = null;
      }
      return getCurrentUser();
    })();

    return loadingPromise;
  }

  function handleUnauthorizedResponse(err) {
    clearAccessToken();
    setLastAuthState({
      type: "expired",
      message: (err && err.serverMessage) || "当前登录已过期，请重新登录后继续。",
      traceId: err && err.traceId ? err.traceId : ""
    });
  }

  function getCurrentUserId() {
    if (!currentUser || currentUser.userId == null) return null;
    var id = Number(currentUser.userId);
    return Number.isFinite(id) && id > 0 ? id : null;
  }

  function isAuthenticated() {
    return getCurrentUserId() != null;
  }

  function hasRole(role) {
    if (!role) return false;
    var user = getCurrentUser();
    if (!user || !user.role) return false;
    return String(user.role).toLowerCase() === String(role).toLowerCase();
  }

  function hasAnyRole(roles) {
    if (!Array.isArray(roles) || roles.length === 0) return false;
    return roles.some(hasRole);
  }

  async function requireAnyRole(roles, options) {
    await loadCurrentUser();
    var allowed = hasAnyRole(roles);
    return {
      allowed: allowed,
      user: getCurrentUser(),
      reason: allowed ? null : ((options && options.reason) || "forbidden")
    };
  }

  function buildAbsoluteUrl(path) {
    if (!path) return window.location.origin;
    if (/^https?:\/\//i.test(path)) return path;
    return window.location.origin + path;
  }

  function toBase64Url(bytes) {
    var str = btoa(String.fromCharCode.apply(null, Array.from(bytes)));
    return str.replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/g, "");
  }

  async function sha256Base64Url(value) {
    var encoder = new TextEncoder();
    var data = encoder.encode(value);
    var digest = await crypto.subtle.digest("SHA-256", data);
    return toBase64Url(new Uint8Array(digest));
  }

  function randomString(size) {
    var bytes = new Uint8Array(size);
    crypto.getRandomValues(bytes);
    return toBase64Url(bytes);
  }

  async function fetchOidcDiscovery(authority) {
    var normalizedAuthority = String(authority || "").replace(/\/+$/, "");
    var response = await fetch(normalizedAuthority + "/.well-known/openid-configuration");
    if (!response.ok) {
      throw new Error("Failed to load OIDC discovery document.");
    }
    return response.json();
  }

  async function beginOidcLogin() {
    var info = await loadAuthInfo();
    if (!info || !info.oidc || !info.oidc.authority || !info.oidc.clientId) {
      throw new Error("OIDC configuration is incomplete.");
    }

    var discovery = await fetchOidcDiscovery(info.oidc.authority);
    if (!discovery.authorization_endpoint) {
      throw new Error("OIDC discovery document is missing authorization_endpoint.");
    }

    var state = randomString(32);
    var verifier = randomString(48);
    var challenge = await sha256Base64Url(verifier);
    var redirectPath = info.oidc.redirectPath || "/login-callback.html";
    var redirectUri = buildAbsoluteUrl(redirectPath);
    var scopes = Array.isArray(info.oidc.scopes) && info.oidc.scopes.length
      ? info.oidc.scopes.join(" ")
      : "openid profile email";

    safeSetSession(keys.oidcState, state);
    safeSetSession(keys.oidcVerifier, verifier);
    safeSetSession(keys.oidcRedirectPath, redirectPath);

    var params = new URLSearchParams();
    params.set("response_type", "code");
    params.set("client_id", info.oidc.clientId);
    params.set("redirect_uri", redirectUri);
    params.set("scope", scopes);
    params.set("state", state);
    params.set("code_challenge", challenge);
    params.set("code_challenge_method", "S256");

    window.location.assign(discovery.authorization_endpoint + "?" + params.toString());
  }

  async function handleOidcCallback() {
    var params = new URLSearchParams(window.location.search);
    var error = params.get("error");
    if (error) {
      throw new Error(params.get("error_description") || error);
    }

    var code = params.get("code");
    var state = params.get("state");
    var storedState = safeGetSession(keys.oidcState);
    var verifier = safeGetSession(keys.oidcVerifier);
    var redirectPath = safeGetSession(keys.oidcRedirectPath) || "/login-callback.html";

    if (!code || !state || !storedState || state !== storedState || !verifier) {
      clearOidcTempState();
      throw new Error("OIDC callback state validation failed.");
    }

    var info = await loadAuthInfo();
    if (!info || !info.oidc || !info.oidc.authority || !info.oidc.clientId) {
      clearOidcTempState();
      throw new Error("OIDC configuration is incomplete.");
    }

    var discovery = await fetchOidcDiscovery(info.oidc.authority);
    if (!discovery.token_endpoint) {
      clearOidcTempState();
      throw new Error("OIDC discovery document is missing token_endpoint.");
    }

    var body = new URLSearchParams();
    body.set("grant_type", "authorization_code");
    body.set("client_id", info.oidc.clientId);
    body.set("code", code);
    body.set("redirect_uri", buildAbsoluteUrl(redirectPath));
    body.set("code_verifier", verifier);

    var tokenResponse = await fetch(discovery.token_endpoint, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: body.toString()
    });

    var tokenPayload = await tokenResponse.json().catch(function () { return null; });
    if (!tokenResponse.ok || !tokenPayload || !tokenPayload.access_token) {
      clearOidcTempState();
      throw new Error((tokenPayload && tokenPayload.error_description) || "Failed to exchange authorization code.");
    }

    var expiresIn = Number(tokenPayload.expires_in || 3600);
    var expiresAtUtc = new Date(Date.now() + Math.max(expiresIn, 60) * 1000).toISOString();
    if (!setAccessToken(tokenPayload.access_token, expiresAtUtc)) {
      clearOidcTempState();
      throw new Error("Failed to persist access token in sessionStorage.");
    }
    clearOidcTempState();

    await loadCurrentUser(true);
    if (!currentUser) {
      throw new Error("OIDC login succeeded but current user bootstrap failed.");
    }

    return getCurrentUser();
  }

  async function logout() {
    clearImpersonatedRole();
    clearAccessToken();
    setLastAuthState(null);
  }

  async function beginLogin() {
    var info = await loadAuthInfo();
    if (info && String(info.mode || "").toLowerCase() === "oidc") {
      return beginOidcLogin();
    }
    throw new Error("Current auth mode does not use OIDC.");
  }

  window.Auth = {
    beginLogin: beginLogin,
    beginOidcLogin: beginOidcLogin,
    clearAccessToken: clearAccessToken,
    clearImpersonatedRole: clearImpersonatedRole,
    getAccessToken: getAccessToken,
    getActualCurrentUser: getActualCurrentUser,
    getCurrentUser: getCurrentUser,
    getCurrentUserId: getCurrentUserId,
    getImpersonatedRole: getImpersonatedRole,
    getLastAuthState: function () { return lastAuthState; },
    handleOidcCallback: handleOidcCallback,
    handleUnauthorizedResponse: handleUnauthorizedResponse,
    hasAccessToken: hasAccessToken,
    hasAnyRole: hasAnyRole,
    hasRole: hasRole,
    isAuthenticated: isAuthenticated,
    loadAuthInfo: loadAuthInfo,
    loadCurrentUser: loadCurrentUser,
    logout: logout,
    requireAnyRole: requireAnyRole,
    resetCache: resetCache,
    setAccessToken: setAccessToken,
    setImpersonatedRole: setImpersonatedRole
  };
})();
