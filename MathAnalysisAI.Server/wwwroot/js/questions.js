(function () {
  function renderQuestionList(data) {
    var container = UI.qs("#questionListContainer");
    if (!container) return;

    var items = (data && data.items) ? data.items : (Array.isArray(data) ? data : []);
    if (!items.length) {
      container.className = "hint";
      container.textContent = "暂无题目，请尝试其他筛选条件。";
      return;
    }

    var html = "";
    items.forEach(function (q) {
      html += "<div class='result-section'>";
      html += "<div class='kicker'>" + UI.escapeHtml(q.difficulty || "中等") + " · " + UI.escapeHtml(q.questionType || "未知题型") + "</div>";
      html += "<h3>" + UI.escapeHtml(q.title || "无标题") + "</h3>";
      if (q.content) {
        html += "<p>" + UI.escapeHtml((q.content || "").substring(0, 160)) + (q.content.length > 160 ? "…" : "") + "</p>";
      }
      if (q.link) {
        html += "<p><a href='" + UI.escapeHtml(q.link) + "' target='_blank' rel='noopener noreferrer'>查看资源</a></p>";
      }
      html += "</div>";
    });

    container.className = "";
    container.innerHTML = html;
  }

  async function loadQuestions() {
    var container = UI.qs("#questionListContainer");
    if (!container) return;

    container.className = "hint";
    container.textContent = "加载中…";

    var search = (UI.qs("#questionSearch")?.value || "").trim();
    var difficulty = UI.qs("#questionDifficulty")?.value || "";
    var questionType = UI.qs("#questionType")?.value || "";

    try {
      var data = await window.BackendApi.questions.list({
        search: search,
        difficulty: difficulty,
        questionType: questionType,
        take: 20
      });
      renderQuestionList(data);
    } catch (_) {
      container.className = "hint error";
      container.textContent = "题库加载失败，请稍后重试。";
    }
  }

  function clearFilters() {
    var searchInput = UI.qs("#questionSearch");
    var difficultySelect = UI.qs("#questionDifficulty");
    var typeSelect = UI.qs("#questionType");
    if (searchInput) searchInput.value = "";
    if (difficultySelect) difficultySelect.value = "";
    if (typeSelect) typeSelect.value = "";
    loadQuestions();
  }

  function bindEvents() {
    var searchBtn = UI.qs("#questionSearchBtn");
    var clearBtn = UI.qs("#questionClearBtn");
    var searchInput = UI.qs("#questionSearch");

    if (searchBtn) searchBtn.addEventListener("click", loadQuestions);
    if (clearBtn) clearBtn.addEventListener("click", clearFilters);
    if (searchInput) {
      searchInput.addEventListener("keydown", function (evt) {
        if (evt.key === "Enter") {
          evt.preventDefault();
          loadQuestions();
        }
      });
    }
  }

  function initQuestionsPage() {
    if (!UI.qs("#questionListContainer")) return;
    bindEvents();
    loadQuestions();
  }

  document.addEventListener("DOMContentLoaded", function () {
    initQuestionsPage();
  });
})();
