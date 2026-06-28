(function () {
  var backendApi = window.BackendApi || {};

  function normalizeResourceItem(resource) {
    return {
      id: resource && resource.id ? resource.id : 0,
      title: resource && resource.title ? resource.title : "无标题",
      content: resource && resource.description ? resource.description : "",
      difficulty: "中等",
      questionType: resource && resource.category ? resource.category : "资源",
      link: resource && resource.link ? resource.link : ""
    };
  }

  function matchesFilters(item, filters) {
    var search = String(filters && filters.search ? filters.search : "").toLowerCase();
    var difficulty = String(filters && filters.difficulty ? filters.difficulty : "").toLowerCase();
    var questionType = String(filters && filters.questionType ? filters.questionType : "").toLowerCase();

    var searchableText = [
      item.title || "",
      item.content || "",
      item.questionType || ""
    ].join(" ").toLowerCase();

    if (search && searchableText.indexOf(search) === -1) {
      return false;
    }

    if (difficulty && String(item.difficulty || "").toLowerCase() !== difficulty) {
      return false;
    }

    if (questionType && String(item.questionType || "").toLowerCase() !== questionType) {
      return false;
    }

    return true;
  }

  backendApi.questions = {
    list: async function (filters) {
      var resources = await backendApi.resources.list(filters && filters.courseId ? filters.courseId : null);
      var take = Number(filters && filters.take ? filters.take : 20);
      var items = (Array.isArray(resources) ? resources : [])
        .map(normalizeResourceItem)
        .filter(function (item) {
          return matchesFilters(item, filters || {});
        })
        .slice(0, take > 0 ? take : 20);

      return { items: items };
    }
  };

  window.BackendApi = backendApi;
})();
