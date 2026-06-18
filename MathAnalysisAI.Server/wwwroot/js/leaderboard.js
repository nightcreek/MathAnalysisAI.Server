(function () {
  function renderLeaderboard(rows) {
    var data = Array.isArray(rows) ? rows : [];
    if (!data.length) return "<div class='status'>暂无排行榜数据，完成一次分析后会显示在这里。</div>";

    var html = "<table class='rank-table'><thead><tr>" +
      "<th>排名</th><th>用户名</th><th>练习次数</th><th>正确数</th><th>错误数</th><th>正确率</th><th>积分</th>" +
      "</tr></thead><tbody>";

    data.forEach(function (x, idx) {
      var rank = x.rank || (idx + 1);
      var accuracy = x.accuracyRate == null ? "-" : String(x.accuracyRate);
      var score = x.rankingScore == null ? "-" : String(x.rankingScore);
      html += "<tr>" +
        "<td>" + UI.escapeHtml(rank) + "</td>" +
        "<td>" + UI.escapeHtml(x.username || "") + "</td>" +
        "<td>" + UI.escapeHtml(x.attemptCount || 0) + "</td>" +
        "<td>" + UI.escapeHtml(x.correctCount || 0) + "</td>" +
        "<td>" + UI.escapeHtml(x.wrongCount || 0) + "</td>" +
        "<td>" + UI.escapeHtml(accuracy) + "</td>" +
        "<td>" + UI.escapeHtml(score) + "</td>" +
        "</tr>";
    });

    return html + "</tbody></table>";
  }

  function getSelectedCourseId() {
    var select = document.getElementById("leaderboardCourseSelect");
    if (select && select.value) {
      var id = Number(select.value);
      if (Number.isFinite(id) && id > 0) return id;
    }
    return AppConfig.resolveCourseId() || null;
  }

  var isLoading = false;

  async function loadLeaderboard() {
    var box = UI.qs("#leaderboardContainer");
    if (!box || isLoading) return;

    var courseId = getSelectedCourseId();
    if (!courseId) {
      UI.renderBootstrapError(box, "课程列表尚未准备好。", initLeaderboardPage, "");
      return;
    }

    isLoading = true;
    UI.showStatus(box, "加载中...", false);

    try {
      var result = await Api.getJsonDetailed("/api/leaderboard/public?courseId=" + courseId + "&take=" + AppConfig.leaderboardTake);
      if (result.meta && result.meta.degraded) {
        UI.renderBootstrapError(box, "排行榜当前处于降级状态，请稍后重试。", loadLeaderboard, result.meta.traceId);
      } else {
        box.className = "";
        box.innerHTML = renderLeaderboard(result.data);
      }
    } catch (err) {
      UI.renderErrorPanel(box, {
        title: "排行榜加载失败",
        message: UI.formatApiErrorMessage(err, "leaderboard"),
        traceId: err && err.traceId ? err.traceId : "",
        actionLabel: "重试",
        onAction: loadLeaderboard
      });
    } finally {
      isLoading = false;
    }
  }

  async function loadLeaderboardCourses() {
    var select = document.getElementById("leaderboardCourseSelect");
    if (!select) return;

    try {
      var courses = await window.AppConfig.fetchCourses();
      var courseState = window.AppConfig.getCourseLoadState();

      if (courseState.status === "degraded") {
        select.innerHTML = '<option value="">课程加载降级</option>';
        UI.renderBootstrapError("#leaderboardContainer", courseState.message, loadLeaderboardCourses, courseState.traceId || "");
        return;
      }

      select.innerHTML = "";
      if (!courses || !courses.length) {
        select.innerHTML = '<option value="">无可用课程</option>';
        return;
      }

      courses.forEach(function (course) {
        var option = document.createElement("option");
        option.value = course.id;
        option.textContent = course.name;
        select.appendChild(option);
      });

      var resolvedId = AppConfig.resolveCourseId();
      if (resolvedId && courses.some(function (c) { return c.id === resolvedId; })) {
        select.value = resolvedId;
      } else if (courses.length > 0) {
        select.value = courses[0].id;
      }

      await loadLeaderboard();
    } catch (err) {
      select.innerHTML = '<option value="">加载失败</option>';
      UI.renderBootstrapError("#leaderboardContainer", UI.formatApiErrorMessage(err, "bootstrap"), loadLeaderboardCourses, err && err.traceId ? err.traceId : "");
    }
  }

  function handleCourseChange() {
    var select = document.getElementById("leaderboardCourseSelect");
    if (select && select.value) {
      AppConfig.persistCourseId(Number(select.value));
    }
    loadLeaderboard();
  }

  async function initLeaderboardPage() {
    await loadLeaderboardCourses();
    var select = document.getElementById("leaderboardCourseSelect");
    if (select) {
      select.addEventListener("change", handleCourseChange);
    }
  }

  window.loadLeaderboard = loadLeaderboard;

  document.addEventListener("DOMContentLoaded", function () {
    if (UI.qs("#leaderboardPageRoot")) {
      initLeaderboardPage();
    }
  });
})();
