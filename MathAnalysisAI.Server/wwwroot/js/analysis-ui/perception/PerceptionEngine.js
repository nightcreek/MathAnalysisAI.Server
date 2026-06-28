export class PerceptionEngine {
  apply(state, context) {
    var shell = context && context.shell ? context.shell : null;
    if (!shell) {
      return {
        throttled: false
      };
    }

    var sectionCount = (state.sections || []).length;
    var stepCount = (state.steps || []).length;
    var densityScore = sectionCount + stepCount;
    var throttled = densityScore > 12;

    shell.dataset.density = throttled ? "high" : "normal";

    var stepNodes = shell.querySelectorAll(".analysis-cognitive-step");
    var visibleThreshold = throttled ? 5 : 8;
    stepNodes.forEach(function (node, index) {
      var isMuted = stepNodes.length - index > visibleThreshold;
      node.classList.toggle("is-muted", isMuted);
      node.style.opacity = isMuted ? "0.32" : "1";
      node.style.filter = isMuted ? "saturate(0.82)" : "none";
    });

    var sectionNodes = shell.querySelectorAll(".analysis-cognitive-section");
    sectionNodes.forEach(function (node) {
      if (node.classList.contains("is-active")) {
        node.style.opacity = "1";
        node.style.transform = "translateY(-2px)";
      } else if (state.activeSection) {
        node.style.opacity = "0.52";
        node.style.transform = "translateY(0)";
      } else {
        node.style.opacity = "1";
      }
    });

    var insight = shell.querySelector(".analysis-cognitive-insight");
    if (insight) {
      insight.style.opacity = state.insight ? "1" : "0.82";
      insight.style.marginTop = stepCount > 0 ? "8px" : "0";
    }

    return {
      throttled: throttled
    };
  }
}
