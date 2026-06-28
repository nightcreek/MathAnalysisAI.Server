import { MotionEngine } from "../motion/MotionEngine.js";

export class AnimationController {
  constructor(engine) {
    this.motionEngine = engine instanceof MotionEngine ? engine : new MotionEngine(engine);
  }

  apply(context) {
    this.motionEngine.apply(context || {});
  }
}
