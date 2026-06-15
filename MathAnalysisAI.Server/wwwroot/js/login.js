(function () {
  var authMode = null;
  var showPasswordField = false;

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

  function detectAuthMode() {
    var me = window.Auth && window.Auth.getCurrentUser ? window.Auth.getCurrentUser() : null;
    var fallbackApplied = window.Auth && window.Auth.isDevelopmentFallbackApplied
      ? window.Auth.isDevelopmentFallbackApplied()
      : false;

    if (me && !fallbackApplied) {
      authMode = "authenticated";
      return;
    }

    if (fallbackApplied) {
      authMode = "development_fallback";
      showPasswordField = false;
      return;
    }

    if (window.location.hostname === "localhost" ||
        window.location.hostname === "127.0.0.1" ||
        window.location.hostname === "::1") {
      authMode = "development";
      showPasswordField = false;
      return;
    }

    authMode = "unknown";
    showPasswordField = false;
  }

  function applyAuthModeUI() {
    var passwordGroup = UI.qs("#passwordFieldGroup");
    var quickBtn = UI.qs("#loginQuickBtn");
    var registerSection = UI.qs("#registerSection");
    var modeInfoSection = UI.qs("#modeInfoSection");
    var modeInfoContent = UI.qs("#modeInfoContent");
    var pageHint = UI.qs("#loginPageHint");

    if (authMode === "authenticated") {
      if (pageHint) pageHint.textContent = "你已登录，可以正常使用所有功能。";
      if (passwordGroup) passwordGroup.style.display = "none";
      if (quickBtn) quickBtn.style.display = "none";
      if (registerSection) registerSection.style.display = "none";
      return;
    }

    if (authMode === "development_fallback" || authMode === "development") {
      if (pageHint) pageHint.textContent = "开发环境 - 输入用户名即可登录，也支持注册新账号。";
      showPasswordField = false;
      if (passwordGroup) passwordGroup.style.display = "none";
      if (quickBtn) quickBtn.style.display = "";
      if (registerSection) registerSection.style.display = "";
      return;
    }

    showPasswordField = true;
    if (passwordGroup) passwordGroup.style.display = "";
    if (quickBtn) quickBtn.style.display = "none";
    if (pageHint) pageHint.textContent = "请输入用户名和密码登录。";

    if (modeInfoSection && modeInfoContent) {
      modeInfoSection.style.display = "";
      modeInfoContent.textContent = "当前服务器可能采用密码认证模式。如果你还没有账号，请联系管理员。";
    }

    if (registerSection) registerSection.style.display = "none";
  }

  async function doLogin(username, password) {
    var status = UI.qs("#loginStatus");
    var loginBtn = UI.qs("#loginSubmitBtn");
    var quickBtn = UI.qs("#loginQuickBtn");

    var value = (username || "").trim();
    if (!value) {
      UI.showStatus(status, "请输入用户名。", true);
      return;
    }

    loginBtn.disabled = true;
    if (quickBtn) quickBtn.disabled = true;
    UI.showStatus(status, "正在登录……", false);

    var payload = { username: value };
    if (showPasswordField) {
      payload.password = (password || "").trim();
    }

    try {
      await Api.postJson("/api/auth/login", payload);
      if (window.Auth && window.Auth.loadCurrentUser) {
        await window.Auth.loadCurrentUser(true);
      }
      var user = window.Auth && window.Auth.getCurrentUser ? window.Auth.getCurrentUser() : null;
      renderUserInfo(user);
      UI.showStatus(status, "登录成功，正在进入首页……", false);
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

    if (!password) {
      UI.showStatus(status, "请输入密码。", true);
      return;
    }

    registerBtn.disabled = true;
    UI.showStatus(status, "正在注册……", false);

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
      UI.showStatus(status, "注册成功，正在进入首页……", false);
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

    if (loginBtn) {
      loginBtn.addEventListener("click", loginWithCredentials);
    }

    if (quickBtn) {
      quickBtn.addEventListener("click", quickLoginTestStudent);
    }

    if (registerBtn) {
      registerBtn.addEventListener("click", doRegister);
    }

    var usernameInput = UI.qs("#loginUsernameInput");
    if (usernameInput) {
      usernameInput.addEventListener("keydown", function (evt) {
        if (evt.key === "Enter") {
          evt.preventDefault();
          loginWithCredentials();
        }
      });
    }

    var passwordInput = UI.qs("#loginPasswordInput");
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

    if (window.Auth && window.Auth.loadCurrentUser) {
      await window.Auth.loadCurrentUser();
      renderUserInfo(window.Auth.getCurrentUser());
    }

    detectAuthMode();
    applyAuthModeUI();
    bindEvents();
  }

  window.loginWithCredentials = loginWithCredentials;
  window.quickLoginTestStudent = quickLoginTestStudent;

  document.addEventListener("DOMContentLoaded", function () {
    initLoginPage();
  });
})();
