(function () {
  var endpoints = window.ApiEndpoints;
  var client = window.ApiClient;
  var backendApi = window.BackendApi || {};

  backendApi.auth = {
    getInfo: function () {
      return client.get(endpoints.AUTH_INFO);
    },
    getCurrentUser: function () {
      return client.get(endpoints.AUTH_ME);
    },
    login: function (payload) {
      return client.post(endpoints.AUTH_LOGIN, payload);
    },
    register: function (payload) {
      return client.post(endpoints.AUTH_REGISTER, payload);
    },
    impersonate: function (payload) {
      return client.post(endpoints.AUTH_IMPERSONATE, payload);
    }
  };

  window.BackendApi = backendApi;
})();
