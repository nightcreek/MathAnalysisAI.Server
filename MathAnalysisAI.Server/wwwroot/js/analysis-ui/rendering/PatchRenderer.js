function escapeText(value) {
  if (window.UI && typeof window.UI.escapeHtml === "function") {
    return window.UI.escapeHtml(value);
  }
  return String(value || "");
}

function renderMathText(text) {
  if (window.UI && typeof window.UI.renderMixedMarkdownMath === "function") {
    return window.UI.renderMixedMarkdownMath(String(text || ""));
  }
  return escapeText(text);
}

function ensureStyles() {
  if (document.getElementById("analysis-cognitive-runtime-styles")) {
    return;
  }

  var style = document.createElement("style");
  style.id = "analysis-cognitive-runtime-styles";
  style.textContent = "" +
    ".analysis-cognitive-shell{display:grid;gap:16px;}" +
    ".analysis-cognitive-sections,.analysis-cognitive-steps{display:grid;gap:12px;}" +
    ".analysis-cognitive-section,.analysis-cognitive-step,.analysis-cognitive-insight{background:rgba(255,255,255,0.9);border:1px solid rgba(15,23,42,0.08);border-radius:20px;padding:18px 20px;box-shadow:0 10px 30px rgba(15,23,42,0.06);transform:translateY(0);transition:opacity .28s ease,transform .36s ease,box-shadow .36s ease,filter .36s ease;}" +
    ".analysis-cognitive-section.is-dimmed,.analysis-cognitive-step.is-dimmed{opacity:.42;filter:saturate(.85);}" +
    ".analysis-cognitive-section.is-active{box-shadow:0 18px 44px rgba(15,23,42,0.12);transform:translateY(-2px);}" +
    ".analysis-cognitive-kicker{font-size:12px;letter-spacing:.08em;text-transform:uppercase;color:var(--text-fade);margin-bottom:8px;}" +
    ".analysis-cognitive-title{margin:0;font-size:1.05rem;line-height:1.35;}" +
    ".analysis-cognitive-body{margin-top:10px;color:var(--text-main);line-height:1.7;}" +
    ".analysis-cognitive-steps-title,.analysis-cognitive-insight-title{font-size:13px;text-transform:uppercase;letter-spacing:.08em;color:var(--text-fade);margin-bottom:8px;}" +
    ".analysis-cognitive-step-index{display:inline-flex;min-width:1.8rem;height:1.8rem;border-radius:999px;align-items:center;justify-content:center;background:rgba(15,23,42,0.06);font-size:12px;margin-right:10px;}" +
    ".analysis-cognitive-step-head{display:flex;align-items:center;font-weight:600;line-height:1.45;}" +
    ".analysis-cognitive-step-body{margin-top:10px;line-height:1.72;}" +
    ".analysis-cognitive-shell[data-density='high'] .analysis-cognitive-section,.analysis-cognitive-shell[data-density='high'] .analysis-cognitive-step{padding:14px 16px;border-radius:16px;}" +
    ".analysis-cognitive-shell[data-density='high'] .analysis-cognitive-step.is-muted{opacity:.28;transform:scale(.985);}" +
    ".analysis-cognitive-streaming{color:var(--text-fade);font-size:13px;}" +
    ".analysis-cognitive-insight{background:linear-gradient(180deg,rgba(255,255,255,0.98),rgba(246,248,252,0.96));}" +
    ".analysis-cognitive-insight-headline{font-size:1.15rem;font-weight:700;line-height:1.5;}" +
    ".analysis-cognitive-insight-list{margin:12px 0 0;padding-left:18px;line-height:1.7;}" +
    ".analysis-cognitive-status{font-size:13px;color:var(--text-fade);}" +
    ".analysis-cognitive-root[data-runtime-state='loading'] .analysis-cognitive-insight{opacity:.85;}" +
    ".analysis-cognitive-root[data-runtime-state='error'] .analysis-cognitive-insight{border-color:rgba(220,38,38,.18);}" +
    ".analysis-cognitive-math-morph{transition:transform .42s ease,opacity .42s ease;}" +
    ".analysis-cognitive-shell .math-rich-text{line-height:1.8;}";
  document.head.appendChild(style);
}

export class PatchRenderer {
  constructor(root) {
    this.root = root;
    this.mount = root && root.querySelector ? (root.querySelector("#resultContainer") || root) : root;
    this.nodes = {
      shell: null,
      status: null,
      sections: null,
      steps: null,
      insight: null
    };
    ensureStyles();
  }

  reset() {
    if (!this.mount) {
      return;
    }

    while (this.mount.firstChild) {
      this.mount.removeChild(this.mount.firstChild);
    }

    var shell = document.createElement("div");
    shell.className = "analysis-cognitive-shell";

    var status = document.createElement("div");
    status.className = "analysis-cognitive-status";
    status.textContent = "等待分析开始。";

    var sections = document.createElement("div");
    sections.className = "analysis-cognitive-sections";

    var steps = document.createElement("div");
    steps.className = "analysis-cognitive-steps";

    var insight = document.createElement("div");
    insight.className = "analysis-cognitive-insight";
    insight.style.display = "none";

    shell.appendChild(status);
    shell.appendChild(sections);
    shell.appendChild(steps);
    shell.appendChild(insight);
    this.mount.appendChild(shell);

    this.mount.classList.remove("empty-state");
    this.mount.classList.add("analysis-cognitive-root");

    this.nodes = {
      shell: shell,
      status: status,
      sections: sections,
      steps: steps,
      insight: insight
    };
  }

  applyPatches(patches, state) {
    if (!this.nodes.shell) {
      this.reset();
    }

    var touchedElements = [];
    var latestStepElement = null;
    var activeSectionElement = null;

    for (var i = 0; i < patches.length; i += 1) {
      var patch = patches[i];
      if (patch.type === "sections_update") {
        touchedElements = touchedElements.concat(this._applySections(patch.sections || []));
      } else if (patch.type === "steps_append") {
        var appended = this._appendSteps(patch.steps || []);
        touchedElements = touchedElements.concat(appended);
        latestStepElement = appended.length ? appended[appended.length - 1] : latestStepElement;
      } else if (patch.type === "insight_update") {
        var insightElement = this._updateInsight(patch.insight);
        if (insightElement) {
          touchedElements.push(insightElement);
        }
      } else if (patch.type === "active_section_update") {
        activeSectionElement = this._markActiveSection(patch.activeSection || "");
      } else if (patch.type === "state_update") {
        this._updateStateStatus(patch, state);
      }
    }

    if (!activeSectionElement && state.activeSection) {
      activeSectionElement = this._markActiveSection(state.activeSection);
    }

    this._renderMathFor(touchedElements);

    return {
      root: this.mount,
      shell: this.nodes.shell,
      activeSectionElement: activeSectionElement,
      latestStepElement: latestStepElement,
      touchedElements: touchedElements
    };
  }

  _applySections(sections) {
    var host = this.nodes.sections;
    var touched = [];
    var existing = new Map();

    Array.from(host.children).forEach(function (child) {
      existing.set(child.getAttribute("data-section-id"), child);
    });

    sections.forEach(function (section) {
      if (!section || !section.id) {
        return;
      }

      var node = existing.get(section.id);
      if (!node) {
        node = document.createElement("article");
        node.className = "analysis-cognitive-section";
        node.setAttribute("data-section-id", section.id);
        node.innerHTML =
          "<div class='analysis-cognitive-kicker'></div>" +
          "<h3 class='analysis-cognitive-title'></h3>" +
          "<div class='analysis-cognitive-body'></div>";
        host.appendChild(node);
      }

      node.querySelector(".analysis-cognitive-kicker").textContent = section.kicker || "Analysis";
      node.querySelector(".analysis-cognitive-title").textContent = section.title || "未命名阶段";
      node.querySelector(".analysis-cognitive-body").innerHTML = renderMathText(section.content || "");
      touched.push(node);
      existing.delete(section.id);
    });

    existing.forEach(function (node) {
      if (node && node.parentNode === host) {
        host.removeChild(node);
      }
    });

    return touched;
  }

  _appendSteps(steps) {
    var host = this.nodes.steps;
    var created = [];

    steps.forEach(function (step, index) {
      if (!step || !step.id) {
        return;
      }

      if (host.querySelector("[data-step-id='" + step.id + "']")) {
        return;
      }

      var node = document.createElement("article");
      node.className = "analysis-cognitive-step";
      node.setAttribute("data-step-id", step.id);
      node.setAttribute("data-section-id", step.sectionId || "");
      node.innerHTML =
        "<div class='analysis-cognitive-step-head'>" +
          "<span class='analysis-cognitive-step-index'></span>" +
          "<span class='analysis-cognitive-step-title'></span>" +
        "</div>" +
        "<div class='analysis-cognitive-step-body'></div>";

      node.querySelector(".analysis-cognitive-step-index").textContent = step.order || (host.children.length + index + 1);
      node.querySelector(".analysis-cognitive-step-title").textContent = step.title || "推理步骤";
      node.querySelector(".analysis-cognitive-step-body").innerHTML = renderMathText(step.content || "");
      host.appendChild(node);
      created.push(node);
    });

    return created;
  }

  _updateInsight(insight) {
    var host = this.nodes.insight;
    if (!host) {
      return null;
    }

    if (!insight) {
      host.style.display = "none";
      return null;
    }

    host.style.display = "";
    while (host.firstChild) {
      host.removeChild(host.firstChild);
    }

    var kicker = document.createElement("div");
    kicker.className = "analysis-cognitive-insight-title";
    kicker.textContent = "Insight";
    host.appendChild(kicker);

    var headline = document.createElement("div");
    headline.className = "analysis-cognitive-insight-headline math-rich-text";
    headline.innerHTML = renderMathText(insight.headline || insight.summary || "分析已完成");
    host.appendChild(headline);

    if (insight.summary) {
      var summary = document.createElement("div");
      summary.className = "analysis-cognitive-body math-rich-text";
      summary.innerHTML = renderMathText(insight.summary);
      host.appendChild(summary);
    }

    if (Array.isArray(insight.points) && insight.points.length) {
      var list = document.createElement("ul");
      list.className = "analysis-cognitive-insight-list";
      insight.points.forEach(function (point) {
        var item = document.createElement("li");
        item.className = "math-rich-text";
        item.innerHTML = renderMathText(point);
        list.appendChild(item);
      });
      host.appendChild(list);
    }

    return host;
  }

  _markActiveSection(activeSection) {
    var node = null;
    Array.from(this.nodes.sections.children).forEach(function (child) {
      var isActive = child.getAttribute("data-section-id") === activeSection;
      child.classList.toggle("is-active", isActive);
      child.classList.toggle("is-dimmed", !isActive && !!activeSection);
      if (isActive) {
        node = child;
      }
    });

    Array.from(this.nodes.steps.children).forEach(function (child) {
      var stepSection = child.getAttribute("data-section-id") || "";
      var isDimmed = !!activeSection && stepSection && stepSection !== activeSection;
      child.classList.toggle("is-dimmed", isDimmed);
    });

    return node;
  }

  _updateStateStatus(patch, state) {
    if (!this.nodes.status) {
      return;
    }

    if (patch.phase === "starting") {
      this.nodes.status.textContent = "已初始化分析运行时，等待流式响应。";
    } else if (patch.phase === "streaming") {
      this.nodes.status.textContent = "正在接收分析流并逐步构建推理界面。";
    } else if (patch.phase === "completed") {
      this.nodes.status.textContent = "分析完成，已收敛为结构化结论。";
    } else if (patch.phase === "error") {
      this.nodes.status.textContent = state && state.meta && state.meta.errorMessage
        ? state.meta.errorMessage
        : "分析失败。";
    }

    if (this.root) {
      this.root.dataset.runtimeState = patch.status || "";
      this.root.dataset.runtimePhase = patch.phase || "";
    }
  }

  _renderMathFor(elements) {
    if (!window.UI || typeof window.UI.renderMathInElement !== "function") {
      return;
    }

    (elements || []).forEach(function (element) {
      if (element) {
        window.UI.renderMathInElement(element);
      }
    });
  }
}
