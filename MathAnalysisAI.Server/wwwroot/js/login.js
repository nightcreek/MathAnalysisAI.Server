(function () {
  var authMode = null;
  var showPasswordField = false;
  var MIN_PASSWORD_LENGTH = 4;

  function renderUserInfo(user) {
    var info = UI.qs("#loginUserInfo");
    if (!info) return;

    if (!user) {
      UI.setText(info, "");
      return;
    }

    var displayName = user.realName || user.username || "未知用户";
    var role = user.role || "student";
    UI.setText(info, "当前用户：" + displayName + "（" + role + "）");
  }

  function isLocalhost(hostname) {
    return hostname === "localhost"
      || hostname === "127.0.0.1"
      || hostname === "::1";
  }

  /**
   * 同时按客户端检测（localhost）与服务端检测（API 返回模式）
   * 来决定是否显示密码字段。
   */
  async function detectAuthMode() {
    if (window.Auth && window.Auth.loadCurrentUser) {
      try { await window.Auth.loadCurrentUser(); } catch (_) {}
    }
    var user = window.Auth && window.Auth.getCurrentUser ? window.Auth.getCurrentUser() : null;
    var fallbackApplied = window.Auth && window.Auth.isDevelopmentFallbackApplied
      ? window.Auth.isDevelopmentFallbackApplied()
      : false;

    if (user && !fallbackApplied) {
      authMode = "authenticated";
      return;
    }

    var serverMode = null;
    try {
      var info = await Api.getJson("/api/auth/info");
      if (info && info.mode) {
        serverMode = String(info.mode).toLowerCase();
      }
    } catch (_) {
      // 旧版本后端可能没有这个接口，忽略即可。
    }

    // 服务端明确声明密码模式 -> 必须输入密码
    if (serverMode === "local_password" || serverMode === "password") {
      authMode = "password_required";
      showPasswordField = true;
      return;
    }

    // 服务端声明为开发用户名模式 -> 仅用户名
    if (serverMode === "development_username" || serverMode === "user_only") {
      authMode = "development_username";
      showPasswordField = false;
      return;
    }

    // 服务端禁用 / 不可用 -> 当作只读/无登录
    if (serverMode === "disabled") {
      authMode = "disabled";
      showPasswordField = false;
      return;
    }

    // 没有可靠的服务端指示 -> 以 hostname 作为兜底
    if (isLocalhost(window.location.hostname)) {
      authMode = "development";
      showPasswordField = false;
      return;
    }

    authMode = "password_required";
    showPasswordField = true;
  }

  function applyAuthModeUI() {
    var passwordGroup = UI.qs("#passwordFieldGroup");
    var quickBtn = UI.qs("#loginQuickBtn");
    var registerSection = UI.qs("#registerSection");
    var pageHint = UI.qs("#loginPageHint");
    var usernameInput = UI.qs("#loginUsernameInput");

    if (authMode === "authenticated") {
      if (pageHint) pageHint.textContent = "你已登录，可以正常使用所有功能。";
      if (passwordGroup) passwordGroup.style.display = "none";
      if (quickBtn) quickBtn.style.display = "none";
      if (registerSection) registerSection.style.display = "none";
      return;
    }

    if (authMode === "disabled") {
      if (pageHint) pageHint.textContent = "当前部署未启用登录入口，请联系管理员。";
      if (passwordGroup) passwordGroup.style.display = "none";
      if (quickBtn) quickBtn.style.display = "none";
      if (registerSection) registerSection.style.display = "none";
      return;
    }

    if (authMode === "password_required") {
      if (pageHint) pageHint.textContent = "请使用用户名和密码登录。";
      if (passwordGroup) passwordGroup.style.display = "";
      if (quickBtn) quickBtn.style.display = "none";
      if (registerSection) registerSection.style.display = "";
      if (usernameInput) usernameInput.setAttribute("autocomplete", "username");
      return;
    }

    // development_username 或 development
    if (pageHint) pageHint.textContent = "开发环境 - 输入用户名即可登录，也支持注册新账号。";
    showPasswordField = false;
    if (passwordGroup) passwordGroup.style.display = "none";
    if (quickBtn) quickBtn.style.display = "";
    if (registerSection) registerSection.style.display = "";
  }

  async function doLogin(username, password) {
    var status = UI.qs("#loginStatus");
    var loginBtn = UI.qs("#loginSubmitBtn");
    var quickBtn = UI.qs("#loginQuickBtn");

    var usernameValue = (username || "").trim();
    var passwordValue = (password || "").trim();

    if (!usernameValue) {
      UI.showStatus(status, "请输入用户名。", true);
      return;
    }

    if (showPasswordField && !passwordValue) {
      UI.showStatus(status, "请输入密码。", true);
      return;
    }
    if (showPasswordField && passwordValue.length < MIN_PASSWORD_LENGTH) {
      UI.showStatus(status, "密码长度不足 " + MIN_PASSWORD_LENGTH + " 位。", true);
      return;
    }

    loginBtn.disabled = true;
    if (quickBtn) quickBtn.disabled = true;
    UI.showStatus(status, "正在登录…", false);

    var payload = { username: usernameValue };
    if (showPasswordField) {
      payload.password = passwordValue;
    }

    try {
      await Api.postJson("/api/auth/login", payload);
      if (window.Auth && window.Auth.loadCurrentUser) {
        await window.Auth.loadCurrentUser(true);
      }
      var user = window.Auth && window.Auth.getCurrentUser ? window.Auth.getCurrentUser() : null;
      renderUserInfo(user);
      UI.showStatus(status, "登录成功，正在进入首页…", false);
      setTimeout(function () {
        window.location.href = "/index.html";
      }, 300);
    } catch (err) {
      UI.showStatus(status, UI.formatApiErrorMessage(err, "login"), true);
    } finally {
      loginBtn.disabled = false;
      if (quickBtn) quickBtn.disabled = false;
    }
  }

  function loginWithCredentials() {
    var input = UI.qs("#loginUsernameInput");
    var passwordInput = UI.qs("#loginPasswordInput");
    var username = input ? input.value : "";
    var password = passwordInput ? passwordInput.value : "";
    doLogin(username, password);
  }

  function quickLoginTestStudent() {
    doLogin("test_student", "");
  }

  async function doRegister() {
    var status = UI.qs("#registerStatus");
    var registerBtn = UI.qs("#registerSubmitBtn");

    var username = (UI.qs("#registerUsernameInput")?.value || "").trim();
    var password = (UI.qs("#registerPasswordInput")?.value || "").trim();
    var realName = (UI.qs("#registerRealNameInput")?.value || "").trim();
    var studentNumber = (UI.qs("#registerStudentNumberInput")?.value || "").trim();

    if (!username) {
      UI.showStatus(status, "请输入用户名。", true);
      return;
    }

    if (!password || password.length < MIN_PASSWORD_LENGTH) {
      UI.showStatus(status, "请输入至少 " + MIN_PASSWORD_LENGTH + " 位的密码。", true);
      return;
    }

    registerBtn.disabled = true;
    UI.showStatus(status, "正在注册…", false);

    var payload = {
      username: username,
      password: password,
      realName: realName || null,
      studentNumber: studentNumber || null
    };

    try {
      await Api.postJson("/api/auth/register", payload);
      if (window.Auth && window.Auth.loadCurrentUser) {
        await window.Auth.loadCurrentUser(true);
      }
      var user = window.Auth && window.Auth.getCurrentUser ? window.Auth.getCurrentUser() : null;
      renderUserInfo(user);
      UI.showStatus(status, "注册成功，正在进入首页…", false);
      setTimeout(function () {
        window.location.href = "/index.html";
      }, 300);
    } catch (err) {
      UI.showStatus(status, UI.formatApiErrorMessage(err, "register"), true);
    } finally {
      registerBtn.disabled = false;
    }
  }

  function bindEvents() {
    var loginBtn = UI.qs("#loginSubmitBtn");
    var quickBtn = UI.qs("#loginQuickBtn");
    var registerBtn = UI.qs("#registerSubmitBtn");

    if (loginBtn) loginBtn.addEventListener("click", loginWithCredentials);
    if (quickBtn) quickBtn.addEventListener("click", quickLoginTestStudent);
    if (registerBtn) registerBtn.addEventListener("click", doRegister);

    var usernameInput = UI.qs("#loginUsernameInput");
    var passwordInput = UI.qs("#loginPasswordInput");
    if (usernameInput) {
      usernameInput.addEventListener("keydown", function (evt) {
        if (evt.key === "Enter") {
          evt.preventDefault();
          if (showPasswordField && passwordInput) {
            passwordInput.focus();
          } else {
            loginWithCredentials();
          }
        }
      });
    }
    if (passwordInput) {
      passwordInput.addEventListener("keydown", function (evt) {
        if (evt.key === "Enter") {
          evt.preventDefault();
          loginWithCredentials();
        }
      });
    }
  }

  async function initLoginPage() {
    if (!UI.qs("#loginPageRoot")) return;
    bindEvents();
    await detectAuthMode();
    applyAuthModeUI();

    if (window.Auth && window.Auth.getCurrentUser) {
      renderUserInfo(window.Auth.getCurrentUser());
    }
  }

  window.loginWithCredentials = loginWithCredentials;
  window.quickLoginTestStudent = quickLoginTestStudent;

  document.addEventListener("DOMContentLoaded", function () {
    initLoginPage();
  });
})();
