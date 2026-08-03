namespace PackageBuilder.Contracts.Processes;

/// <summary>Runs one contained executable directly with literal arguments and bounded stream capture.</summary>
public interface IExternalProcessRunner
{
    Task<ProcessExecutionOperationResult> RunAsync(ExternalProcessRequest request);
}
