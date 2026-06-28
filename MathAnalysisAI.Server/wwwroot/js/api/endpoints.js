(function () {
  var contracts = window.ApiContractCatalog || { modules: {} };
  var auth = contracts.modules.auth && contracts.modules.auth.endpoints ? contracts.modules.auth.endpoints : {};
  var analysis = contracts.modules.analysis && contracts.modules.analysis.endpoints ? contracts.modules.analysis.endpoints : {};
  var admin = contracts.modules.admin && contracts.modules.admin.endpoints ? contracts.modules.admin.endpoints : {};
  var resources = contracts.modules.resources && contracts.modules.resources.endpoints ? contracts.modules.resources.endpoints : {};
  var question = contracts.modules.question && contracts.modules.question.endpoints ? contracts.modules.question.endpoints : {};
  var support = contracts.modules.support && contracts.modules.support.endpoints ? contracts.modules.support.endpoints : {};

  function materialize(templateContract, tokenName, value) {
    return Object.assign({}, templateContract, {
      endpoint: String(templateContract.endpointTemplate || "").replace("{" + tokenName + "}", encodeURIComponent(value))
    });
  }

  window.ApiEndpoints = {
    AUTH_INFO: auth.info,
    AUTH_ME: auth.me,
    AUTH_LOGIN: auth.login,
    AUTH_REGISTER: auth.register,
    AUTH_IMPERSONATE: auth.impersonate,

    ANALYSIS_RUN: analysis.run,
    ANALYSIS_STREAM: analysis.stream,

    ADMIN_DASHBOARD: admin.dashboard,
    ADMIN_USERS: admin.users,
    ADMIN_TEACHERS: admin.teachers,
    ADMIN_IMPORT_STUDENTS: admin.importStudents,

    COURSES: support.coursesList,
    COURSE_CHAPTERS: function (courseId) {
      return materialize(support.courseChapters, "courseId", courseId);
    },

    COURSE_MATERIALS: support.courseMaterialsList,

    RESOURCES: resources.list,

    QUESTION_LIST: question.list,

    LEADERBOARD_PUBLIC: support.leaderboardPublic,
    STATS_PERSONAL: support.statsPersonal,

    PHOTO_OCR: support.photoOcr,
    PHOTO_OCR_CONFIRM: function (recordId) {
      return materialize(support.photoOcrConfirm, "id", recordId);
    },

    SYMBOLIC_COMPUTE: support.symbolicCompute,

    ADMIN_USER_ROLE: function (userId) {
      return materialize(admin.updateUserRole, "userId", userId);
    },

    RESOURCE_BY_ID: function (resourceId) {
      return materialize(resources.update, "resourceId", resourceId);
    }
  };
})();
