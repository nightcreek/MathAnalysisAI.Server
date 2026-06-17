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
    } catch (_) {}

    if (serverMode === "local_password" || serverMode === "password") {
      authMode = "password_required";
      showPasswordField = true;
      return;
    }

    if (serverMode === "development_username" || serverMode === "user_only") {
      authMode = "development_username";
      showPasswordField = false;
      return;
    }

    if (serverMode === "disabled") {
      authMode = "disabled";
      showPasswordField = false;
      return;
    }

    authMode = "password_required";
    showPasswordField = true;
  }

  function applyAuthModeUI() {
    var passwordGroup = UI.qs("#passwordFieldGroup");
    var registerSection = UI.qs("#registerSection");
    var pageHint = UI.qs("#loginPageHint");
    var usernameInput = UI.qs("#loginUsernameInput");
    var modeSwitch = UI.qs("#loginModeSwitch");

    if (authMode === "authenticated") {
      if (pageHint) pageHint.textContent = "你已登录，可以正常使用所有功能。";
      if (passwordGroup) passwordGroup.style.display = "none";
      if (registerSection) registerSection.style.display = "none";
      if (modeSwitch) modeSwitch.style.display = "none";
      return;
    }

    if (authMode === "disabled") {
      if (pageHint) pageHint.textContent = "当前部署未启用登录入口，请联系管理员。";
      if (passwordGroup) passwordGroup.style.display = "none";
      if (registerSection) registerSection.style.display = "none";
      if (modeSwitch) modeSwitch.style.display = "none";
      return;
    }

    if (modeSwitch) modeSwitch.style.display = "";

    if (authMode === "password_required") {
      if (pageHint) pageHint.textContent = "请使用用户名和密码登录。";
      if (passwordGroup) passwordGroup.style.display = "";
      if (usernameInput) usernameInput.setAttribute("autocomplete", "username");
      return;
    }

    if (pageHint) pageHint.textContent = "开发环境 - 输入用户名即可登录，也支持注册新账号。";
    showPasswordField = false;
    if (passwordGroup) passwordGroup.style.display = "none";
  }

  function switchTab(tab) {
    var loginSection = UI.qs("#loginSection");
    var registerSection = UI.qs("#registerSection");
    var modeSwitch = UI.qs("#loginModeSwitch");
    var loginStatus = UI.qs("#loginStatus");
    var registerStatus = UI.qs("#registerStatus");

    if (modeSwitch) {
      var buttons = modeSwitch.querySelectorAll(".mode-switch-btn");
      buttons.forEach(function (btn) {
        if (btn.getAttribute("data-tab") === tab) {
          btn.classList.add("is-active");
        } else {
          btn.classList.remove("is-active");
        }
      });
    }

    if (tab === "register") {
      if (loginSection) loginSection.style.display = "none";
      if (registerSection) registerSection.style.display = "";
      if (loginStatus) UI.showStatus(loginStatus, "", false);
    } else {
      if (loginSection) loginSection.style.display = "";
      if (registerSection) registerSection.style.display = "none";
      if (registerStatus) UI.showStatus(registerStatus, "", false);
    }
  }

  function togglePasswordVisibility(toggleBtn, passwordInput) {
    if (!passwordInput) return;
    var showing = passwordInput.type === "password";
    passwordInput.type = showing ? "text" : "password";
    if (toggleBtn) {
      toggleBtn.setAttribute("aria-label", showing ? "隐藏密码" : "显示密码");
      var svg = toggleBtn.querySelector("svg");
      if (svg) {
        if (showing) {
          svg.innerHTML = '<path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"></path><line x1="1" y1="1" x2="23" y2="23"></line>';
        } else {
          svg.innerHTML = '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path><circle cx="12" cy="12" r="3"></circle>';
        }
      }
    }
  }

  async function doLogin(username, password) {
    var status = UI.qs("#loginStatus");
    var loginBtn = UI.qs("#loginSubmitBtn");

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
    UI.showStatus(status, "正在登录…", false);

    var payload = { username: usernameValue };
    if (showPasswordField) {
      payload.password = passwordValue;
    }

    try {
      var result = await Api.postJson("/api/auth/login", payload);

      if (window.Auth && window.Auth.loadCurrentUser) {
        await window.Auth.loadCurrentUser(true);
      }

      var user = window.Auth && window.Auth.getCurrentUser ? window.Auth.getCurrentUser() : null;
      renderUserInfo(user);
      UI.showStatus(status, "登录成功，正在跳转…", false);

      setTimeout(function () {
        window.location.href = "/index.html";
      }, 500);
    } catch (err) {
      var msg = UI.formatApiErrorMessage(err, "login");
      if (!err.status && err.message && err.message.toLowerCase().indexOf("failed to fetch") !== -1) {
        msg = "无法连接服务器，请确认后端服务已启动。";
      }
      UI.showStatus(status, msg, true);
    } finally {
      loginBtn.disabled = false;
    }
  }

  function loginWithCredentials() {
    var input = UI.qs("#loginUsernameInput");
    var passwordInput = UI.qs("#loginPasswordInput");
    var username = input ? input.value : "";
    var password = passwordInput ? passwordInput.value : "";
    doLogin(username, password);
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
      UI.showStatus(status, "注册成功，正在跳转…", false);
      setTimeout(function () {
        window.location.href = "/index.html";
      }, 500);
    } catch (err) {
      var msg = UI.formatApiErrorMessage(err, "register");
      if (!err.status && err.message && err.message.toLowerCase().indexOf("failed to fetch") !== -1) {
        msg = "无法连接服务器，请确认后端服务已启动。";
      }
      UI.showStatus(status, msg, true);
    } finally {
      registerBtn.disabled = false;
    }
  }

  function bindEvents() {
    var modeSwitch = UI.qs("#loginModeSwitch");
    if (modeSwitch) {
      modeSwitch.addEventListener("click", function (evt) {
        var btn = evt.target.closest(".mode-switch-btn");
        if (!btn) return;
        var tab = btn.getAttribute("data-tab");
        if (tab) switchTab(tab);
      });
    }

    var loginBtn = UI.qs("#loginSubmitBtn");
    if (loginBtn) loginBtn.addEventListener("click", loginWithCredentials);

    var registerBtn = UI.qs("#registerSubmitBtn");
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

    var registerUsernameInput = UI.qs("#registerUsernameInput");
    var registerPasswordInput = UI.qs("#registerPasswordInput");
    if (registerUsernameInput) {
      registerUsernameInput.addEventListener("keydown", function (evt) {
        if (evt.key === "Enter") {
          evt.preventDefault();
          if (registerPasswordInput) registerPasswordInput.focus();
        }
      });
    }
    if (registerPasswordInput) {
      registerPasswordInput.addEventListener("keydown", function (evt) {
        if (evt.key === "Enter") {
          evt.preventDefault();
          doRegister();
        }
      });
    }

    var loginToggle = UI.qs("#loginPasswordToggle");
    if (loginToggle) {
      loginToggle.addEventListener("click", function () {
        togglePasswordVisibility(loginToggle, UI.qs("#loginPasswordInput"));
      });
    }
    var registerToggle = UI.qs("#registerPasswordToggle");
    if (registerToggle) {
      registerToggle.addEventListener("click", function () {
        togglePasswordVisibility(registerToggle, UI.qs("#registerPasswordInput"));
      });
    }
  }

  async function initLoginPage() {
    if (!UI.qs("#loginPageRoot")) return;
    bindEvents();
    await detectAuthMode();
    applyAuthModeUI();
    switchTab("login");

    if (window.Auth && window.Auth.getCurrentUser) {
      renderUserInfo(window.Auth.getCurrentUser());
    }
  }

  document.addEventListener("DOMContentLoaded", function () {
    initLoginPage();
  });
})();
