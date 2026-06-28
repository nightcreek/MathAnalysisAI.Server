function animateSpring(element) {
  if (!element) {
    return;
  }

  var position = 18;
  var velocity = 0;
  var target = 0;
  var stiffness = 0.12;
  var damping = 0.82;
  element.style.opacity = "0.01";
  element.style.transform = "translateY(" + position + "px) scale(0.985)";

  function frame() {
    var force = (target - position) * stiffness;
    velocity = (velocity + force) * damping;
    position += velocity;

    if (Math.abs(velocity) < 0.08 && Math.abs(position - target) < 0.08) {
      element.style.opacity = "1";
      element.style.transform = "translateY(0) scale(1)";
      return;
    }

    var opacity = Math.max(0.08, 1 - Math.abs(position) / 22);
    element.style.opacity = String(opacity);
    element.style.transform = "translateY(" + position.toFixed(2) + "px) scale(" + (1 - Math.abs(position) / 900).toFixed(4) + ")";
    window.requestAnimationFrame(frame);
  }

  window.requestAnimationFrame(frame);
}

function animateScrollIntoView(element) {
  if (!element) {
    return;
  }

  var startY = window.scrollY || window.pageYOffset || 0;
  var targetY = Math.max(0, startY + element.getBoundingClientRect().top - 120);
  var velocity = (targetY - startY) * 0.18;
  var friction = 0.84;

  function step() {
    if (Math.abs(velocity) < 0.5 && Math.abs(targetY - window.scrollY) < 1) {
      window.scrollTo({ top: targetY, behavior: "auto" });
      return;
    }

    var nextY = (window.scrollY || window.pageYOffset || 0) + velocity;
    window.scrollTo({ top: nextY, behavior: "auto" });
    velocity *= friction;
    velocity += (targetY - nextY) * 0.08;
    window.requestAnimationFrame(step);
  }

  window.requestAnimationFrame(step);
}

function morphMath(elements) {
  (elements || []).forEach(function (element) {
    if (!element || !element.querySelectorAll) {
      return;
    }

    element.querySelectorAll(".math-inline-fragment, .math-block-fragment, .math-rich-text").forEach(function (mathNode) {
      mathNode.classList.add("analysis-cognitive-math-morph");
      mathNode.style.opacity = "0.4";
      mathNode.style.transform = "scale(0.985)";
      window.requestAnimationFrame(function () {
        mathNode.style.opacity = "1";
        mathNode.style.transform = "scale(1)";
      });
    });
  });
}

export class MotionEngine {
  apply(context) {
    var touched = context && context.touchedElements ? context.touchedElements : [];
    var active = context && context.activeSectionElement ? context.activeSectionElement : null;
    var latestStep = context && context.latestStepElement ? context.latestStepElement : null;

    touched.forEach(animateSpring);
    morphMath(touched);

    if (latestStep) {
      animateScrollIntoView(latestStep);
    } else if (active) {
      animateScrollIntoView(active);
    }
  }
}
