import { PatchRenderer } from "./PatchRenderer.js";

export class AnalysisRenderer {
  constructor(root) {
    this.patchRenderer = new PatchRenderer(root);
  }

  mount(state) {
    this.patchRenderer.reset(state);
  }

  apply(patches, state) {
    return this.patchRenderer.applyPatches(patches, state);
  }
}
