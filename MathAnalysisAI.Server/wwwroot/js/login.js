(function () {
  var authMode = null;
  var authInfo = null;
  var showPasswordField = false;
  var authModeErrorMessage = "";

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

  function renderModeInfo() {
    var section = UI.qs("#modeInfoSection");
    var content = UI.qs("#modeInfoContent");
    if (!section || !content) return;

    if (!authInfo) {
      section.style.display = "none";
      return;
    }

    var mode = String(authInfo.mode || "").toLowerCase();
    if (mode === "oidc" && authInfo.oidc) {
      var scopes = Array.isArray(authInfo.oidc.scopes) ? authInfo.oidc.scopes.join(" ") : "";
      content.textContent = "统一认证已启用。Authority: " + (authInfo.oidc.authority || "-") + " ClientId: " + (authInfo.oidc.clientId || "-") + " Scopes: " + scopes;
      section.style.display = "block";
      return;
    }

    if (mode === "developmentusername") {
      content.textContent = "当前为开发期用户名登录模式。";
      section.style.display = "block";
      return;
    }

    if (mode === "localpassword") {
      content.textContent = "当前为本地用户名密码登录模式。";
      section.style.display = "block";
      return;
    }

    section.style.display = "none";
  }

  async function detectAuthMode() {
    if (window.Auth && window.Auth.loadCurrentUser) {
      await window.Auth.loadCurrentUser(true);
    }

    var user = window.Auth && window.Auth.getCurrentUser ? window.Auth.getCurrentUser() : null;
    if (user) {
      authMode = "authenticated";
      return;
    }

    try {
      authInfo = await window.Auth.loadAuthInfo(true);
    } catch (err) {
      authMode = "unavailable";
      authModeErrorMessage = UI.formatApiErrorMessage(err, "login");
      return;
    }

    var serverMode = String((authInfo && authInfo.mode) || "").toLowerCase();
    if (serverMode === "oidc") {
      authMode = "oidc";
      showPasswordField = false;
      return;
    }

    if (serverMode === "localpassword") {
      authMode = "password_required";
      showPasswordField = true;
      return;
    }

    if (serverMode === "developmentusername") {
      authMode = "development_username";
      showPasswordField = false;
      return;
    }

    if (serverMode === "disabled") {
      authMode = "disabled";
      showPasswordField = false;
      return;
    }

    authMode = "unavailable";
    authModeErrorMessage = "当前认证模式无法识别，请联系管理员检查配置。";
  }

  function readPersistedAccessToken() {
    try {
      return sessionStorage.getItem(window.MathAnalysisAuthStorageKeys.accessToken) || "";
    } catch (err) {
      console.error("[Login] Failed to read persisted access token from sessionStorage.", err);
      throw new Error("浏览器无法保存登录状态，请检查会话存储设置后重试。");
    }
  }

  async function persistAuthenticatedSession(result, source) {
    var accessToken = result && result.accessToken ? String(result.accessToken).trim() : "";
    if (!accessToken) {
      throw new Error("服务器未返回有效的 accessToken。");
    }

    console.info("[Login] " + source + " token length:", accessToken.length);

    var persisted = window.Auth.setAccessToken(accessToken, result.expiresAtUtc);
    var storedToken = readPersistedAccessToken();
    if (!persisted || storedToken !== accessToken) {
      console.error("[Login] Access token persistence verification failed.", {
        source: source,
        persisted: persisted,
        storedTokenLength: storedToken.length,
        accessTokenLength: accessToken.length
      });
      if (window.Auth && window.Auth.clearAccessToken) {
        window.Auth.clearAccessToken();
      }
      throw new Error("登录状态保存失败，请检查浏览器会话存储后重试。");
    }

    await window.Auth.loadCurrentUser(true);
    if (!window.Auth.getCurrentUser || !window.Auth.getCurrentUser()) {
      throw new Error("登录成功，但用户信息加载失败。");
    }
  }

  function applyAuthModeUI() {
    var loginSection = UI.qs("#loginSection");
    var passwordGroup = UI.qs("#passwordFieldGroup");
    var registerSection = UI.qs("#registerSection");
    var pageHint = UI.qs("#loginPageHint");
    var loginBtn = UI.qs("#loginSubmitBtn");
    var modeSwitch = UI.qs("#loginModeSwitch");
    var loginFormGrid = UI.qs("#loginSection .form-grid");

    renderModeInfo();

    if (authMode === "authenticated") {
      if (pageHint) pageHint.textContent = "你已登录，可以直接进入系统。";
      if (passwordGroup) passwordGroup.style.display = "none";
      if (registerSection) registerSection.style.display = "none";
      if (modeSwitch) modeSwitch.style.display = "none";
      if (loginBtn) loginBtn.textContent = "进入首页";
      return;
    }

    if (authMode === "disabled") {
      if (pageHint) pageHint.textContent = "当前部署未启用登录入口，请联系管理员。";
      if (loginFormGrid) loginFormGrid.style.display = "none";
      if (registerSection) registerSection.style.display = "none";
      if (modeSwitch) modeSwitch.style.display = "none";
      if (loginBtn) loginBtn.style.display = "none";
      return;
    }

    if (authMode === "oidc") {
      if (pageHint) pageHint.textContent = "当前部署使用统一认证。点击下方按钮跳转到认证中心登录。";
      if (loginFormGrid) loginFormGrid.style.display = "none";
      if (passwordGroup) passwordGroup.style.display = "none";
      if (registerSection) registerSection.style.display = "none";
      if (modeSwitch) modeSwitch.style.display = "none";
      if (loginBtn) {
        loginBtn.textContent = "使用统一认证登录";
        loginBtn.style.display = "";
      }
      return;
    }

    if (authMode === "unavailable") {
      if (pageHint) pageHint.textContent = authModeErrorMessage || "认证配置加载失败，请稍后重试。";
      if (loginFormGrid) loginFormGrid.style.display = "none";
      if (registerSection) registerSection.style.display = "none";
      if (modeSwitch) modeSwitch.style.display = "none";
      if (loginBtn) loginBtn.style.display = "none";
      return;
    }

    if (modeSwitch) modeSwitch.style.display = "";
    if (loginFormGrid) loginFormGrid.style.display = "";

    if (authMode === "password_required") {
      if (pageHint) pageHint.textContent = "请使用用户名和密码登录。";
      if (passwordGroup) passwordGroup.style.display = "";
      if (loginBtn) loginBtn.textContent = "登录";
      return;
    }

    if (pageHint) pageHint.textContent = "开发环境 - 输入用户名即可登录，也支持注册新账号。";
    showPasswordField = false;
    if (passwordGroup) passwordGroup.style.display = "none";
    if (loginBtn) loginBtn.textContent = "登录";
  }

  function switchTab(tab) {
    var loginSection = UI.qs("#loginSection");
    var registerSection = UI.qs("#registerSection");
    var modeSwitch = UI.qs("#loginModeSwitch");
    var loginStatus = UI.qs("#loginStatus");
    var registerStatus = UI.qs("#registerStatus");

    if (!modeSwitch || modeSwitch.style.display === "none") {
      return;
    }

    var buttons = modeSwitch.querySelectorAll(".mode-switch-btn");
    buttons.forEach(function (btn) {
      btn.classList.toggle("is-active", btn.getAttribute("data-tab") === tab);
    });

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

    if (authMode === "authenticated") {
      window.location.href = "/index.html";
      return;
    }

    if (authMode === "oidc") {
      try {
        loginBtn.disabled = true;
        UI.showStatus(status, "正在跳转到统一认证中心…", false);
        await window.Auth.beginOidcLogin();
      } catch (err) {
        UI.showStatus(status, UI.formatApiErrorMessage(err, "login"), true);
        loginBtn.disabled = false;
      }
      return;
    }

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

    loginBtn.disabled = true;
    UI.showStatus(status, "正在登录…", false);

    var payload = { username: usernameValue };
    if (showPasswordField) {
      payload.password = passwordValue;
    }

    try {
      var result = await Api.postJson("/api/auth/login", payload);
      await persistAuthenticatedSession(result, "login");
      renderUserInfo(window.Auth.getCurrentUser());
      UI.showStatus(status, "登录成功，正在跳转…", false);
      setTimeout(function () {
        window.location.href = "/index.html";
      }, 300);
    } catch (err) {
      UI.showStatus(status, UI.formatApiErrorMessage(err, "login"), true);
    } finally {
      loginBtn.disabled = false;
    }
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
    UI.showStatus(status, "正在注册…", false);

    try {
      var result = await Api.postJson("/api/auth/register", {
        username: username,
        password: password,
        realName: realName || null,
        studentNumber: studentNumber || null
      });

      await persistAuthenticatedSession(result, "register");
      renderUserInfo(window.Auth.getCurrentUser());
      UI.showStatus(status, "注册成功，正在跳转…", false);
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
    if (loginBtn) {
      loginBtn.addEventListener("click", function () {
        var username = UI.qs("#loginUsernameInput") ? UI.qs("#loginUsernameInput").value : "";
        var password = UI.qs("#loginPasswordInput") ? UI.qs("#loginPasswordInput").value : "";
        doLogin(username, password);
      });
    }

    var registerBtn = UI.qs("#registerSubmitBtn");
    if (registerBtn) registerBtn.addEventListener("click", doRegister);

    UI.qsa(".mode-switch-btn").forEach(function (btn) {
      btn.addEventListener("click", function () {
        switchTab(btn.getAttribute("data-tab"));
      });
    });

    var loginPasswordToggle = UI.qs("#loginPasswordToggle");
    var loginPasswordInput = UI.qs("#loginPasswordInput");
    if (loginPasswordToggle) {
      loginPasswordToggle.addEventListener("click", function () {
        togglePasswordVisibility(loginPasswordToggle, loginPasswordInput);
      });
    }

    var registerPasswordToggle = UI.qs("#registerPasswordToggle");
    var registerPasswordInput = UI.qs("#registerPasswordInput");
    if (registerPasswordToggle) {
      registerPasswordToggle.addEventListener("click", function () {
        togglePasswordVisibility(registerPasswordToggle, registerPasswordInput);
      });
    }
  }

  async function initLoginPage() {
    bindEvents();
    await detectAuthMode();
    applyAuthModeUI();
    renderUserInfo(window.Auth.getCurrentUser ? window.Auth.getCurrentUser() : null);

    var authState = window.Auth && window.Auth.getLastAuthState ? window.Auth.getLastAuthState() : null;
    if (authState && authState.type === "expired") {
      var status = UI.qs("#loginStatus");
      UI.showStatus(status, authState.message || "当前登录已过期，请重新登录。", true);
    }
  }

  document.addEventListener("DOMContentLoaded", function () {
    initLoginPage().catch(function (err) {
      var status = UI.qs("#loginStatus");
      UI.showStatus(status, UI.formatApiErrorMessage(err, "login"), true);
    });
  });
})();
