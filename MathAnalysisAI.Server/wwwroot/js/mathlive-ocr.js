(function () {
  var state = {
    formulas: [],
    derivedFormulas: [],
    mathLiveReady: false
  };

  function qs(selector) {
    return UI.qs(selector);
  }

  function hasDom() {
    return !!(qs("#photoOcrFormulaReview") && qs("#photoOcrFormulaList") && qs("#photoOcrReviewNotice"));
  }

  function normalizeFormula(item) {
    if (!item || typeof item !== "object") {
      return { latex: "", context: "" };
    }

    return {
      latex: String(item.latex || "").trim(),
      context: String(item.context || "").trim()
    };
  }

  function isMathLiveReady() {
    return !!(window.MathfieldElement || customElements.get("math-field"));
  }

  function getFormulaLatex(index) {
    if (index < 0 || index >= state.formulas.length) return "";
    return String(state.formulas[index].latex || "");
  }

  function setFormulaLatex(index, latex) {
    if (index < 0 || index >= state.formulas.length) return;
    state.formulas[index].latex = String(latex || "");
    document.dispatchEvent(new CustomEvent("photo-solution-ocr-changed"));
  }

  function appendLatexToTextarea(textareaId, latex) {
    var textarea = qs(textareaId);
    if (!textarea) return;

    var safeLatex = String(latex || "").trim();
    if (!safeLatex) return;

    var current = textarea.value || "";
    var prefix = current.trim() ? "\n\n" : "";
    textarea.value = current + prefix + "$" + safeLatex + "$";
    textarea.dispatchEvent(new Event("input", { bubbles: true }));
  }

  async function copyLatex(latex) {
    var safeLatex = String(latex || "").trim();
    if (!safeLatex || !navigator.clipboard) return;

    try {
      await navigator.clipboard.writeText(safeLatex);
    } catch (_) {
      // Ignore clipboard permission failures.
    }
  }

  function showReviewNotice(ocrResponse) {
    var notice = qs("#photoOcrReviewNotice");
    if (!notice) return;

    if (!ocrResponse || typeof ocrResponse !== "object") {
      notice.style.display = "none";
      notice.textContent = "";
      return;
    }

    var warnings = Array.isArray(ocrResponse.warnings) ? ocrResponse.warnings : [];
    var texts = ["识别结果已回填，请检查题目、我的解答和公式。确认无误后再点击开始分析。"];

    if (warnings.indexOf("section_split_uncertain") >= 0) {
      texts.push("题目与解答分区可能不完全准确，请重点检查。");
      notice.className = "hint ocr-review-card ocr-warning-note";
    } else {
      notice.className = "hint ocr-review-card";
    }

    if (String(ocrResponse.studentSolutionText || "").trim() === "[unclear]") {
      texts.push("未能可靠识别我的解答，请手动补充或修改后再分析。");
      notice.className = "hint ocr-review-card ocr-warning-note";
    }

    notice.textContent = texts.join(" ");
    notice.style.display = "block";
  }

  function buildMathLiveField(index, item, latexOutput, preview) {
    var field = document.createElement("math-field");
    field.className = "mathlive-ocr-field";
    field.value = item.latex || "";
    field.setAttribute("aria-label", "公式 " + (index + 1) + " 编辑器");

    field.addEventListener("input", function () {
      var value = String(field.value || "");
      setFormulaLatex(index, value);
      latexOutput.textContent = value;
      updatePreviewElement(preview, "\\(" + value + "\\)");
      document.dispatchEvent(new CustomEvent("photo-solution-ocr-changed"));
    });

    return field;
  }

  function buildFallbackCode(item) {
    var code = document.createElement("code");
    code.className = "latex-output-code";
    code.textContent = item.latex || "[unclear]";
    return code;
  }

  function updatePreviewElement(element, text) {
    if (!element) return;

    element.innerHTML = "<div class='math-rich-text'>" + UI.renderMixedMarkdownMath(text) + "</div>";
    UI.renderMathInElement(element);
  }

  function collectDerivedFormulas(ocrResponse) {
    if (!ocrResponse || typeof ocrResponse !== "object" || !UI || typeof UI.extractMathSegments !== "function") {
      return [];
    }

    var derived = [];
    var seen = new Set();
    var sources = [
      { label: "题目正文", text: String(ocrResponse.problemText || "") },
      { label: "解答正文", text: String(ocrResponse.studentSolutionText || "") }
    ];

    sources.forEach(function (source) {
      UI.extractMathSegments(source.text).forEach(function (segment) {
        if (!segment || segment.type !== "math") {
          return;
        }

        var value = String(segment.value || "").trim();
        if (!value) {
          return;
        }

        var key = source.label + "::" + value;
        if (seen.has(key)) {
          return;
        }

        seen.add(key);
        derived.push({
          snippet: value,
          sourceLabel: source.label
        });
      });
    });

    return derived;
  }

  function buildFormulaRow(item, index) {
    var row = document.createElement("div");
    row.className = "ocr-formula-item";

    var title = document.createElement("div");
    title.className = "ocr-formula-meta";
    title.textContent = "公式 " + (index + 1);
    row.appendChild(title);

    if (state.mathLiveReady) {
      var inlineHint = document.createElement("div");
      inlineHint.className = "ocr-formula-edit-hint";
      inlineHint.textContent = "点击公式可直接编辑";
      row.appendChild(inlineHint);
    }

    var preview = document.createElement("div");
    preview.className = "ocr-formula-preview";
    row.appendChild(preview);
    updatePreviewElement(preview, "\\(" + (item.latex || "") + "\\)");

    var renderBox = document.createElement("div");
    renderBox.className = "ocr-formula-render";

    var latexOutputWrapper = document.createElement("div");
    latexOutputWrapper.className = "latex-output";

    var latexLabel = document.createElement("div");
    latexLabel.className = "hint";
    latexLabel.textContent = "当前 LaTeX";

    var latexOutput = document.createElement("code");
    latexOutput.className = "latex-output-code";
    latexOutput.textContent = item.latex || "";

    if (state.mathLiveReady) {
      renderBox.appendChild(buildMathLiveField(index, item, latexOutput, preview));
    } else {
      renderBox.appendChild(buildFallbackCode(item));
    }

    row.appendChild(renderBox);
    latexOutputWrapper.appendChild(latexLabel);
    latexOutputWrapper.appendChild(latexOutput);
    row.appendChild(latexOutputWrapper);

    if (item.context) {
      var context = document.createElement("div");
      context.className = "hint";
      context.textContent = "上下文：" + item.context;
      row.appendChild(context);
    }

    var actions = document.createElement("div");
    actions.className = "example-button-row";

    var copyBtn = document.createElement("button");
    copyBtn.type = "button";
    copyBtn.className = "btn-secondary";
    copyBtn.textContent = "复制 LaTeX";
    copyBtn.addEventListener("click", function () {
      copyLatex(getFormulaLatex(index));
    });

    var insertProblemBtn = document.createElement("button");
    insertProblemBtn.type = "button";
    insertProblemBtn.className = "btn-secondary";
    insertProblemBtn.textContent = "插入到题目末尾";
    insertProblemBtn.addEventListener("click", function () {
      appendLatexToTextarea("#problemTextInput", getFormulaLatex(index));
    });

    var insertSolutionBtn = document.createElement("button");
    insertSolutionBtn.type = "button";
    insertSolutionBtn.className = "btn-secondary";
    insertSolutionBtn.textContent = "插入到我的解答末尾";
    insertSolutionBtn.addEventListener("click", function () {
      appendLatexToTextarea("#studentSolutionTextInput", getFormulaLatex(index));
    });

    actions.appendChild(copyBtn);
    actions.appendChild(insertProblemBtn);
    actions.appendChild(insertSolutionBtn);
    row.appendChild(actions);

    return row;
  }

  function buildDerivedFormulaRow(item, index) {
    var row = document.createElement("div");
    row.className = "ocr-formula-item";

    var title = document.createElement("div");
    title.className = "ocr-formula-meta";
    title.textContent = "正文公式片段 " + (index + 1);
    row.appendChild(title);

    var note = document.createElement("div");
    note.className = "ocr-derived-note";
    note.textContent = "未识别为独立公式候选，以下内容来自正文中的 LaTeX 片段，只读展示。";
    row.appendChild(note);

    var preview = document.createElement("div");
    preview.className = "ocr-formula-preview";
    row.appendChild(preview);
    updatePreviewElement(preview, item.snippet || "");

    if (item.sourceLabel) {
      var source = document.createElement("div");
      source.className = "hint";
      source.textContent = "来源：" + item.sourceLabel;
      row.appendChild(source);
    }

    var raw = document.createElement("code");
    raw.className = "latex-output-code";
    raw.textContent = item.snippet || "";
    row.appendChild(raw);

    return row;
  }

  async function renderFormulas(formulas, ocrResponse) {
    if (!hasDom()) return;

    state.mathLiveReady = isMathLiveReady();
    state.formulas = (Array.isArray(formulas) ? formulas : [])
      .map(normalizeFormula)
      .filter(function (item) {
        return !!item.latex;
      });
    state.derivedFormulas = state.formulas.length ? [] : collectDerivedFormulas(ocrResponse);

    var reviewCard = qs("#photoOcrFormulaReview");
    var list = qs("#photoOcrFormulaList");
    if (!reviewCard || !list) return;

    showReviewNotice(ocrResponse);
    list.innerHTML = "";
    reviewCard.style.display = "block";

    if (!state.formulas.length && !state.derivedFormulas.length) {
      var empty = document.createElement("div");
      empty.className = "status";
      empty.textContent = "未识别到独立公式，可直接检查题目和解答文本。";
      list.appendChild(empty);
      return;
    }

    if (!state.mathLiveReady && state.formulas.length) {
      var hint = document.createElement("div");
      hint.className = "status warning-hint";
      hint.textContent = "MathLive 未加载，请检查 /vendor/mathlive 文件是否存在。";
      list.appendChild(hint);
    }

    state.formulas.forEach(function (item, index) {
      list.appendChild(buildFormulaRow(item, index));
    });

    state.derivedFormulas.forEach(function (item, index) {
      list.appendChild(buildDerivedFormulaRow(item, index));
    });

    await UI.renderMathInElement(list);
  }

  window.MathLiveOcr = {
    init: function () {
      if (!hasDom()) return;
      state.mathLiveReady = isMathLiveReady();
    },
    renderFormulas: renderFormulas,
    getFormulas: function () {
      return state.formulas.map(function (item) {
        return {
          latex: String(item.latex || "").trim(),
          context: String(item.context || "").trim()
        };
      });
    }
  };

  document.addEventListener("DOMContentLoaded", function () {
    if (window.MathLiveOcr && window.MathLiveOcr.init) {
      window.MathLiveOcr.init();
    }
  });
})();
