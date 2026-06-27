using System.Text.Json;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.SharedKernel.Analysis;
using MathAnalysisAI.Server.Services.Analysis.Persistence;

namespace MathAnalysisAI.Server.Services.Visualization
{
    public class VisualizationService : IVisualizationService
    {
        private readonly IPersistenceService _persistenceService;
        private readonly IGeoGebraCommandValidator _commandValidator;

        public VisualizationService(
            IPersistenceService persistenceService,
            IGeoGebraCommandValidator commandValidator)
        {
            _persistenceService = persistenceService;
            _commandValidator = commandValidator;
        }

        public async Task<AnalysisVisualization?> SaveVisualizationAsync(
            int analysisResultId,
            VisualizationSpec visualization,
            CancellationToken cancellationToken = default)
        {
            if (analysisResultId <= 0 || visualization == null)
            {
                return null;
            }

            // Strategy: persist an explicit "none" record when should_use=false for auditability.
            if (!visualization.ShouldUse)
            {
                var noneRecord = new AnalysisVisualization
                {
                    AnalysisResultId = analysisResultId,
                    Engine = "none",
                    VisualizationType = "none",
                    CommandsJson = JsonSerializer.Serialize(Array.Empty<string>()),
                    ViewConfigJson = "{}",
                    StepBindingJson = "{}",
                    Caption = visualization.Caption,
                    ValidationStatus = "skipped",
                    ValidationMessage = "Visualization disabled by upstream decision.",
                    CreatedAt = DateTime.UtcNow
                };

                return await _persistenceService.CreateAnalysisVisualizationAsync(
                    new CreateAnalysisVisualizationCommand(noneRecord),
                    cancellationToken);
            }

            var validation = _commandValidator.Validate(visualization.GeoGebraCommands);
            var isValid = validation.IsValid;

            var sanitizedCommands = validation.ValidCommands;
            var validationMessage = validation.Errors.Count == 0
                ? null
                : string.Join(" | ", validation.Errors.Take(8));

            var entity = new AnalysisVisualization
            {
                AnalysisResultId = analysisResultId,
                Engine = string.IsNullOrWhiteSpace(visualization.Engine) ? "geogebra" : visualization.Engine.Trim(),
                VisualizationType = visualization.VisualizationType,
                CommandsJson = JsonSerializer.Serialize(sanitizedCommands),
                ViewConfigJson = "{}",
                StepBindingJson = "{}",
                Caption = visualization.Caption,
                ValidationStatus = isValid ? "valid" : "invalid",
                ValidationMessage = validationMessage,
                CreatedAt = DateTime.UtcNow
            };

            return await _persistenceService.CreateAnalysisVisualizationAsync(
                new CreateAnalysisVisualizationCommand(entity),
                cancellationToken);
        }
    }
}
