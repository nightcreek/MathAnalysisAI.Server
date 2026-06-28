import { PerceptionEngine } from "../perception/PerceptionEngine.js";

export class SectionController {
  constructor(renderer, perceptionEngine) {
    this.renderer = renderer;
    this.perceptionEngine = perceptionEngine || new PerceptionEngine();
  }

  mount(state) {
    this.renderer.mount(state);
  }

  handleCommit(patches, state) {
    var context = this.renderer.apply(patches, state);
    var perception = this.perceptionEngine.apply(state, context);
    return Object.assign({}, context, {
      perception: perception,
      state: state
    });
  }
}
