namespace DevEn.Xrm.EntityValidation.Configuration
{
    /// <summary>
    /// Dataverse plugin execution pipeline stages. The numeric values match exactly what
    /// <c>IPluginExecutionContext.Stage</c> returns.
    /// </summary>
    internal enum PipelineStage
    {
        PreValidation = 10,
        PreOperation = 20,
        PostOperation = 40
    }
}
