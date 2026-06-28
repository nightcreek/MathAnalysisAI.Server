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

  backendApi.admin = {
    getDashboard: function () {
      return client.get(endpoints.ADMIN_DASHBOARD);
    },
    listUsers: function (params) {
      return client.get(withQuery(endpoints.ADMIN_USERS, params));
    },
    updateUserRole: function (userId, payload) {
      return client.put(endpoints.ADMIN_USER_ROLE(userId), payload);
    },
    listTeachers: function () {
      return client.get(endpoints.ADMIN_TEACHERS);
    },
    createTeacher: function (payload) {
      return client.post(endpoints.ADMIN_TEACHERS, payload);
    },
    importStudents: function (payload) {
      return client.post(endpoints.ADMIN_IMPORT_STUDENTS, payload);
    }
  };

  window.BackendApi = backendApi;
})();
