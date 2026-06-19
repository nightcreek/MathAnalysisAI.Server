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
    { href: "/index.html", label: "首页" },
    { href: "/analysis.html", label: "解题分析" },
    { href: "/questions.html", label: "题库" },
    { href: "/materials.html", label: "课程资料" },
    { href: "/leaderboard.html", label: "排行榜" },
    { href: "/stats.html", label: "学习统计" },
    { href: "/dev.html", label: "开发工具", roles: ["admin", "teacher"] },
    { href: "/materials-manage.html", label: "资料管理", roles: ["teacher", "admin"] },
    { href: "/resources-manage.html", label: "网络资源", roles: ["teacher", "admin"] },
    { href: "/admin.html", label: "管理", roles: ["admin"] }
  ];

  function getCurrentNavLabel() {
    var current = normalizePath(window.location.pathname);
    var matched = standardNavLinks.find(function (item) {
      return item.href === current;
    });
    return matched ? matched.label : "当前页面";
  }

  function normalizePath(pathname) {
    if (!pathname || pathname === "/") return "/index.html";
    return routeMap[pathname] || pathname;
  }

  function setActiveNav() {
    var current = normalizePath(window.location.pathname);
    document.querySelectorAll(".top-nav-link").forEach(function (link) {
      link.classList.toggle("active", (link.getAttribute("href") || "") === current);
    });
  }

  function normalizeTopNavLinks(user) {
    var nav = document.querySelector(".top-nav");
    if (!nav) return;
    var currentLabel = getCurrentNavLabel();

    nav.innerHTML =
      "<div class='top-nav-head'>" +
      "<div class='brand'>" +
      "<a class='brand-link' href='/index.html'>数学分析智能体</a>" +
      "<span class='brand-subtitle'>MathAnalysisAI</span>" +
      "</div>" +
      "<div class='top-nav-head-actions'>" +
      "<span class='top-nav-current' aria-current='page'>" + currentLabel + "</span>" +
      "<button type='button' class='mobile-nav-toggle' aria-expanded='false' aria-label='打开导航菜单'>菜单</button>" +
      "</div>" +
      "</div>" +
      "<div class='nav-links'></div>" +
      "<div class='top-nav-user'></div>";

    var linksHost = nav.querySelector(".nav-links");
    var userRole = user && user.role ? String(user.role).toLowerCase() : "";

    standardNavLinks.forEach(function (item) {
      if (item.roles && item.roles.length > 0 && !item.roles.includes(userRole)) {
        return;
      }
      var link = document.createElement("a");
      link.className = "top-nav-link";
      link.href = item.href;
      link.textContent = item.label;
      if (linksHost) {
        linksHost.appendChild(link);
      }
    });
  }

  function setMobileNavOpenState(nav, isOpen) {
    if (!nav) return;

    var isMobile = window.matchMedia("(max-width: 768px)").matches;
    var toggle = nav.querySelector(".mobile-nav-toggle");
    var linksHost = nav.querySelector(".nav-links");
    var userArea = nav.querySelector(".top-nav-user");

    nav.classList.toggle("nav-collapsed", isMobile);
    nav.classList.toggle("nav-open", isMobile && !!isOpen);

    if (toggle) {
      toggle.hidden = !isMobile;
      toggle.setAttribute("aria-expanded", isMobile && !!isOpen ? "true" : "false");
      toggle.textContent = isMobile && !!isOpen ? "收起" : "菜单";
      toggle.setAttribute("aria-label", isMobile && !!isOpen ? "收起导航菜单" : "打开导航菜单");
    }

    if (linksHost) {
      linksHost.hidden = isMobile && !isOpen;
    }

    if (userArea) {
      userArea.hidden = isMobile && !isOpen;
    }
  }

  function ensureMobileNavBinding() {
    var nav = document.querySelector(".top-nav");
    if (!nav || nav.__mobileNavBound) {
      setMobileNavOpenState(nav, nav && nav.classList.contains("nav-open"));
      return;
    }

    nav.addEventListener("click", function (event) {
      var toggle = event.target.closest(".mobile-nav-toggle");
      if (toggle) {
        var shouldOpen = !nav.classList.contains("nav-open");
        setMobileNavOpenState(nav, shouldOpen);
        return;
      }

      if (window.matchMedia("(max-width: 768px)").matches && event.target.closest(".top-nav-link, .top-nav-auth-link")) {
        setMobileNavOpenState(nav, false);
      }
    });

    var resizeHandler = function () {
      if (window.matchMedia("(max-width: 768px)").matches) {
        setMobileNavOpenState(nav, nav.classList.contains("nav-open"));
      } else {
        setMobileNavOpenState(nav, false);
      }
    };

    window.addEventListener("resize", resizeHandler);
    nav.__mobileNavBound = true;
    nav.__mobileNavResizeHandler = resizeHandler;
    setMobileNavOpenState(nav, false);
  }

  function getUserText(user) {
    if (!user) {
      var authState = window.Auth && window.Auth.getLastAuthState ? window.Auth.getLastAuthState() : null;
      if (authState && authState.type === "expired") {
        return "当前登录已过期";
      }
      return "当前未登录";
    }
    var displayName = user.realName || user.username || "未知用户";
    var role = user.role || "student";
    return "当前用户：" + displayName + "（" + role + "）";
  }

  async function impersonateRole(role) {
    try {
      await Api.postJson("/api/auth/impersonate", { role: role || null });
      if (window.Auth && window.Auth.setImpersonatedRole) {
        window.Auth.setImpersonatedRole(role || "");
      }
      var newUser = window.Auth ? window.Auth.getCurrentUser() : null;
      normalizeTopNavLinks(newUser);
      setActiveNav();
      renderTopNavUserArea();
      ensureMobileNavBinding();
    } catch (err) {
      console.error("Impersonation failed:", err);
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
    var actualUser = auth && auth.getActualCurrentUser ? auth.getActualCurrentUser() : user;

    area.innerHTML = "";

    var text = document.createElement("span");
    text.className = "top-nav-user-text";
    text.textContent = getUserText(user);
    area.appendChild(text);

    var action = document.createElement("a");
    action.className = "top-nav-auth-link";
    area.appendChild(action);

    if (!user) {
      action.href = "/login.html";
      action.textContent = (auth && auth.getLastAuthState && auth.getLastAuthState() && auth.getLastAuthState().type === "expired")
        ? "重新登录"
        : "登录";
      return;
    }

    action.href = "#";
    action.textContent = "退出";
    action.addEventListener("click", function (e) {
      e.preventDefault();
      logout();
    });

    var isAdmin = actualUser && String(actualUser.role || "").toLowerCase() === "admin";
    if (!isAdmin) {
      return;
    }

    var switcher = document.createElement("select");
    switcher.className = "impersonate-switch";
    switcher.setAttribute("aria-label", "切换查看视角");
    switcher.title = "以不同角色视角查看前端效果";

    var impersonatedRole = auth && auth.getImpersonatedRole ? auth.getImpersonatedRole() : "";
    [
      { value: "", label: "👤 管理员视角" },
      { value: "teacher", label: "🎓 教师视角" },
      { value: "student", label: "📚 学生视角" }
    ].forEach(function (role) {
      var opt = document.createElement("option");
      opt.value = role.value;
      opt.textContent = role.label;
      if (impersonatedRole === role.value || (!impersonatedRole && !role.value)) {
        opt.selected = true;
      }
      switcher.appendChild(opt);
    });

    switcher.addEventListener("change", function () {
      impersonateRole(switcher.value || "");
    });

    area.appendChild(switcher);
  }

  function showAccessDenied(containerId, message) {
    var denied = document.getElementById(containerId);
    if (!denied) return;
    denied.innerHTML =
      "<div class='card access-denied-card'>" +
      "<div class='section-title'>无权访问</div>" +
      "<div class='status error'>" + UI.escapeHtml(message) + "</div>" +
      "<a class='jump-link' href='/index.html'>返回首页 →</a>" +
      "</div>";
    denied.style.display = "block";
  }

  function guardDevPage() {
    if (normalizePath(window.location.pathname) !== "/dev.html") return;
    var auth = window.Auth;
    var allowed = auth && auth.hasRole && auth.hasRole("admin");
    if (allowed) return;

    var managerArea = document.getElementById("devToolsArea");
    if (managerArea) {
      managerArea.remove();
    }
    showAccessDenied("devAccessDenied", "无权访问开发工具");
  }

  async function logout() {
    if (window.Auth && window.Auth.logout) {
      await window.Auth.logout();
    }

    var current = normalizePath(window.location.pathname);
    if (current === "/login.html") {
      renderTopNavUserArea();
      return;
    }

    window.location.href = "/login.html";
  }

  async function bootstrapNavigation() {
    var nav = document.querySelector(".top-nav");
    if (nav) {
      nav.setAttribute("aria-busy", "true");
    }

    try {
      if (window.Auth && window.Auth.loadCurrentUser) {
        await window.Auth.loadCurrentUser(true);
      }
    } catch (err) {
      console.error("[Nav] Failed to hydrate auth state before rendering navigation.", err);
    }

    var auth = window.Auth;
    var user = auth ? auth.getCurrentUser() : null;
    normalizeTopNavLinks(user);
    setActiveNav();
    renderTopNavUserArea();
    ensureMobileNavBinding();
    guardDevPage();
    document.body.setAttribute("data-auth-hydrated", "true");
    if (nav) {
      nav.setAttribute("aria-busy", "false");
    }
  }

  document.addEventListener("DOMContentLoaded", function () {
    document.body.setAttribute("data-auth-hydrated", "false");
    bootstrapNavigation().catch(function (err) {
      console.error("[Nav] Navigation bootstrap failed.", err);
      var nav = document.querySelector(".top-nav");
      if (nav) {
        nav.setAttribute("aria-busy", "false");
      }
      document.body.setAttribute("data-auth-hydrated", "true");
    });
  });
})();
