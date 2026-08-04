namespace PackageBuilder.App.Wpf.Navigation;

/// <summary>Creates the deterministic version-one shell destinations and honest placeholder copy.</summary>
public static class ShellModuleCatalog
{
    public static IReadOnlyList<ShellModuleViewModel> Create() =>
        [
            new(
                "overview",
                "Overview",
                "_Overview",
                "O",
                "WORKSPACE OVERVIEW",
                "Build game-ready packages with confidence.",
                "The secure local foundation is connected. Product creation screens arrive in the next guided workflow tasks.",
                "Foundation ready",
                "Next: run the environment audit, then create your first product draft.",
                [
                    new("Core pipeline", "Ready", "Persisted jobs can run, pause, resume, and fail safely."),
                    new("Data location", "Local", "Project-owned files stay beneath the approved workspace."),
                    new("Publishing", "Manual", "Nothing is uploaded or published without your action."),
                ]),
            new(
                "products",
                "Products",
                "_Products",
                "P",
                "PRODUCT WORKSPACE",
                "Shape the package before any engine runs.",
                "Product profiles, source inspection, and the guided creation wizard are planned and are not active in this shell yet.",
                "Module preview",
                "PB-1303 through PB-1305 will add profiles, product drafts, and inspected sources.",
                [
                    new("Product cases", "5", "Static, rigged, animated, item set, and collection."),
                    new("Targets", "3", "Portable, Unity, and Unreal remain isolated adapters."),
                    new("Publisher root", "Configurable", "Publisher identity is never hard-coded."),
                ]),
            new(
                "build-queue",
                "Build queue",
                "_Build queue",
                "B",
                "BUILD ORCHESTRATION",
                "Every stage has an accountable state.",
                "The persisted orchestrator is ready. Live queue controls and progress visualization arrive in PB-1310.",
                "Core ready",
                "PB-1310 will connect queued jobs, elapsed time, artifacts, and correlation IDs.",
                [
                    new("Resume", "Supported", "Interrupted work continues from its exact persisted stage."),
                    new("Review pause", "Explicit", "Inspection can wait safely for a human decision."),
                    new("Promotion", "Fail closed", "Failed work is never reported as a successful release."),
                ]),
            new(
                "validation",
                "Validation",
                "_Validation",
                "V",
                "QUALITY AND FINDINGS",
                "Know what blocks release and how to fix it.",
                "Structured findings exist in the core. The searchable report viewer arrives in PB-1311.",
                "Contract ready",
                "PB-1311 will surface findings, logs, severity, evidence, and corrective actions.",
                [
                    new("Finding levels", "4", "Info, warning, error, and fatal remain explicit."),
                    new("Release gates", "Fail closed", "Missing or contradictory evidence blocks release."),
                    new("Diagnostics", "Redacted", "Logs are structured and remove credential-like content."),
                ]),
            new(
                "settings",
                "Settings",
                "_Settings",
                "S",
                "LOCAL CONFIGURATION",
                "One contained workspace. Clear tool choices.",
                "Settings are intentionally read-only placeholders until PB-1315 adds validated persistence.",
                "Preview only",
                "PB-1302 will first audit tools and paths; PB-1315 will add safe editable preferences.",
                [
                    new("Workspace", "Contained", "Project-owned state remains beneath the approved root."),
                    new("Cloud uploads", "Off", "No telemetry or remote processing is enabled."),
                    new("Tool policy", "Pinned", "Approved stable tool versions remain reproducible."),
                ]),
        ];
}
