namespace CatiaAIAssistant.Core.Projects
{
    /// <summary>Explicit engineering workflow stages (Spec §03). Order matters: forward moves go one step at a time.</summary>
    public enum WorkflowStage
    {
        Draft = 0,
        Requirements = 1,
        Analysis = 2,
        Design = 3,
        Validation = 4,
        Execution = 5,
        Review = 6,
        Completed = 7
    }
}
