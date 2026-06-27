#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
OUTPUT_FILE="${REPO_ROOT}/architecture-violations.json"

VIOLATION_COUNT=0
JSON_BODY=""

log() {
  printf '%s\n' "$*"
}

json_escape() {
  printf '%s' "$1" | tr '\n' ' ' | awk '
    BEGIN { ORS="" }
    {
      gsub(/\\/,"\\\\");
      gsub(/"/,"\\\"");
      gsub(/\t/,"\\t");
      gsub(/\r/,"\\r");
      gsub(/\f/,"\\f");
      print;
    }'
}

append_violation() {
  local rule="$1"
  local file="$2"
  local line="$3"
  local severity="$4"
  local message="$5"
  local snippet="$6"

  local escaped_rule escaped_file escaped_message escaped_snippet
  escaped_rule="$(json_escape "${rule}")"
  escaped_file="$(json_escape "${file}")"
  escaped_message="$(json_escape "${message}")"
  escaped_snippet="$(json_escape "${snippet}")"

  if [[ "${VIOLATION_COUNT}" -gt 0 ]]; then
    JSON_BODY+=","
  fi

  JSON_BODY+="
    {
      \"rule\": \"${escaped_rule}\",
      \"file\": \"${escaped_file}\",
      \"line\": ${line},
      \"severity\": \"${severity}\",
      \"message\": \"${escaped_message}\",
      \"snippet\": \"${escaped_snippet}\"
    }"

  printf '::error file=%s,line=%s::[%s] %s | %s\n' "${file}" "${line}" "${severity}" "${rule}" "${message}"
  VIOLATION_COUNT=$((VIOLATION_COUNT + 1))
}

record_matches() {
  local rule="$1"
  local severity="$2"
  local message="$3"
  shift 3

  while IFS=: read -r file line snippet; do
    [[ -n "${file}" ]] || continue
    append_violation "${rule}" "${file}" "${line}" "${severity}" "${message}" "${snippet}"
  done < <("$@" | sort -t: -k1,1 -k2,2n -u)
}

cs_files() {
  find "${REPO_ROOT}/MathAnalysisAI.Server" -type f -name '*.cs' \
    ! -path '*/bin/*' \
    ! -path '*/obj/*' \
    | LC_ALL=C sort
}

server_files_excluding() {
  local exclude_pattern="$1"
  cs_files | grep -v -E "${exclude_pattern}" || true
}

scan_pattern_in_files() {
  local pattern="$1"
  shift
  if [[ "$#" -eq 0 ]]; then
    return 0
  fi
  grep -nH -E "${pattern}" "$@" || true
}

build_file_array() {
  local pattern="$1"
  local result_name="$2"
  local line
  local files=()

  while IFS= read -r line; do
    [[ -n "${line}" ]] || continue
    files+=("${line}")
  done < <(server_files_excluding "${pattern}")

  eval "${result_name}=(\"\${files[@]}\")"
}

build_file_array '^.*/Services/Analysis/Persistence/|^.*/Data/|^.*/Migrations/' db_excluded_files
build_file_array '^.*/Services/Analysis/Persistence/|^.*/Data/|^.*/Migrations/|^.*/Program\.cs$' ef_excluded_files
build_file_array '^.*/Services/LLM/|^.*/Services/OCR/|^.*/Program\.cs$' provider_excluded_files
build_file_array '^.*/Controllers/' non_controller_files
build_file_array '^.*/Services/Analysis/UAO/' non_uao_files
build_file_array '^.*/Services/Analysis/Domain/' non_domain_files
all_server_files=()
while IFS= read -r file; do
  [[ -n "${file}" ]] || continue
  all_server_files+=("${file}")
done < <(cs_files)

write_result_file() {
  cat > "${OUTPUT_FILE}" <<EOF
{
  "scannedRevision": "$(git -C "${REPO_ROOT}" rev-parse HEAD)",
  "deterministic": true,
  "violationCount": ${VIOLATION_COUNT},
  "violations": [${JSON_BODY}
  ]
}
EOF
}

log "Architecture guard started."

# A. DB leakage: ApplicationDbContext outside persistence/data/migrations
record_matches \
  "DB_LEAKAGE_APPLICATION_DBCONTEXT" \
  "error" \
  "ApplicationDbContext must not be used outside PersistenceModule." \
  scan_pattern_in_files '\bApplicationDbContext\b' "${db_excluded_files[@]}"

# A/B. EF Core imports outside persistence/data/migrations/startup
record_matches \
  "FORBIDDEN_IMPORT_ENTITY_FRAMEWORK_CORE" \
  "error" \
  "Microsoft.EntityFrameworkCore must not be imported outside persistence, data, migrations, or startup." \
  scan_pattern_in_files '^[[:space:]]*using[[:space:]]+Microsoft\.EntityFrameworkCore\b' "${ef_excluded_files[@]}"

# B. Direct LLM provider leakage outside LLM/OCR service modules and startup
record_matches \
  "FORBIDDEN_PROVIDER_USAGE_LLM_GATEWAY" \
  "error" \
  "Direct LLMGateway usage is forbidden outside LLM service module or startup composition root." \
  scan_pattern_in_files '\bLLMGateway\b' "${provider_excluded_files[@]}"

record_matches \
  "FORBIDDEN_PROVIDER_USAGE_OCR_PROVIDER" \
  "error" \
  "Direct OCR provider usage is forbidden outside OCR service module or startup composition root." \
  scan_pattern_in_files '\b(IPhotoSolutionOcrProvider|LiteLLMPhotoSolutionOcrProvider)\b' "${provider_excluded_files[@]}"

# C. Controller fatness: db usage and business-logic heuristics
controller_files=()
while IFS= read -r file; do
  [[ -n "${file}" ]] || continue
  controller_files+=("${file}")
done < <(find "${REPO_ROOT}/MathAnalysisAI.Server/Controllers" -maxdepth 1 -type f -name '*.cs' | LC_ALL=C sort)

record_matches \
  "CONTROLLER_DB_USAGE" \
  "error" \
  "Controllers must be thin and must not use DbContext or EF query APIs." \
  scan_pattern_in_files '\b(ApplicationDbContext|DbContext|SaveChangesAsync|FirstOrDefaultAsync|ToListAsync|AnyAsync|AsNoTracking|AddRange\(|Add\(|Update\(|Remove\(|FindAsync\()\b?' "${controller_files[@]}"

record_matches \
  "CONTROLLER_PROVIDER_USAGE" \
  "error" \
  "Controllers must not use direct LLM/OCR providers or gateways." \
  scan_pattern_in_files '\b(LLMGateway|IPhotoSolutionOcrProvider|LiteLLMPhotoSolutionOcrProvider)\b' "${controller_files[@]}"

record_matches \
  "CONTROLLER_BUSINESS_LOGIC_HEURISTIC" \
  "warning" \
  "Controller contains business-logic helper patterns. Review for thin-controller compliance." \
  scan_pattern_in_files '\b(private[[:space:]]+(static[[:space:]]+)?(List<|string|bool|int|decimal|DateTime|Task<|Task\b)|JsonSerializer\.|SHA256|ComputeSha256|Normalize[A-Za-z0-9_]*\(|Parse[A-Za-z0-9_]*\(|Assess[A-Za-z0-9_]*\(|BuildRecognized[A-Za-z0-9_]*\()' "${controller_files[@]}"

# D. UAO violations: EF, controllers, HTTP, DTO/frontend leakage
uao_files=()
while IFS= read -r file; do
  [[ -n "${file}" ]] || continue
  uao_files+=("${file}")
done < <(find "${REPO_ROOT}/MathAnalysisAI.Server/Services/Analysis/UAO" -type f -name '*.cs' | LC_ALL=C sort)

record_matches \
  "UAO_FORBIDDEN_HTTP_OR_CONTROLLER_DEPENDENCY" \
  "error" \
  "UAO layer must not depend on ASP.NET HTTP/controller types." \
  scan_pattern_in_files '^[[:space:]]*using[[:space:]]+(Microsoft\.AspNetCore|System\.Net\.Http|MathAnalysisAI\.Server\.Controllers)\b|\b(ActionResult|ControllerBase|HttpContext|HttpRequest|HttpResponse)\b' "${uao_files[@]}"

record_matches \
  "UAO_FORBIDDEN_EF_DEPENDENCY" \
  "error" \
  "UAO layer must not depend on EF Core or ApplicationDbContext." \
  scan_pattern_in_files '^[[:space:]]*using[[:space:]]+Microsoft\.EntityFrameworkCore\b|\bApplicationDbContext\b' "${uao_files[@]}"

record_matches \
  "UAO_FORBIDDEN_DTO_FRONTEND_COUPLING" \
  "error" \
  "UAO layer must not depend on HTTP DTOs or frontend/rendering contracts." \
  scan_pattern_in_files '^[[:space:]]*using[[:space:]]+MathAnalysisAI\.Server\.DTOs\.' "${uao_files[@]}"

# Domain leakage: AnalysisResultModel must not depend on DTO/frontend coupling
domain_files=()
while IFS= read -r file; do
  [[ -n "${file}" ]] || continue
  domain_files+=("${file}")
done < <(find "${REPO_ROOT}/MathAnalysisAI.Server/Services/Analysis/Domain" -type f -name '*.cs' | LC_ALL=C sort)

record_matches \
  "DOMAIN_DTO_OR_FRONTEND_COUPLING" \
  "error" \
  "AnalysisResult domain layer must not depend on DTO or frontend contracts." \
  scan_pattern_in_files '^[[:space:]]*using[[:space:]]+MathAnalysisAI\.Server\.DTOs\.|^[[:space:]]*using[[:space:]]+Microsoft\.AspNetCore\b' "${domain_files[@]}"

# E. MAMP leakage and frontend rendering leakage
record_matches \
  "MAMP_LEAKAGE_BACKEND_RENDERING_PROTOCOL" \
  "error" \
  "Backend C# code must not embed MAMP/frontend rendering protocol concerns." \
  scan_pattern_in_files '\b(MAMP|MathJax|KaTeX|Katex|renderMath|renderMarkup|frontend rendering)\b' "${non_domain_files[@]}"

record_matches \
  "MATH_MARKUP_LEAKAGE_BACKEND" \
  "warning" \
  "Backend C# code appears to embed raw math markup/render strings. Review boundary ownership." \
  scan_pattern_in_files '(<math[[:space:]>]|</math>|\\\(|\\\[|\\\]|\\\)|<mi>|<mo>|<mn>)' "${non_domain_files[@]}"

# No AST / symbolic computation system
record_matches \
  "FORBIDDEN_AST_OR_SYMBOLIC_USAGE" \
  "error" \
  "AST or symbolic computation APIs are forbidden by architecture policy." \
  scan_pattern_in_files '\b(Microsoft\.CodeAnalysis|SyntaxTree|SyntaxNode|SyntaxToken|SemanticModel|CSharpSyntaxTree|CSharpSyntaxWalker|CSharpSyntaxRewriter|IOperation|ExpressionVisitor|System\.Linq\.Expressions)\b' "${all_server_files[@]}"

write_result_file

log "Architecture guard wrote ${OUTPUT_FILE}"
cat "${OUTPUT_FILE}"

if [[ "${VIOLATION_COUNT}" -gt 0 ]]; then
  log "Architecture guard failed with ${VIOLATION_COUNT} violation(s)."
  exit 1
fi

log "Architecture guard passed."
