function upsertById(list, item) {
  var found = false;
  var next = (list || []).map(function (entry) {
    if (entry.id !== item.id) {
      return entry;
    }
    found = true;
    return Object.assign({}, entry, item);
  });

  if (!found) {
    next.push(Object.assign({}, item));
  }

  return next;
}

function dedupeById(list) {
  var seen = new Set();
  return (list || []).filter(function (entry) {
    if (!entry || !entry.id || seen.has(entry.id)) {
      return false;
    }
    seen.add(entry.id);
    return true;
  });
}

export class StreamReconciler {
  constructor(store, diffEngine) {
    this.store = store;
    this.diffEngine = diffEngine;
  }

  process(event) {
    var previousState = this.store.getState();
    var nextState = this._reduce(previousState, event || {});
    this.store.setState(nextState);

    return {
      previousState: previousState,
      nextState: nextState,
      patches: this.diffEngine.diff(previousState, nextState),
      event: event
    };
  }

  _reduce(previousState, event) {
    var payload = event.payload || {};
    var next = Object.assign({}, previousState);

    if (event.type === "state") {
      if (payload.phase === "starting") {
        return {
          input: payload.input || "",
          sections: [],
          steps: [],
          insight: null,
          activeSection: "",
          status: payload.status || "running",
          phase: payload.phase || "starting",
          meta: payload.meta || {}
        };
      }

      next.status = payload.status || next.status;
      next.phase = payload.phase || next.phase;
      next.meta = Object.assign({}, next.meta || {}, payload.meta || {});
      return next;
    }

    if (event.type === "section") {
      next.sections = upsertById(next.sections, payload);
      next.activeSection = payload.id || next.activeSection;
      return next;
    }

    if (event.type === "step") {
      next.steps = dedupeById((next.steps || []).concat([payload]));
      if (payload.sectionId) {
        next.activeSection = payload.sectionId;
      }
      return next;
    }

    if (event.type === "insight") {
      next.insight = Object.assign({}, next.insight || {}, payload);
      if (payload.activeSection) {
        next.activeSection = payload.activeSection;
      }
      return next;
    }

    if (event.type === "math") {
      next.meta = Object.assign({}, next.meta || {}, {
        mathCount: (next.meta && next.meta.mathCount ? next.meta.mathCount : 0) + 1
      });
      return next;
    }

    return next;
  }
}
