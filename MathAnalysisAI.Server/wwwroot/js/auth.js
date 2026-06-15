(function () {
  let currentUser = null;
  let loaded = false;
  let loadingPromise = null;
  let fallbackApplied = false;

  function resetCache() {
    loaded = false;
    loadingPromise = null;
  }

  async function loadCurrentUser(forceReload) {
    if (forceReload === true) {
      resetCache();
    }
    if (loaded) {
      return currentUser;
    }
    if (loadingPromise) {
      return loadingPromise;
    }

    loadingPromise = (async function () {
      try {
        const user = await Api.getJson("/api/auth/me");
        currentUser = user || null;
        fallbackApplied = false;
      } catch (err) {
        currentUser = null;
        fallbackApplied = false;
      } finally {
        loaded = true;
        loadingPromise = null;
      }
      return currentUser;
    })();

    return loadingPromise;
  }

  function getCurrentUser() {
    return currentUser;
  }

  function getCurrentUserId() {
    if (!currentUser || currentUser.userId == null) return null;
    const id = Number(currentUser.userId);
    return Number.isFinite(id) && id > 0 ? id : null;
  }

  function isAuthenticated() {
    return getCurrentUserId() != null;
  }

  function isDevelopmentFallbackApplied() {
    return fallbackApplied;
  }

  function hasRole(role) {
    if (!role) return false;
    if (!currentUser || !currentUser.role) return false;
    return String(currentUser.role).toLowerCase() === String(role).toLowerCase();
  }

  function hasAnyRole(roles) {
    if (!Array.isArray(roles) || roles.length === 0) return false;
    return roles.some(hasRole);
  }

  async function requireAnyRole(roles, options) {
    await loadCurrentUser();
    const allowed = hasAnyRole(roles);
    return {
      allowed,
      user: currentUser,
      reason: allowed ? null : ((options && options.reason) || "forbidden")
    };
  }

  window.Auth = {
    loadCurrentUser,
    resetCache,
    getCurrentUser,
    getCurrentUserId,
    isAuthenticated,
    isDevelopmentFallbackApplied,
    hasRole,
    hasAnyRole,
    requireAnyRole
  };
})();
