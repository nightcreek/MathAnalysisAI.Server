(function () {
  var endpoints = window.ApiEndpoints;
  var client = window.ApiClient;
  var backendApi = window.BackendApi || {};

  function withQuery(contract, params) {
    var query = params instanceof URLSearchParams ? params.toString() : new URLSearchParams(params || {}).toString();
    return Object.assign({}, contract, {
      endpoint: contract.endpoint + (query ? "?" + query : "")
    });
  }

  backendApi.analysis = {
    run: function (payload) {
      return client.post(endpoints.ANALYSIS_RUN, payload);
    },
    runStream: function (payload) {
      return client.fetchWithAuth(endpoints.ANALYSIS_STREAM, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });
    }
  };

  backendApi.courses = {
    listDetailed: function () {
      return client.getDetailed(endpoints.COURSES);
    },
    list: function () {
      return client.get(endpoints.COURSES);
    },
    listChaptersDetailed: function (courseId) {
      return client.getDetailed(endpoints.COURSE_CHAPTERS(courseId));
    }
  };

  backendApi.leaderboard = {
    getPublicDetailed: function (courseId, take) {
      var params = new URLSearchParams();
      params.set("courseId", String(courseId));
      params.set("take", String(take));
      return client.getDetailed(withQuery(endpoints.LEADERBOARD_PUBLIC, params));
    }
  };

  backendApi.stats = {
    getPersonal: function (courseId) {
      if (!courseId) {
        return client.get(endpoints.STATS_PERSONAL);
      }

      var params = new URLSearchParams();
      params.set("courseId", String(courseId));
      return client.get(withQuery(endpoints.STATS_PERSONAL, params));
    }
  };

  backendApi.photoSolutions = {
    runOcr: function (formData) {
      return client.postFormData(endpoints.PHOTO_OCR, formData);
    },
    confirmOcr: function (recordId, payload) {
      return client.post(endpoints.PHOTO_OCR_CONFIRM(recordId), payload);
    }
  };

  backendApi.symbolic = {
    compute: function (payload) {
      return client.post(endpoints.SYMBOLIC_COMPUTE, payload);
    }
  };

  window.BackendApi = backendApi;
})();
