function hasArrayChanged(previous, next) {
  if (previous.length !== next.length) {
    return true;
  }

  for (var i = 0; i < previous.length; i += 1) {
    if (JSON.stringify(previous[i]) !== JSON.stringify(next[i])) {
      return true;
    }
  }

  return false;
}

export class DiffEngine {
  diff(previousState, nextState) {
    var patches = [];

    if (hasArrayChanged(previousState.sections || [], nextState.sections || [])) {
      patches.push({
        type: "sections_update",
        sections: nextState.sections || []
      });
    }

    if ((nextState.steps || []).length > (previousState.steps || []).length) {
      patches.push({
        type: "steps_append",
        steps: (nextState.steps || []).slice((previousState.steps || []).length)
      });
    }

    if (JSON.stringify(previousState.insight || null) !== JSON.stringify(nextState.insight || null)) {
      patches.push({
        type: "insight_update",
        insight: nextState.insight || null
      });
    }

    if ((previousState.activeSection || "") !== (nextState.activeSection || "")) {
      patches.push({
        type: "active_section_update",
        activeSection: nextState.activeSection || ""
      });
    }

    if ((previousState.status || "") !== (nextState.status || "") || (previousState.phase || "") !== (nextState.phase || "")) {
      patches.push({
        type: "state_update",
        status: nextState.status || "",
        phase: nextState.phase || ""
      });
    }

    return patches;
  }
}
