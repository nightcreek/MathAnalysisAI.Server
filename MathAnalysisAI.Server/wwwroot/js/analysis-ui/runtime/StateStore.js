function normalizeState(state) {
  var next = Object.assign({
    input: "",
    sections: [],
    steps: [],
    insight: null,
    activeSection: "",
    status: "idle",
    phase: "idle",
    meta: {}
  }, state || {});

  next.sections = Array.isArray(next.sections) ? next.sections.map(function (section) {
    return Object.freeze(Object.assign({}, section));
  }) : [];
  next.steps = Array.isArray(next.steps) ? next.steps.map(function (step) {
    return Object.freeze(Object.assign({}, step));
  }) : [];
  next.insight = next.insight ? Object.freeze(Object.assign({}, next.insight)) : null;
  next.meta = Object.freeze(Object.assign({}, next.meta || {}));

  return Object.freeze(next);
}

export class StateStore {
  constructor(initialState) {
    this._state = normalizeState(initialState);
    this._listeners = new Set();
  }

  getState() {
    return this._state;
  }

  setState(updater) {
    var previous = this._state;
    var nextValue = typeof updater === "function" ? updater(previous) : updater;
    var next = normalizeState(nextValue);
    this._state = next;

    this._listeners.forEach(function (listener) {
      listener(next, previous);
    });

    return {
      previous: previous,
      next: next
    };
  }

  subscribe(listener) {
    if (typeof listener !== "function") {
      return function () {};
    }

    this._listeners.add(listener);
    return () => {
      this._listeners.delete(listener);
    };
  }
}
