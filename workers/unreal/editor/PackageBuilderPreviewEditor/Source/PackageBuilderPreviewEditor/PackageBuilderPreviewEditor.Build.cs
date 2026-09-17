using UnrealBuildTool;

/// <summary>Build-only bridge to native widget authoring; no runtime module is exported.</summary>
public class PackageBuilderPreviewEditor : ModuleRules
{
    public PackageBuilderPreviewEditor(ReadOnlyTargetRules target) : base(target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
        PublicDependencyModuleNames.AddRange(new[] { "Core", "CoreUObject", "Engine", "UMG", "UMGEditor", "BlueprintGraph" });
        PrivateDependencyModuleNames.AddRange(new[] { "UnrealEd", "Slate", "SlateCore", "InputCore", "ImageCore" });
    }
}
