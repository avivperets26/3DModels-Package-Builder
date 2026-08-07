namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Stable process exit codes consumed by the Package Builder orchestrator.</summary>
    internal enum UnityWorkerExitCode
    {
        Success = 0,
        InvocationFailure = 2,
        InvalidRequest = 3,
        UnsupportedOperation = 4,
        ExecutionFailure = 5,
        ResultWriteFailure = 6,
        Cancelled = 7,
    }
}
