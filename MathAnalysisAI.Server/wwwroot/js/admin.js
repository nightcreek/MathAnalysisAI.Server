(function () {
  var trendChartInstance = null;
  var userPage = 1;
  var userPageSize = 20;

  function getRoleLabel(role) {
    var map = { student: "学生", teacher: "教师", school_leader: "学校管理员", admin: "超级管理员" };
    return map[role] || role || "未知";
  }

  function renderDashboard(dashboard) {
    var container = UI.qs("#adminDashboardContainer");
    if (!container || !dashboard) return;

    var llmErrorRate = dashboard.totalLlmCalls > 0
      ? Math.round(dashboard.totalLlmFailedCalls / dashboard.totalLlmCalls * 1000) / 10
      : 0;

    container.innerHTML =
      '<div class="stats-card">' +
        '<div class="stats-card-value">' + dashboard.totalUsers + '</div>' +
        '<div class="stats-card-label">总用户数</div>' +
      '</div>' +
      '<div class="stats-card">' +
        '<div class="stats-card-value">' + dashboard.totalAnalyses + '</div>' +
        '<div class="stats-card-label">分析请求</div>' +
      '</div>' +
      '<div class="stats-card">' +
        '<div class="stats-card-value">' + dashboard.totalOcrRecords + '</div>' +
        '<div class="stats-card-label">OCR 识别</div>' +
      '</div>' +
      '<div class="stats-card">' +
        '<div class="stats-card-value">' + dashboard.totalQuestions + '</div>' +
        '<div class="stats-card-label">题库题目</div>' +
      '</div>' +
      '<div class="stats-card">' +
        '<div class="stats-card-value">' + (dashboard.totalTokensConsumed > 1000
          ? Math.round(dashboard.totalTokensConsumed / 1000) + 'K'
          : dashboard.totalTokensConsumed) + '</div>' +
        '<div class="stats-card-label">Token 消耗</div>' +
      '</div>' +
      '<div class="stats-card">' +
        '<div class="stats-card-value">' + dashboard.totalLlmCalls + '</div>' +
        '<div class="stats-card-label">LLM 调用 (' + dashboard.totalLlmSuccessCalls + ' 成功)</div>' +
      '</div>' +
      '<div class="stats-card">' +
        '<div class="stats-card-value">' + dashboard.averageLlmLatencyMs + 'ms</div>' +
        '<div class="stats-card-label">平均 LLM 延迟</div>' +
      '</div>' +
      '<div class="stats-card">' +
        '<div class="stats-card-value ' + (llmErrorRate > 5 ? 'result-status-wrong' : 'result-status-correct') + '">' + llmErrorRate + '%</div>' +
        '<div class="stats-card-label">LLM 错误率</div>' +
      '</div>';
  }

  function renderTrendChart(dailyStats) {
    var canvas = UI.qs("#adminTrendChart");
    if (!canvas || !dailyStats || !dailyStats.length) return;

    if (trendChartInstance) {
      trendChartInstance.destroy();
      trendChartInstance = null;
    }

    var labels = dailyStats.map(function (d) { return d.date.substring(5); });
    var analysisData = dailyStats.map(function (d) { return d.analysisCount; });
    var llmData = dailyStats.map(function (d) { return d.llmCallCount; });

    var ctx = canvas.getContext("2d");
    trendChartInstance = new Chart(ctx, {
      type: "line",
      data: {
        labels: labels,
        datasets: [
          {
            label: "分析请求",
            data: analysisData,
            borderColor: "rgba(59, 130, 246, 1)",
            backgroundColor: "rgba(59, 130, 246, 0.1)",
            fill: true,
            tension: 0.3,
            pointRadius: 4,
            pointHoverRadius: 6
          },
          {
            label: "LLM 调用",
            data: llmData,
            borderColor: "rgba(34, 197, 94, 1)",
            backgroundColor: "rgba(34, 197, 94, 0.1)",
            fill: true,
            tension: 0.3,
            pointRadius: 4,
            pointHoverRadius: 6
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: true,
        plugins: {
          legend: { position: "bottom" }
        },
        scales: {
          y: {
            beginAtZero: true,
            ticks: { stepSize: 1 }
          }
        }
      }
    });
  }

  function renderUserList(data) {
    var container = UI.qs("#adminUserListContainer");
    var paginationEl = UI.qs("#adminUserPagination");
    if (!container) return;

    if (!data || !data.items || !data.items.length) {
      container.innerHTML = '<div class="empty-state"><h3>无用户</h3><p>没有找到符合条件的用户。</p></div>';
      if (paginationEl) paginationEl.innerHTML = "";
      return;
    }

    var html = '<table class="rank-table"><thead><tr>' +
      '<th>ID</th><th>用户名</th><th>姓名</th><th>学号</th><th>角色</th><th>分析数</th><th>注册时间</th><th>操作</th>' +
      '</tr></thead><tbody>';

    data.items.forEach(function (u) {
      var roleClass = u.role === "admin" ? "result-status-correct"
        : u.role === "teacher" ? "result-status-unknown"
        : "";
      var created = u.createdAt ? new Date(u.createdAt).toLocaleDateString("zh-CN") : "-";

      html += '<tr>' +
        '<td>' + u.id + '</td>' +
        '<td><strong>' + UI.escapeHtml(u.username) + '</strong></td>' +
        '<td>' + UI.escapeHtml(u.realName || "-") + '</td>' +
        '<td>' + UI.escapeHtml(u.studentNumber || "-") + '</td>' +
        '<td><span class="result-status-pill ' + roleClass + '">' + getRoleLabel(u.role) + '</span></td>' +
        '<td>' + u.analysisCount + '</td>' +
        '<td>' + created + '</td>' +
        '<td><select class="role-select" data-user-id="' + u.id + '" data-current-role="' + UI.escapeHtml(u.role) + '">' +
          '<option value="student"' + (u.role === "student" ? " selected" : "") + '>学生</option>' +
          '<option value="teacher"' + (u.role === "teacher" ? " selected" : "") + '>教师</option>' +
          '<option value="school_leader"' + (u.role === "school_leader" ? " selected" : "") + '>学校管理员</option>' +
          '<option value="admin"' + (u.role === "admin" ? " selected" : "") + '>超级管理员</option>' +
        '</select></td>' +
      '</tr>';
    });

    html += '</tbody></table>';
    container.innerHTML = html;

    container.querySelectorAll(".role-select").forEach(function (select) {
      select.addEventListener("change", function () {
        var userId = parseInt(this.getAttribute("data-user-id"), 10);
        var currentRole = this.getAttribute("data-current-role");
        var newRole = this.value;

        if (newRole === currentRole) return;

        if (!confirm("确认将用户 #" + userId + " 的角色从 '" + getRoleLabel(currentRole) + "' 改为 '" + getRoleLabel(newRole) + "'？")) {
          this.value = currentRole;
          return;
        }

        updateUserRole(userId, newRole, this);
      });
    });

    renderUserPagination(data);
  }

  function renderUserPagination(data) {
    var paginationEl = UI.qs("#adminUserPagination");
    if (!paginationEl) return;

    var totalPages = Math.ceil(data.totalCount / data.pageSize);
    if (totalPages <= 1) {
      paginationEl.innerHTML = "";
      return;
    }

    var html = '<span class="hint" style="margin-right:8px;">共 ' + data.totalCount + ' 用户</span>';

    if (data.page > 1) {
      html += '<button class="btn-secondary" data-page="' + (data.page - 1) + '" style="font-size:13px;">上一页</button>';
    }
    if (data.page < totalPages) {
      html += '<button class="btn-secondary" data-page="' + (data.page + 1) + '" style="font-size:13px;margin-left:8px;">下一页</button>';
    }

    paginationEl.innerHTML = html;

    paginationEl.querySelectorAll("button[data-page]").forEach(function (btn) {
      btn.addEventListener("click", function () {
        userPage = parseInt(btn.getAttribute("data-page"), 10);
        loadUserList();
      });
    });
  }

  async function updateUserRole(userId, newRole, selectEl) {
    try {
      await Api.putJson("/api/admin/users/" + userId + "/role", { role: newRole });
      selectEl.setAttribute("data-current-role", newRole);
    } catch (err) {
      var currentRole = selectEl.getAttribute("data-current-role");
      selectEl.value = currentRole;
      alert("更新角色失败：" + (err.message || "未知错误"));
    }
  }

  async function loadDashboard() {
    try {
      var dashboard = await Api.getJson("/api/admin/dashboard");
      renderDashboard(dashboard);
      renderTrendChart(dashboard.dailyStats);
    } catch (err) {
      var container = UI.qs("#adminDashboardContainer");
      if (container) container.innerHTML = '<div class="hint error">加载失败，可能无管理员权限。</div>';
    }
  }

  async function loadUserList() {
    var container = UI.qs("#adminUserListContainer");
    if (container) container.innerHTML = '<div class="hint">加载中...</div>';

    var params = new URLSearchParams();
    params.set("page", userPage);
    params.set("pageSize", userPageSize);

    var search = UI.qs("#adminUserSearch").value.trim();
    if (search) params.set("search", search);

    var role = UI.qs("#adminUserRoleFilter").value;
    if (role) params.set("role", role);

    try {
      var data = await Api.getJson("/api/admin/users?" + params.toString());
      renderUserList(data);
    } catch (err) {
      if (container) container.innerHTML = '<div class="hint error">加载失败，可能无管理员权限。</div>';
    }
  }

  function clearUserFilters() {
    UI.qs("#adminUserSearch").value = "";
    UI.qs("#adminUserRoleFilter").value = "";
    userPage = 1;
    loadUserList();
  }

  function initAdminPage() {
    loadDashboard();
    loadUserList();

    var searchBtn = UI.qs("#adminUserSearchBtn");
    var clearBtn = UI.qs("#adminUserClearBtn");
    var searchInput = UI.qs("#adminUserSearch");

    if (searchBtn) {
      searchBtn.addEventListener("click", function () { userPage = 1; loadUserList(); });
    }
    if (clearBtn) {
      clearBtn.addEventListener("click", clearUserFilters);
    }
    if (searchInput) {
      searchInput.addEventListener("keydown", function (e) {
        if (e.key === "Enter") { userPage = 1; loadUserList(); }
      });
    }
  }

  document.addEventListener("DOMContentLoaded", function () {
    if (!UI.qs("#adminPageRoot")) return;
    initAdminPage();
  });
})();
