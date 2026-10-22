using System;
using System.Collections.Generic;
using CatiaAIAssistant.Core.Logging;

namespace CatiaAIAssistant.Engineering
{
    public enum EngineeringDomain
    {
        Unknown,
        BasicBlockOrProfile,
        SpurGearTransmission,
        SteppedShaftAndBearing,
        FlangeCoupling,
        CrankSliderPiston,
        QuadcopterSystem,
        ToyCarSystem
    }

    public enum ParameterStatus
    {
        SpecifiedByUser,
        Calculated,
        AssumedByEngineeringRule
    }

    public sealed class EngineeringParameter
    {
        public string Key { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; init; } = "mm";
        public ParameterStatus Status { get; set; }
        public string EngineeringEffect { get; init; } = string.Empty;

        public string StatusBadge => Status switch
        {
            ParameterStatus.SpecifiedByUser => "مشخص‌شده توسط کاربر",
            ParameterStatus.Calculated => "محاسبه‌شده",
            ParameterStatus.AssumedByEngineeringRule => "فرض مهندسی",
            _ => "نامشخص"
        };
    }

    public sealed class EngineeringCalculationRecord
    {
        public string Title { get; init; } = string.Empty;
        public string Formula { get; init; } = string.Empty;
        public string InputsSummary { get; init; } = string.Empty;
        public double ResultValue { get; init; }
        public string Unit { get; init; } = string.Empty;
        public string AssumptionsAndLimits { get; init; } = string.Empty;
    }

    public sealed class CadExecutionStep
    {
        public string StepId { get; init; } = string.Empty;
        public string TargetDocumentName { get; init; } = string.Empty;
        public string OperationType { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public Dictionary<string, double> NumericInputs { get; } = new();
        public List<string> PrerequisiteStepIds { get; } = new();
        public string SuccessCriteria { get; init; } = string.Empty;
        public OperationState State { get; set; } = OperationState.Pending;
        public string? OutputFilePath { get; set; }
    }

    public sealed class EngineeringDesignSpecification
    {
        public string ProjectTitle { get; set; } = string.Empty;
        public string RawUserPrompt { get; set; } = string.Empty;
        public EngineeringDomain Domain { get; set; } = EngineeringDomain.Unknown;
        public string EngineeringGoal { get; set; } = string.Empty;
        public bool IsPreliminaryConcept { get; set; }
        public bool CanExecuteNow { get; set; } = true;

        public Dictionary<string, EngineeringParameter> Parameters { get; } = new();
        public List<string> Assumptions { get; } = new();
        public List<string> ClarifyingQuestions { get; } = new();
        public List<string> ContradictionsAndWarnings { get; } = new();
        public List<EngineeringCalculationRecord> Calculations { get; } = new();
        public List<CadExecutionStep> ExecutionPlan { get; } = new();
    }
}