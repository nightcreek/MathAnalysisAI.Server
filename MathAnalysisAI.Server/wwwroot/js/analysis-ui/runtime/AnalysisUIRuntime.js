import { StateStore } from "./StateStore.js";
import { DiffEngine } from "./DiffEngine.js";
import { StreamReconciler } from "./StreamReconciler.js";

function safeText(value) {
  return String(value == null ? "" : value).trim();
}

function uniqueList(items) {
  var seen = new Set();
  return (items || []).map(function (item) {
    return safeText(item);
  }).filter(function (item) {
    if (!item || seen.has(item)) {
      return false;
    }
    seen.add(item);
    return true;
  });
}

function parseSseLines(buffer) {
  var lines = buffer.split("\n");
  return {
    complete: lines.slice(0, -1),
    remainder: lines[lines.length - 1] || ""
  };
}

function buildSection(id, kicker, title, content) {
  return {
    id: id,
    kicker: kicker,
    title: title,
    content: content
  };
}

export class AnalysisUIRuntime {
  constructor(api) {
    this.api = api;
    this.store = new StateStore({
      input: "",
      sections: [],
      steps: [],
      insight: null,
      activeSection: "",
      status: "idle",
      phase: "idle",
      meta: {}
    });
    this.diffEngine = new DiffEngine();
    this.reconciler = new StreamReconciler(this.store, this.diffEngine);
    this.sectionController = null;
    this.animationController = null;
    this.orchestrator = null;
    this._streamStepCounter = 0;
    this._streamNarrativeRemainder = "";
  }

  init(options) {
    var opts = options || {};
    this.sectionController = opts.sectionController || null;
    this.animationController = opts.animationController || null;
    this.orchestrator = opts.orchestrator || null;
  }

  getStore() {
    return this.store;
  }

  async run(input) {
    var request = await this._buildRequest(input || {});
    this._streamStepCounter = 0;
    this._streamNarrativeRemainder = "";

    this._commit({
      type: "state",
      payload: {
        phase: "starting",
        status: "loading",
        input: request.inputText,
        meta: {}
      }
    }, true);

    this._commit({
      type: "section",
      payload: buildSection("stream", "Runtime", "推理流初始化", "系统已接管分析流程，正在等待流式结果。")
    });

    var response = await this.api.runStream(request.payload);
    await this._consumeStream(response, request.inputText);
  }

  _commit(event, shouldMount) {
    var result = this.reconciler.process(event);

    if (this.sectionController) {
      if (shouldMount) {
        this.sectionController.mount(result.nextState);
      }

      var context = this.sectionController.handleCommit(result.patches, result.nextState);
      if (this.animationController) {
        this.animationController.apply(Object.assign({}, context, {
          event: event
        }));
      }
    }

    return result;
  }

  async _buildRequest(input) {
    var providedText = safeText(input.text);
    var visibleInput = document.getElementById("problemTextInput");
    var hiddenInput = document.getElementById("analysis-input");
    var statusNode = document.getElementById("analyzeStatus");

    var problemText = providedText || safeText(visibleInput && visibleInput.value);
    if (!problemText) {
      throw new Error("请先输入题目。");
    }

    if (visibleInput) {
      visibleInput.value = problemText;
    }
    if (hiddenInput) {
      hiddenInput.value = problemText;
    }

    if (window.MathAnalysis && typeof window.MathAnalysis.refreshInputPreviews === "function") {
      await window.MathAnalysis.refreshInputPreviews();
    }

    var currentUserId = null;
    if (window.Auth && typeof window.Auth.getCurrentUserId === "function") {
      currentUserId = window.Auth.getCurrentUserId();
      if (currentUserId == null && typeof window.Auth.loadCurrentUser === "function") {
        await window.Auth.loadCurrentUser();
        currentUserId = window.Auth.getCurrentUserId();
      }
    }

    if (currentUserId == null) {
      throw new Error("当前登录已失效，请重新登录后再继续分析。");
    }

    var ocrRecordId = null;
    if (window.PhotoSolutionOcr && typeof window.PhotoSolutionOcr.getRecordId === "function") {
      ocrRecordId = window.PhotoSolutionOcr.getRecordId();
      if (ocrRecordId && typeof window.PhotoSolutionOcr.isConfirmed === "function" && !window.PhotoSolutionOcr.isConfirmed()) {
        if (statusNode && window.UI && typeof window.UI.showStatus === "function") {
          window.UI.showStatus(statusNode, "OCR 结果尚未确认，请先完成 OCR 确认后再分析。", true);
        }
        throw new Error("OCR 结果尚未确认，请先完成 OCR 确认后再分析。");
      }
    }

    var studentSolutionInput = document.getElementById("studentSolutionTextInput");
    var chapterSelect = document.getElementById("chapterSelect");
    var modeSelect = document.getElementById("modeSelect");
    var chapterId = parseInt(chapterSelect && chapterSelect.value, 10);
    var courseId = (window.AppConfig && typeof window.AppConfig.resolveCourseId === "function"
      ? window.AppConfig.resolveCourseId()
      : null) || (window.AppConfig && typeof window.AppConfig.getCachedCourses === "function"
      ? ((window.AppConfig.getCachedCourses()[0] || {}).id || null)
      : null);

    return {
      inputText: problemText,
      payload: {
        courseId: courseId,
        chapterId: Number.isNaN(chapterId) ? (window.AppConfig ? window.AppConfig.defaultChapterId : null) : chapterId,
        problemText: problemText,
        studentSolutionText: safeText(studentSolutionInput && studentSolutionInput.value),
        analysisMode: safeText(modeSelect && modeSelect.value) || "review_solution",
        userId: currentUserId,
        ocrRecordId: ocrRecordId,
        formulas: (window.MathLiveOcr && typeof window.MathLiveOcr.getFormulas === "function")
          ? window.MathLiveOcr.getFormulas()
          : []
      }
    };
  }

  async _consumeStream(response, inputText) {
    if (!response || !response.ok) {
      var message = "分析请求失败。";
      if (response) {
        try {
          var raw = await response.text();
          var parsed = raw ? JSON.parse(raw) : null;
          message = parsed && parsed.message ? parsed.message : message;
        } catch (_) {
        }
      }

      this._commit({
        type: "state",
        payload: {
          phase: "error",
          status: "error",
          meta: {
            errorMessage: message
          }
        }
      });
      throw new Error(message);
    }

    if (!response.body) {
      throw new Error("浏览器不支持流式读取。");
    }

    this._commit({
      type: "state",
      payload: {
        phase: "streaming",
        status: "loading",
        meta: {}
      }
    });

    var reader = response.body.getReader();
    var decoder = new TextDecoder();
    var buffer = "";
    var accumulatedText = "";

    while (true) {
      var readResult = await reader.read();
      if (readResult.done) {
        break;
      }

      buffer += decoder.decode(readResult.value, { stream: true });
      var parsedLines = parseSseLines(buffer);
      buffer = parsedLines.remainder;

      for (var i = 0; i < parsedLines.complete.length; i += 1) {
        var line = parsedLines.complete[i].trim();
        if (!line || line.indexOf("data: ") !== 0) {
          continue;
        }

        var rawData = line.slice(6);
        if (rawData === "[DONE]") {
          continue;
        }

        try {
          var chunk = JSON.parse(rawData);
          accumulatedText += chunk;
          this._emitNarrativeEvents(String(chunk || ""));
          this._commit({
            type: "section",
            payload: buildSection(
              "stream",
              "Runtime",
              "推理流进行中",
              this._summarizeStream(accumulatedText)
            )
          });
        } catch (_) {
        }
      }
    }

    await this._finalizeStructuredResult(accumulatedText, inputText);
  }

  _emitNarrativeEvents(chunkText) {
    var text = safeText(chunkText);
    if (!text) {
      return;
    }

    this._streamNarrativeRemainder += text;
    var segments = this._streamNarrativeRemainder.split(/(?<=[。！？!?\n])/);
    this._streamNarrativeRemainder = segments.pop() || "";

    for (var i = 0; i < segments.length; i += 1) {
      var segment = safeText(segments[i]);
      if (!segment) {
        continue;
      }

      this._streamStepCounter += 1;
      this._commit({
        type: "step",
        payload: {
          id: "stream-step-" + this._streamStepCounter,
          order: this._streamStepCounter,
          sectionId: "stream",
          title: "推理片段 " + this._streamStepCounter,
          content: segment
        }
      });

      if (window.UI && typeof window.UI.containsMathSyntax === "function" && window.UI.containsMathSyntax(segment)) {
        this._commit({
          type: "math",
          payload: {
            text: segment
          }
        });
      }
    }
  }

  _summarizeStream(text) {
    var compact = safeText(text).slice(-280);
    return compact || "正在接收推理过程…";
  }

  async _finalizeStructuredResult(accumulatedText, inputText) {
    var parsed = null;

    try {
      parsed = JSON.parse(accumulatedText);
    } catch (_) {
      parsed = null;
    }

    if (!parsed) {
      this._commit({
        type: "section",
        payload: buildSection("raw", "Fallback", "流式结果", accumulatedText || "未返回结构化内容。")
      });
      this._commit({
        type: "insight",
        payload: {
          headline: "已收到原始流式内容",
          summary: accumulatedText || "流式结果为空。",
          points: []
        }
      });
      this._commit({
        type: "state",
        payload: {
          phase: "completed",
          status: "complete",
          meta: {}
        }
      });
      return;
    }

    var review = parsed.studentSolutionReview && typeof parsed.studentSolutionReview === "object"
      ? parsed.studentSolutionReview
      : {};
    var mainIssue = window.UI && typeof window.UI.formatAnalysisMainIssue === "function"
      ? window.UI.formatAnalysisMainIssue(safeText(review.mainIssue))
      : safeText(review.mainIssue);

    var sections = [
      buildSection(
        "summary",
        "Overview",
        "分析摘要",
        mainIssue || safeText(parsed.solutionOverview) || "已完成分析。"
      ),
      buildSection(
        "knowledge",
        "Knowledge",
        "关联知识点",
        uniqueList(parsed.knowledgePoints).join("、") || "暂无关联知识点"
      ),
      buildSection(
        "issues",
        "Review",
        "解答问题",
        uniqueList([].concat(review.logicGaps || [], parsed.mistakeTags || [])).join("；") || "暂未发现明确问题"
      ),
      buildSection(
        "solution",
        "Solution",
        "标准解法概览",
        safeText(parsed.solutionOverview) || "请查看下方的分步推理。"
      )
    ];

    if (safeText(parsed.answerReliability)) {
      sections.push(buildSection(
        "reliability",
        "Reliability",
        "答案可靠性",
        safeText(parsed.answerReliability) + (uniqueList(parsed.reliabilityReasons).length ? "：" + uniqueList(parsed.reliabilityReasons).join("；") : "")
      ));
    }

    if (parsed.visualization && parsed.visualization.shouldUse) {
      sections.push(buildSection(
        "visualization",
        "Visualization",
        "可视化建议",
        safeText(parsed.visualization.caption) || safeText(parsed.visualization.reason) || "建议配合图示理解此题。"
      ));
    }

    for (var i = 0; i < sections.length; i += 1) {
      this._commit({
        type: "section",
        payload: sections[i]
      });
    }

    var solutionSteps = Array.isArray(parsed.standardSolution) ? parsed.standardSolution : [];
    for (var j = 0; j < solutionSteps.length; j += 1) {
      var step = solutionSteps[j];
      var stepTitle = safeText(step && step.title) || "标准解答";
      var stepContent = safeText(step && step.content) || safeText(step);
      this._commit({
        type: "step",
        payload: {
          id: "solution-step-" + (j + 1),
          order: (this.store.getState().steps || []).length + 1,
          sectionId: "solution",
          title: stepTitle,
          content: stepContent
        }
      });

      if (window.UI && typeof window.UI.containsMathSyntax === "function" && window.UI.containsMathSyntax(stepContent)) {
        this._commit({
          type: "math",
          payload: {
            text: stepContent
          }
        });
      }
    }

    var suggestions = uniqueList([].concat(parsed.reviewSuggestions || [], review.suggestions || []));
    this._commit({
      type: "insight",
      payload: {
        activeSection: suggestions.length ? "solution" : "summary",
        headline: mainIssue || "分析已完成",
        summary: "输入题目已被转化为逐段推理、问题识别和复习建议。",
        points: suggestions
      }
    });

    this._commit({
      type: "state",
      payload: {
        phase: "completed",
        status: "complete",
        meta: {
          inputLength: inputText.length,
          sectionCount: sections.length,
          stepCount: solutionSteps.length
        }
      }
    });
  }
}
