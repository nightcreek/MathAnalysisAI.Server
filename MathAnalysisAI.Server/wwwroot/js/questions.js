(function () {
  var currentPage = 1;
  var pageSize = 10;
  var currentFilters = {};

  function getDifficultyLabel(value) {
    var map = { easy: "简单", medium: "中等", hard: "困难" };
    return map[value] || value || "未知";
  }

  function getTypeLabel(value) {
    var map = { calculation: "计算题", proof: "证明题", concept: "概念题", mixed: "综合题" };
    return map[value] || value || "未知";
  }

  function renderQuestionList(response) {
    var container = UI.qs("#questionListContainer");
    var paginationEl = UI.qs("#questionPagination");
    if (!container) return;

    if (!response || !response.items || !response.items.length) {
      container.innerHTML = '<div class="empty-state"><h3>暂无题目</h3><p>题库中暂无符合筛选条件的题目。</p></div>';
      if (paginationEl) paginationEl.innerHTML = "";
      return;
    }

    var html = '<div class="question-list">';
    response.items.forEach(function (q) {
      var difficultyClass = q.difficulty === "easy" ? "result-status-correct"
        : q.difficulty === "hard" ? "result-status-wrong"
        : "result-status-unknown";

      html += '<div class="question-item" data-question-id="' + q.id + '">' +
        '<div class="question-item-header">' +
          '<span class="question-item-title">' + UI.escapeHtml(q.title || "无标题") + '</span>' +
          '<span class="result-status-pill ' + difficultyClass + '">' + getDifficultyLabel(q.difficulty) + '</span>' +
        '</div>' +
        '<div class="question-item-meta">' +
          '<span>' + getTypeLabel(q.questionType) + '</span>' +
          (q.chapterName ? '<span>' + UI.escapeHtml(q.chapterName) + '</span>' : '') +
          (q.primaryKnowledgePointName ? '<span>' + UI.escapeHtml(q.primaryKnowledgePointName) + '</span>' : '') +
        '</div>' +
        '<div class="question-item-preview">' + UI.escapeHtml(q.content).substring(0, 200) + (q.content && q.content.length > 200 ? "…" : "") + '</div>' +
        '<button class="btn-secondary question-view-btn" data-id="' + q.id + '" style="margin-top:8px;font-size:13px;">查看详情</button>' +
      '</div>';
    });
    html += '</div>';
    container.innerHTML = html;

    renderPagination(response.totalCount, response.page, response.pageSize);

    container.querySelectorAll(".question-view-btn").forEach(function (btn) {
      btn.addEventListener("click", function () {
        showQuestionDetail(parseInt(btn.getAttribute("data-id"), 10));
      });
    });

    container.querySelectorAll(".question-item").forEach(function (item) {
      item.addEventListener("click", function (e) {
        if (e.target.tagName === "BUTTON") return;
        var id = parseInt(item.getAttribute("data-question-id"), 10);
        showQuestionDetail(id);
      });
      item.style.cursor = "pointer";
    });
  }

  function renderPagination(totalCount, page, ps) {
    var paginationEl = UI.qs("#questionPagination");
    if (!paginationEl) return;

    var totalPages = Math.ceil(totalCount / ps);
    if (totalPages <= 1) {
      paginationEl.innerHTML = "";
      return;
    }

    var html = '<span class="hint" style="margin-right:8px;">共 ' + totalCount + ' 题，第 ' + page + ' / ' + totalPages + ' 页</span>';

    if (page > 1) {
      html += '<button class="btn-secondary" data-page="' + (page - 1) + '" style="font-size:13px;">上一页</button>';
    }
    if (page < totalPages) {
      html += '<button class="btn-secondary" data-page="' + (page + 1) + '" style="font-size:13px;margin-left:8px;">下一页</button>';
    }

    paginationEl.innerHTML = html;

    paginationEl.querySelectorAll("button[data-page]").forEach(function (btn) {
      btn.addEventListener("click", function () {
        currentPage = parseInt(btn.getAttribute("data-page"), 10);
        loadQuestions();
      });
    });
  }

  async function showQuestionDetail(questionId) {
    try {
      var question = await Api.getJson("/api/questions/" + questionId);
      if (!question) return;

      UI.qs("#questionListContainer").style.display = "none";
      UI.qs("#questionPagination").style.display = "none";
      UI.qs("#questionDetailCard").style.display = "block";

      UI.qs("#questionDetailTitle").textContent = question.title || "无标题";

      var metaHtml = '<div class="question-detail-metas">' +
        '<span class="result-status-pill result-status-unknown">' + getDifficultyLabel(question.difficulty) + '</span>' +
        '<span>' + getTypeLabel(question.questionType) + '</span>';
      if (question.chapterName) {
        metaHtml += '<span>' + UI.escapeHtml(question.chapterName) + '</span>';
      }
      if (question.primaryKnowledgePointName) {
        metaHtml += '<span>' + UI.escapeHtml(question.primaryKnowledgePointName) + '</span>';
      }
      metaHtml += '</div>';

      UI.qs("#questionDetailMeta").innerHTML = metaHtml;
      UI.qs("#questionDetailContent").innerHTML = '<div class="question-detail-text">' +
        UI.escapeHtml(question.content || "").replace(/\n/g, "<br>") +
      '</div>';

      var answerEl = UI.qs("#questionDetailAnswer");
      if (question.standardAnswer) {
        answerEl.style.display = "block";
        answerEl.innerHTML = '<div class="section-title">标准答案</div>' +
          '<div class="question-detail-text">' + UI.escapeHtml(question.standardAnswer).replace(/\n/g, "<br>") + '</div>';
      } else {
        answerEl.style.display = "none";
      }

      var hintEl = UI.qs("#questionDetailHint");
      if (question.solutionHint) {
        hintEl.style.display = "block";
        hintEl.innerHTML = '<div class="section-title">解题提示</div>' +
          '<div class="question-detail-text">' + UI.escapeHtml(question.solutionHint).replace(/\n/g, "<br>") + '</div>';
      } else {
        hintEl.style.display = "none";
      }

      window.scrollTo({ top: 0, behavior: "smooth" });
      if (window.MathJax) MathJax.typeset();
    } catch (err) {
      console.warn("Failed to load question detail:", err);
    }
  }

  function hideQuestionDetail() {
    UI.qs("#questionListContainer").style.display = "block";
    UI.qs("#questionPagination").style.display = "flex";
    UI.qs("#questionDetailCard").style.display = "none";
  }

  async function loadQuestions() {
    var container = UI.qs("#questionListContainer");
    if (container) container.innerHTML = '<div class="hint">加载中...</div>';

    var params = new URLSearchParams();
    params.set("page", currentPage);
    params.set("pageSize", pageSize);

    var search = UI.qs("#questionSearch").value.trim();
    if (search) params.set("search", search);

    var difficulty = UI.qs("#questionDifficulty").value;
    if (difficulty) params.set("difficulty", difficulty);

    var questionType = UI.qs("#questionType").value;
    if (questionType) params.set("questionType", questionType);

    currentFilters = { search: search, difficulty: difficulty, questionType: questionType };

    try {
      var data = await Api.getJson("/api/questions?" + params.toString());
      renderQuestionList(data);
    } catch (err) {
      if (container) container.innerHTML = '<div class="hint error">加载失败：' + UI.escapeHtml(err.message || "未知错误") + '</div>';
    }
  }

  function clearFilters() {
    UI.qs("#questionSearch").value = "";
    UI.qs("#questionDifficulty").value = "";
    UI.qs("#questionType").value = "";
    currentPage = 1;
    loadQuestions();
  }

  function initQuestionsPage() {
    var searchBtn = UI.qs("#questionSearchBtn");
    var clearBtn = UI.qs("#questionClearBtn");
    var backBtn = UI.qs("#questionDetailBackBtn");
    var searchInput = UI.qs("#questionSearch");

    if (searchBtn) {
      searchBtn.addEventListener("click", function () {
        currentPage = 1;
        loadQuestions();
      });
    }

    if (clearBtn) {
      clearBtn.addEventListener("click", clearFilters);
    }

    if (backBtn) {
      backBtn.addEventListener("click", hideQuestionDetail);
    }

    if (searchInput) {
      searchInput.addEventListener("keydown", function (e) {
        if (e.key === "Enter") {
          currentPage = 1;
          loadQuestions();
        }
      });
    }

    loadQuestions();
  }

  document.addEventListener("DOMContentLoaded", function () {
    if (!UI.qs("#questionsPageRoot")) return;
    initQuestionsPage();
  });
})();
