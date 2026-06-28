window.Api = (function () {
  return {
    getJson: window.ApiClient.get,
    getJsonDetailed: window.ApiClient.getDetailed,
    postJson: window.ApiClient.post,
    postJsonDetailed: window.ApiClient.postDetailed,
    postFormData: window.ApiClient.postFormData,
    postFormDataDetailed: window.ApiClient.postFormDataDetailed,
    putJson: window.ApiClient.put,
    putJsonDetailed: window.ApiClient.putDetailed,
    delete: window.ApiClient.del,
    fetchWithAuth: window.ApiClient.fetchWithAuth
  };
})();
