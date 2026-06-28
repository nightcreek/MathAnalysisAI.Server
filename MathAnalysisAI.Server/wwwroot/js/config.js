(function () {
  var AppConfig = window.AppConfig || {};
  var _courseCache = null;
  var _courseCachePromise = null;
  var _courseLoadState = { status: "idle", message: "", traceId: "" };

  function setCourseLoadState(state) {
    _courseLoadState = state || { status: "idle", message: "", traceId: "" };
  }

  function clearCourseCache() {
    _courseCache = null;
    _courseCachePromise = null;
    setCourseLoadState({ status: "idle", message: "", traceId: "" });
  }

  function fetchCourses(options) {
    var forceReload = options && options.forceReload === true;
    if (forceReload) {
      clearCourseCache();
    }

    if (_courseCachePromise) return _courseCachePromise;

    setCourseLoadState({ status: "loading", message: "", traceId: "" });

    _courseCachePromise = window.BackendApi.courses.listDetailed()
      .then(function (result) {
        var courses = Array.isArray(result.data) ? result.data : [];
        _courseCache = courses;

        if (result.meta && result.meta.degraded) {
          setCourseLoadState({
            status: "degraded",
            message: "课程列表当前处于降级状态，请稍后重试。",
            traceId: result.meta.traceId || ""
          });
        } else {
          setCourseLoadState({ status: "ready", message: "", traceId: result.meta ? result.meta.traceId || "" : "" });
        }

        return _courseCache;
      })
      .catch(function (err) {
        _courseCachePromise = null;
        _courseCache = null;
        setCourseLoadState({
          status: "error",
          message: (window.UI && UI.formatApiErrorMessage) ? UI.formatApiErrorMessage(err, "bootstrap") : "课程列表加载失败。",
          traceId: err && err.traceId ? err.traceId : ""
        });
        throw err;
      });

    return _courseCachePromise;
  }

  AppConfig.resolveCourseId = function () {
    var urlParams = new URLSearchParams(window.location.search);
    var fromUrl = parseInt(urlParams.get("course"), 10);
    if (!Number.isNaN(fromUrl) && fromUrl > 0) return fromUrl;

    try {
      var stored = sessionStorage.getItem("math_analysis_course_id");
      if (stored) {
        var storedId = parseInt(stored, 10);
        if (!Number.isNaN(storedId) && storedId > 0) return storedId;
      }
    } catch (_) {}

    if (_courseCache && _courseCache.length) return _courseCache[0].id;
    return null;
  };

  AppConfig.fetchCourses = fetchCourses;
  AppConfig.clearCourseCache = clearCourseCache;
  AppConfig.getCachedCourses = function () { return _courseCache || []; };
  AppConfig.getCourseLoadState = function () { return _courseLoadState; };
  AppConfig.leaderboardTake = AppConfig.leaderboardTake || 10;
  AppConfig.defaultChapterId = AppConfig.defaultChapterId || null;

  AppConfig.persistCourseId = function (courseId) {
    try { sessionStorage.setItem("math_analysis_course_id", String(courseId)); } catch (_) {}
  };

  window.AppConfig = AppConfig;
})();
