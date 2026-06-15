(function () {
  const routeMap = {
    "/": "/index.html",
    "/index.html": "/index.html",
    "/analysis.html": "/analysis.html",
    "/ocr.html": "/analysis.html",
    "/materials.html": "/materials.html",
    "/questions.html": "/questions.html",
    "/stats.html": "/stats.html",
    "/admin.html": "/admin.html",
    "/dev.html": "/dev.html",
    "/login.html": "/login.html",
    "/leaderboard.html": "/leaderboard.html",
    "/materials-manage.html": "/materials-manage.html",
    "/resources-manage.html": "/resources-manage.html"
  };

  const standardNavLinks = [
    { href: "/index.html", label: "首页", className: "" },
    { href: "/analysis.html", label: "解题分析", className: "" },
    { href: "/questions.html", label: "题库", className: "" },
    { href: "/materials.html", label: "课程资料", className: "" },
    { href: "/leaderboard.html", label: "排行榜", className: "" },
    { href: "/stats.html", label: "学习统计", className: "" },
    { href: "/dev.html", label: "开发工具", className: "nav-dev" },
    { href: "/materials-manage.html", label: "资料管理", className: "nav-dev", roles: ["teacher", "admin"] },
    { href: "/resources-manage.html", label: "网络资源", className: "nav-dev", roles: ["teacher", "admin"] },
    { href: "/admin.html", label: "管理", className: "nav-admin", roles: ["admin"] }
  ];

  function normalizePath(pathname) {
    if (!pathname || pathname === "/") return "/index.html";
    return routeMap[pathname] || pathname;
  }

  function setActiveNav() {
    const current = normalizePath(window.location.pathname);
    const links = document.querySelectorAll(".top-nav-link");
    links.forEach((link) => {
      const href = link.getAttribute("href") || "";
      if (href === current) {
        link.classList.add("active");
      } else {
        link.classList.remove("active");
      }
    });
  }

  function canDisplayDevLink(user) {
    const role = user && user.role ? String(user.role).toLowerCase() : "";
    return role === "admin" || role === "teacher";
  }

  function normalizeTopNavLinks(user) {
    const nav = document.querySelector(".top-nav");
    if (!nav) return;

    nav.innerHTML =
      "<div class='brand'>" +
      "<a class='brand-link' href='/index.html'>数学分析智能体</a>" +
      "<span class='brand-subtitle'>MathAnalysisAI</span>" +
      "</div>" +
      "<div class='nav-links'></div>" +
      "<div class='top-nav-user'></div>";

    const linksHost = nav.querySelector(".nav-links");
    standardNavLinks.forEach((item) => {
      if (item.href === "/dev.html" && !canDisplayDevLink(user)) {
        return;
      }
      if (item.roles && item.roles.length > 0) {
        const userRole = user && user.role ? String(user.role).toLowerCase() : "";
        if (!item.roles.includes(userRole)) {
          return;
        }
      }
      const link = document.createElement("a");
      link.className = "top-nav-link" + (item.className ? " " + item.className : "");
      link.href = item.href;
      link.textContent = item.label;
      if (linksHost) {
        linksHost.appendChild(link);
      }
    });
  }

  function getUserText(user) {
    if (!user) return "当前未登录";
    var displayName = user.realName || user.username || "未知用户";
    var role = user.role || "student";
    return "当前用户：" + displayName + "（" + role + "）";
  }

  async function impersonateRole(role) {
    try {
      await Api.postJson("/api/auth/impersonate", { role: role || null });
      if (window.Auth && window.Auth.loadCurrentUser) {
        await window.Auth.loadCurrentUser(true);
      }
      var newUser = window.Auth ? window.Auth.getCurrentUser() : null;
      normalizeTopNavLinks(newUser);
      applyRoleBasedNavVisibility();
      setActiveNav();
      renderTopNavUserArea();
    } catch (_) {
      console.error("Impersonation failed");
    }
  }

  function renderTopNavUserArea() {
    var nav = document.querySelector(".top-nav");
    if (!nav) return;

    var area = nav.querySelector(".top-nav-user");
    if (!area) {
      area = document.createElement("div");
      area.className = "top-nav-user";
      nav.appendChild(area);
    }

    var auth = window.Auth;
    var user = auth ? auth.getCurrentUser() : null;

    var text = document.createElement("span");
    text.className = "top-nav-user-text";
    text.textContent = getUserText(user);

    var action = document.createElement("a");
    action.className = "top-nav-auth-link";

    area.innerHTML = "";
    area.appendChild(text);
    area.appendChild(action);

    if (!user) {
      action.href = "/login.html";
      action.textContent = "登录";
      return;
    }

    action.href = "#";
    action.textContent = "退出";
    action.addEventListener("click", function (e) {
      e.preventDefault();
      logout();
    });

    var isAdmin = String(user.role || "").toLowerCase() === "admin";
    if (isAdmin) {
      var switcher = document.createElement("select");
      switcher.className = "impersonate-switch";
      switcher.setAttribute("aria-label", "切换查看视角");
      switcher.title = "以不同角色视角查看前端效果";

      var impersonatedRole = user.impersonatedRole || "";
      var roles = [
        { value: "", label: "👤 管理员视角" },
        { value: "teacher", label: "🎓 教师视角" },
        { value: "student", label: "📚 学生视角" }
      ];

      roles.forEach(function (r) {
        var opt = document.createElement("option");
        opt.value = r.value;
        opt.textContent = r.label;
        if (impersonatedRole === r.value || (!impersonatedRole && !r.value)) {
          opt.selected = true;
        }
        switcher.appendChild(opt);
      });

      switcher.addEventListener("change", function () {
        impersonateRole(switcher.value || null);
      });

      area.appendChild(switcher);
    }
  }

  function applyRoleBasedNavVisibility() {
    const auth = window.Auth;
    const user = auth ? auth.getCurrentUser() : null;
    const role = user && user.role ? String(user.role).toLowerCase() : "";
    const materialsLinks = document.querySelectorAll('.top-nav-link[href="/materials.html"]');
    const devLinks = document.querySelectorAll('.top-nav-link[href="/dev.html"]');
    const materialsManageLinks = document.querySelectorAll('.top-nav-link[href="/materials-manage.html"]');

    const canSeeMaterials = true;
    const canSeeDev = role === "admin" || role === "teacher";
    const canSeeMaterialsManage = role === "admin" || role === "teacher";

    materialsLinks.forEach((link) => {
      link.style.display = canSeeMaterials ? "" : "none";
    });

    devLinks.forEach((link) => {
      link.style.display = canSeeDev ? "" : "none";
    });

    materialsManageLinks.forEach((link) => {
      link.style.display = canSeeMaterialsManage ? "" : "none";
    });
  }

  function showAccessDenied(containerId, message) {
    const denied = document.getElementById(containerId);
    if (!denied) return;
    denied.innerHTML =
      "<div class='card access-denied-card'>" +
      "<div class='section-title'>无权访问</div>" +
      "<div class='status error'>" + UI.escapeHtml(message) + "</div>" +
      "<a class='jump-link' href='/index.html'>返回首页 →</a>" +
      "</div>";
    denied.style.display = "block";
  }

  function guardMaterialsPage() {
    if (normalizePath(window.location.pathname) !== "/materials.html") return;
    const auth = window.Auth;
    const allowed = auth && auth.hasAnyRole && auth.hasAnyRole(["teacher", "admin"]);
    const managerArea = document.getElementById("materialsManagerArea");
    const listArea = document.getElementById("materialsListArea");
    const searchArea = document.getElementById("materialsSearchArea");
    const studentNotice = document.getElementById("materialsStudentNotice");

    if (allowed) {
      if (studentNotice) {
        studentNotice.style.display = "none";
      }
      return;
    }

    if (managerArea) {
      managerArea.remove();
    }
    if (listArea) {
      listArea.remove();
    }
    if (searchArea) {
      searchArea.remove();
    }
    if (studentNotice) {
      studentNotice.style.display = "block";
    }
  }

  function guardDevPage() {
    if (normalizePath(window.location.pathname) !== "/dev.html") return;
    const auth = window.Auth;
    const allowed = auth && auth.hasRole && auth.hasRole("admin");
    if (allowed) {
      return;
    }

    const managerArea = document.getElementById("devToolsArea");
    if (managerArea) {
      managerArea.remove();
    }
    showAccessDenied("devAccessDenied", "无权访问开发工具");
  }

  async function logout() {
    try {
      await Api.postJson("/api/auth/logout", {});
    } catch (_) {
      // Keep UX stable in development mode.
    }

    if (window.Auth && window.Auth.loadCurrentUser) {
      await window.Auth.loadCurrentUser(true);
    }

    const current = normalizePath(window.location.pathname);
    if (current === "/login.html") {
      renderTopNavUserArea();
      return;
    }

    window.location.href = "/login.html";
  }


  document.addEventListener("DOMContentLoaded", async function () {
    if (window.Auth && window.Auth.loadCurrentUser) {
      await window.Auth.loadCurrentUser();
    }
    const auth = window.Auth;
    const user = auth ? auth.getCurrentUser() : null;
    normalizeTopNavLinks(user);
    applyRoleBasedNavVisibility();
    setActiveNav();
    renderTopNavUserArea();
    guardMaterialsPage();
    guardDevPage();
  });
})();
