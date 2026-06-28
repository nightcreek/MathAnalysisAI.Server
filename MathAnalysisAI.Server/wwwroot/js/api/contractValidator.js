(function () {
  var warned = {};

  function getType(value) {
    if (value === null) return "null";
    if (Array.isArray(value)) return "array";
    return typeof value;
  }

  function typeMatches(actualType, expectedType) {
    if (Array.isArray(expectedType)) {
      return expectedType.indexOf(actualType) >= 0;
    }
    return actualType === expectedType;
  }

  function validateSchema(schema, value, path, mismatches) {
    if (!schema) return;

    var actualType = getType(value);
    if (schema.type && !typeMatches(actualType, schema.type)) {
      mismatches.push(path + ": expected " + JSON.stringify(schema.type) + ", got " + actualType);
      return;
    }

    if (actualType === "object" && schema.required && value) {
      schema.required.forEach(function (key) {
        if (!(key in value)) {
          mismatches.push(path + "." + key + ": missing required property");
        }
      });
    }

    if (actualType === "object" && schema.properties && value) {
      Object.keys(schema.properties).forEach(function (key) {
        if (!(key in value)) return;
        validateSchema(schema.properties[key], value[key], path + "." + key, mismatches);
      });
    }

    if (actualType === "array" && schema.items && Array.isArray(value)) {
      for (var i = 0; i < value.length; i++) {
        validateSchema(schema.items, value[i], path + "[" + i + "]", mismatches);
      }
    }
  }

  function warnOnce(key, message, details) {
    if (warned[key]) return;
    warned[key] = true;
    console.warn(message, details);
  }

  function validate(contractDef, responseData) {
    if (!contractDef || !contractDef.responseSchema || contractDef.responseSchema.streaming) {
      return;
    }

    var mismatches = [];
    validateSchema(contractDef.responseSchema, responseData, contractDef.key || contractDef.endpoint || "response", mismatches);
    if (!mismatches.length) {
      return;
    }

    warnOnce(
      (contractDef.key || contractDef.endpoint || "unknown") + ":" + mismatches.join("|"),
      "[ContractValidator] Response shape mismatch detected.",
      {
        contract: contractDef.key || contractDef.endpoint || "unknown",
        endpoint: contractDef.endpoint || contractDef.endpointTemplate || "",
        mismatches: mismatches
      }
    );
  }

  window.ApiContractValidator = {
    validate: validate
  };
})();
