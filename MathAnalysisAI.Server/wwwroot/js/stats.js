(function () {
  var radarChartInstance = null;

  function renderSummary(summary) {
    var container = document.getElementById("personalSummaryContainer");
    if (!container) return;

    if (!summary) {
      container.innerHTML = '<div class="hint">暂无学习数据。完成一些练习后再来查看。</div>';
      return;
    }

    var accuracyClass = summary.overallAccuracy >= 80 ? "result-status-correct"
      : summary.overallAccuracy >= 60 ? "result-status-unknown"
      : "result-status-wrong";

    var masteryPercent = summary.totalKnowledgePoints > 0
      ? Math.round(summary.masteredKnowledgePoints / summary.totalKnowledgePoints * 100)
      : 0;

    container.innerHTML =
      '<div class="stats-card">' +
        '<div class="stats-card-value">' + summary.totalAttempts + '</div>' +
        '<div class="stats-card-label">总答题数</div>' +
      '</div>' +
      '<div class="stats-card">' +
        '<div class="stats-card-value ' + accuracyClass + '">' + summary.overallAccuracy + '%</div>' +
        '<div class="stats-card-label">正确率</div>' +
      '</div>' +
      '<div class="stats-card">' +
        '<div class="stats-card-value">' + summary.totalCorrect + ' / ' + summary.totalWrong + '</div>' +
        '<div class="stats-card-label">正确 / 错误</div>' +
      '</div>' +
      '<div class="stats-card">' +
        '<div class="stats-card-value">' + summary.masteredKnowledgePoints + ' / ' + summary.totalKnowledgePoints + '</div>' +
        '<div class="stats-card-label">已掌握知识点 (' + masteryPercent + '%)</div>' +
      '</div>';
  }

  function renderCourseProgress(courseProgress) {
    var container = document.getElementById("courseProgressContainer");
    if (!container) return;

    if (!courseProgress || !courseProgress.length) {
      container.innerHTML = '<div class="hint">暂无课程数据。</div>';
      return;
    }

    var html = '<div class="course-progress-list">';
    courseProgress.forEach(function (course) {
      var accuracyClass = course.accuracyRate >= 80 ? "result-status-correct"
        : course.accuracyRate >= 60 ? "result-status-unknown"
        : "result-status-wrong";

      html += '<div class="course-progress-item">' +
        '<div class="course-progress-header">' +
          '<span class="course-progress-name">' + UI.escapeHtml(course.courseName) + '</span>' +
          '<span class="result-status-pill ' + accuracyClass + '">' + course.accuracyRate + '%</span>' +
        '</div>' +
        '<div class="course-progress-bar-track">' +
          '<div class="course-progress-bar-fill" style="width:' + course.accuracyRate + '%;"></div>' +
        '</div>' +
        '<div class="course-progress-meta">' +
          '答题 ' + course.attemptCount + ' 次  |  排名积分 ' + course.rankingScore +
        '</div>' +
      '</div>';
    });
    html += '</div>';

    container.innerHTML = html;
  }

  function renderRadarChart(knowledgeMastery) {
    var canvas = document.getElementById("knowledgeRadarChart");
    var hint = document.getElementById("statsRadarHint");
    if (!canvas) return;

    if (radarChartInstance) {
      radarChartInstance.destroy();
      radarChartInstance = null;
    }

    if (!knowledgeMastery || !knowledgeMastery.length) {
      if (hint) hint.textContent = "暂无知识点掌握数据。完成一些练习后再来查看。";
      canvas.style.display = "none";
      return;
    }

    var topItems = knowledgeMastery.slice(0, 10);
    var labels = topItems.map(function (k) { return k.knowledgePointName || "知识点 " + k.knowledgePointId; });
    var data = topItems.map(function (k) { return k.masteryLevel; });
    var backgroundColors = topItems.map(function (k) {
      if (k.masteryLevel >= 70) return "rgba(34, 197, 94, 0.2)";
      if (k.masteryLevel >= 40) return "rgba(59, 130, 246, 0.2)";
      return "rgba(239, 68, 68, 0.2)";
    });
    var borderColors = topItems.map(function (k) {
      if (k.masteryLevel >= 70) return "rgba(34, 197, 94, 1)";
      if (k.masteryLevel >= 40) return "rgba(59, 130, 246, 1)";
      return "rgba(239, 68, 68, 1)";
    });

    if (hint) hint.textContent = "以下为练习频率最高的 10 个知识点掌握情况（满分 100）。";
    canvas.style.display = "block";

    var ctx = canvas.getContext("2d");
    radarChartInstance = new Chart(ctx, {
      type: "radar",
      data: {
        labels: labels,
        datasets: [{
          label: "掌握度",
          data: data,
          backgroundColor: "rgba(59, 130, 246, 0.2)",
          borderColor: "rgba(59, 130, 246, 1)",
          borderWidth: 2,
          pointBackgroundColor: borderColors,
          pointBorderColor: borderColors,
          pointRadius: 5,
          pointHoverRadius: 7
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: true,
        scales: {
          r: {
            beginAtZero: true,
            max: 100,
            ticks: { stepSize: 20, backdropColor: "transparent" },
            pointLabels: { font: { size: 12 } }
          }
        },
        plugins: {
          legend: { display: false }
        }
      }
    });
  }

  function renderKnowledgeTable(knowledgeMastery) {
    var hintEl = document.getElementById("statsRadarHint");
    if (!knowledgeMastery || !knowledgeMastery.length) return;

    var tableHtml = '<div style="margin-top:16px;"><table class="rank-table"><thead><tr>' +
      '<th>知识点</th><th>掌握度</th><th>练习次数</th><th>正确率</th>' +
      '</tr></thead><tbody>';

    knowledgeMastery.forEach(function (k) {
      var correctRate = k.practiceCount > 0
        ? Math.round(k.correctCount / k.practiceCount * 100)
        : 0;
      var levelClass = k.masteryLevel >= 70 ? "result-status-correct"
        : k.masteryLevel >= 40 ? "result-status-unknown"
        : "result-status-wrong";

      tableHtml += '<tr>' +
        '<td>' + UI.escapeHtml(k.knowledgePointName || "知识点 " + k.knowledgePointId) + '</td>' +
        '<td><span class="result-status-pill ' + levelClass + '">' + k.masteryLevel + '</span></td>' +
        '<td>' + k.practiceCount + '</td>' +
        '<td>' + correctRate + '%</td>' +
        '</tr>';
    });

    tableHtml += '</tbody></table></div>';

    var container = document.getElementById("knowledgeRadarChart");
    if (container && container.parentNode) {
      var tableDiv = document.createElement("div");
      tableDiv.innerHTML = tableHtml;
      container.parentNode.appendChild(tableDiv);
    }
  }

  async function loadStatsCourses() {
    var select = document.getElementById("statsCourseSelect");
    if (!select) return;

    try {
      var courses = await window.AppConfig.fetchCourses();
      courses.forEach(function (course) {
        var option = document.createElement("option");
        option.value = course.id;
        option.textContent = course.name;
        select.appendChild(option);
      });

      var resolvedId = AppConfig.resolveCourseId();
      if (resolvedId && courses.some(function (c) { return c.id === resolvedId; })) {
        select.value = resolvedId;
      }
    } catch (_) {}
  }

  async function loadPersonalStats() {
    var userHint = document.getElementById("statsCurrentUserHint");
    if (window.Auth && window.Auth.loadCurrentUser) {
      await window.Auth.loadCurrentUser();
    }

    var user = window.Auth && window.Auth.getCurrentUser ? window.Auth.getCurrentUser() : null;
    if (!user) {
      if (userHint) userHint.textContent = "当前未登录，请先登录后查看个人统计。";
      renderSummary(null);
      renderCourseProgress(null);
      renderRadarChart(null);
      return;
    }

    if (userHint) {
      var displayName = user.realName || user.username || "未知用户";
      var role = user.role || "student";
      if (window.Auth && window.Auth.isDevelopmentFallbackApplied && window.Auth.isDevelopmentFallbackApplied()) {
        userHint.textContent = "当前用户：" + displayName + "（开发模式）";
      } else {
        userHint.textContent = "当前用户：" + displayName + "（" + role + "）";
      }
    }

    try {
      var courseSelect = document.getElementById("statsCourseSelect");
      var courseId = courseSelect && courseSelect.value ? courseSelect.value : "";
      var url = "/api/stats/personal";
      if (courseId) {
        url += "?courseId=" + courseId;
      }
      var stats = await Api.getJson(url);
      renderSummary(stats.summary);
      renderCourseProgress(stats.courseProgress);
      renderRadarChart(stats.knowledgeMastery);
      renderKnowledgeTable(stats.knowledgeMastery);
    } catch (err) {
      console.warn("Failed to load personal stats:", err);
      renderSummary(null);
      renderCourseProgress(null);
      renderRadarChart(null);
    }
  }

  function initStatsPage() {
    loadStatsCourses().then(function () {
      loadPersonalStats();

      var select = document.getElementById("statsCourseSelect");
      if (select) {
        select.addEventListener("change", function () {
          if (select.value) {
            AppConfig.persistCourseId(Number(select.value));
          }
          loadPersonalStats();
        });
      }
    });

    if (window.loadLeaderboard) {
      window.loadLeaderboard();
    }
  }

  document.addEventListener("DOMContentLoaded", function () {
    if (!UI.qs("#statsPageRoot")) return;
    initStatsPage();
  });
})();
