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

  backendApi.resources = {
    list: function (courseId) {
      var params = new URLSearchParams();
      if (courseId) {
        params.set("courseId", String(courseId));
      }
      return client.get(withQuery(endpoints.RESOURCES, params));
    },
    listDetailed: function (courseId) {
      var params = new URLSearchParams();
      if (courseId) {
        params.set("courseId", String(courseId));
      }
      return client.getDetailed(withQuery(endpoints.RESOURCES, params));
    },
    create: function (payload) {
      return client.post(endpoints.RESOURCES, payload);
    },
    update: function (resourceId, payload) {
      return client.put(endpoints.RESOURCE_BY_ID(resourceId), payload);
    },
    delete: function (resourceId) {
      return client.del(endpoints.RESOURCE_BY_ID(resourceId));
    }
  };

  backendApi.materials = {
    listCourseMaterials: function (params) {
      return client.get(withQuery(endpoints.COURSE_MATERIALS, params));
    }
  };

  window.BackendApi = backendApi;
})();
