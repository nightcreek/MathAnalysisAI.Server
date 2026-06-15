(function () {
  var AppConfig = window.AppConfig || {};

  var _courseCache = null;
  var _courseCachePromise = null;

  function fetchCourses() {
    if (_courseCachePromise) return _courseCachePromise;
    _courseCachePromise = fetch("/api/courses")
      .then(function (r) { if (!r.ok) throw new Error("Failed to fetch courses"); return r.json(); })
      .then(function (courses) {
        _courseCache = courses && courses.length ? courses : [];
        return _courseCache;
      })
      .catch(function (err) {
        console.warn("Failed to load courses:", err);
        _courseCache = [];
        _courseCachePromise = null;
        return [];
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
  AppConfig.getCachedCourses = function () { return _courseCache || []; };

  AppConfig.persistCourseId = function (courseId) {
    try { sessionStorage.setItem("math_analysis_course_id", String(courseId)); } catch (_) {}
  };

  window.AppConfig = AppConfig;
})();
