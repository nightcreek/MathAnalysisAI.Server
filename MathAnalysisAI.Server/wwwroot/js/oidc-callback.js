(function () {
  async function init() {
    var status = UI.qs("#oidcCallbackStatus");
    var message = UI.qs("#oidcCallbackMessage");

    try {
      UI.showStatus(status, "正在完成统一认证登录…", false);
      var user = await window.Auth.handleOidcCallback();
      UI.showStatus(status, "登录成功，正在跳转…", false);
      if (message) {
        message.textContent = user && user.username
          ? "欢迎回来，" + (user.realName || user.username) + "。"
          : "";
      }
      setTimeout(function () {
        window.location.href = "/index.html";
      }, 300);
    } catch (err) {
      UI.showStatus(status, "统一认证登录失败。", true);
      UI.renderErrorPanel(message, {
        title: "统一认证失败",
        message: err && err.message ? err.message : "无法完成统一认证登录，请返回登录页重试。",
        actionLabel: "返回登录页",
        onAction: function () {
          window.location.href = "/login.html";
        }
      });
    }
  }

  document.addEventListener("DOMContentLoaded", function () {
    init();
  });
})();
