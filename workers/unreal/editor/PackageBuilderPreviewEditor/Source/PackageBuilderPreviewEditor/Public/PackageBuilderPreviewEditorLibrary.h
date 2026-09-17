#pragma once

#include "Kismet/BlueprintFunctionLibrary.h"
#include "PackageBuilderPreviewEditorLibrary.generated.h"

class UWidgetBlueprint;
class UK2Node_ComponentBoundEvent;
class UK2Node;
class UK2Node_CallFunction;
class UEdGraph;
class UUserWidget;

/** Editor-only authoring operations missing from Unreal's Python reflection surface. */
UCLASS()
class PACKAGEBUILDERPREVIEWEDITOR_API UPackageBuilderPreviewEditorLibrary : public UBlueprintFunctionLibrary
{
    GENERATED_BODY()

public:
    /** Register generated widget identities before compilation; never called from runtime graphs. */
    UFUNCTION(BlueprintCallable, Category = "Package Builder|Editor")
    static bool RegisterWidgetVariables(UWidgetBlueprint* Blueprint);

    /** Bind a widget delegate through the editor API after a successful skeleton compilation. */
    UFUNCTION(BlueprintCallable, Category = "Package Builder|Editor")
    static UK2Node_ComponentBoundEvent* BindWidgetEvent(UWidgetBlueprint* Blueprint, FName WidgetName, FName EventName);

    /** Author the engine's typed widget construction node; the helper is never a runtime dependency. */
    UFUNCTION(BlueprintCallable, Category = "Package Builder|Editor")
    static UK2Node* AddCreateWidget(UEdGraph* Graph, TSubclassOf<UUserWidget> WidgetClass);

    /** Create a plain function node without UE's interactive operator-spawner UI dependency. */
    UFUNCTION(BlueprintCallable, Category = "Package Builder|Editor")
    static UK2Node_CallFunction* AddNativeCall(UEdGraph* Graph, FString FunctionPath);

    /** Exercise the generated widget's real keyboard handler in native acceptance tests. */
    UFUNCTION(BlueprintCallable, Category = "Package Builder|Editor")
    static bool SendPreviewKey(UUserWidget* Widget, FName KeyName, bool Shift = false);

    /** Exercise down/move/up/wheel handlers without changing desktop input or product state directly. */
    UFUNCTION(BlueprintCallable, Category = "Package Builder|Editor")
    static bool SendPreviewPointer(UUserWidget* Widget, FName Action, FVector2D Delta, float Wheel = 0);

    /** Invoke native UMG delegates for acceptance; never shipped with the customer project. */
    UFUNCTION(BlueprintCallable, Category = "Package Builder|Editor")
    static bool ClickPreviewButton(UUserWidget* Widget, FName Name);

    UFUNCTION(BlueprintCallable, Category = "Package Builder|Editor")
    static bool SetPreviewSlider(UUserWidget* Widget, FName Name, float Value);

    /** Capture the actual PIE viewport including its UMG overlay to an owned evidence path. */
    UFUNCTION(BlueprintCallable, Category = "Package Builder|Editor")
    static bool CapturePreview(UUserWidget* Widget, FString Filename);
};
