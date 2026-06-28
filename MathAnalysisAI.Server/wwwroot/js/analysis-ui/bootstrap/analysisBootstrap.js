import { AnalysisUIRuntime } from "../runtime/AnalysisUIRuntime.js";
import { AnalysisRenderer } from "../rendering/AnalysisRenderer.js";
import { SectionController } from "../controllers/SectionController.js";
import { AnimationController } from "../controllers/AnimationController.js";
import { MotionEngine } from "../motion/MotionEngine.js";

function resolveForm() {
  return document.getElementById("analysis-form");
}

function resolveInput() {
  return document.getElementById("analysis-input") || document.getElementById("problemTextInput");
}

function resolveVisibleInput() {
  return document.getElementById("problemTextInput");
}

function resolveRoot() {
  return document.getElementById("analysis-root") || document.getElementById("resultContainer");
}

function resolveStatus() {
  return document.getElementById("analyzeStatus");
}

function resolveButton() {
  return document.getElementById("analyzeBtn");
}

function syncHiddenInput() {
  var hiddenInput = resolveInput();
  var visibleInput = resolveVisibleInput();
  if (!hiddenInput || !visibleInput || hiddenInput === visibleInput) {
    return;
  }

  hiddenInput.value = visibleInput.value || "";
}

export const AnalysisBootstrap = {
  start() {
    window.__ANALYSIS_UI_RUNTIME_ACTIVE = true;

    var api = window.BackendApi && window.BackendApi.analysis ? window.BackendApi.analysis : null;
    var renderer = new AnalysisRenderer(resolveRoot());
    var sectionController = new SectionController(renderer);
    var animationController = new AnimationController(new MotionEngine(window.MathAnimationEngine || null));
    var runtime = new AnalysisUIRuntime(api);

    runtime.init({
      sectionController: sectionController,
      animationController: animationController,
      orchestrator: null
    });

    this._bindDOM(runtime);
    this._bindState(runtime);
    return runtime;
  },

  _bindDOM(runtime) {
    var form = resolveForm();
    var visibleInput = resolveVisibleInput();
    var hiddenInput = resolveInput();

    if (visibleInput && hiddenInput && visibleInput !== hiddenInput) {
      visibleInput.addEventListener("input", syncHiddenInput);
      syncHiddenInput();
    }

    if (form) {
      form.addEventListener("submit", function (event) {
        event.preventDefault();
        syncHiddenInput();
        var input = resolveInput();
        var inputValue = input ? input.value : "";
        Promise.resolve(runtime.run({
          text: inputValue
        })).catch(function () {
        });
      });
    }
  },

  _bindState(runtime) {
    runtime.getStore().subscribe(function (nextState) {
      var status = resolveStatus();
      var button = resolveButton();

      if (button) {
        button.disabled = nextState.phase === "starting" || nextState.phase === "streaming";
      }

      if (status && window.UI && typeof window.UI.showStatus === "function") {
        if (nextState.phase === "starting") {
          window.UI.showStatus(status, "正在初始化认知分析运行时…", false);
        } else if (nextState.phase === "streaming") {
          window.UI.showStatus(status, "正在流式推理并构建认知界面…", false);
        } else if (nextState.phase === "completed") {
          window.UI.showStatus(status, "分析完成。", false);
        } else if (nextState.phase === "error") {
          window.UI.showStatus(status, (nextState.meta && nextState.meta.errorMessage) || "分析失败。", true);
        }
      }
    });
  }
};
