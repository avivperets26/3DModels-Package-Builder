#include "PackageBuilderPreviewEditorLibrary.h"

#include "Blueprint/WidgetTree.h"
#include "Components/Widget.h"
#include "K2Node_ComponentBoundEvent.h"
#include "Kismet2/KismetEditorUtilities.h"
#include "Modules/ModuleManager.h"
#include "WidgetBlueprint.h"
#include "Blueprint/UserWidget.h"
#include "K2Node.h"
#include "K2Node_CallFunction.h"
#include "Kismet2/BlueprintEditorUtils.h"
#include "EdGraph/EdGraph.h"
#include "EdGraphSchema_K2.h"
#include "Input/Events.h"
#include "Components/Button.h"
#include "Components/Slider.h"
#include "Framework/Application/SlateApplication.h"
#include "Engine/GameViewportClient.h"
#include "Widgets/SViewport.h"
#include "ImageUtils.h"

IMPLEMENT_MODULE(FDefaultModuleImpl, PackageBuilderPreviewEditor)

bool UPackageBuilderPreviewEditorLibrary::RegisterWidgetVariables(UWidgetBlueprint* Blueprint)
{
    if (!IsValid(Blueprint) || !IsValid(Blueprint->WidgetTree))
    {
        return false;
    }

    Blueprint->Modify();
    Blueprint->WidgetTree->ForEachWidget([Blueprint](UWidget* Widget)
    {
        Widget->bIsVariable = true;
        // The factory already registers the root; preserve every existing identity.
        if (!Blueprint->WidgetVariableNameToGuidMap.Contains(Widget->GetFName()))
        {
            Blueprint->OnVariableAdded(Widget->GetFName());
        }
    });
    Blueprint->MarkPackageDirty();
    return true;
}

bool UPackageBuilderPreviewEditorLibrary::ClickPreviewButton(UUserWidget* Widget, FName Name)
{
    UButton* Button = Widget ? Cast<UButton>(Widget->GetWidgetFromName(Name)) : nullptr;
    if (!Button) { return false; }
    Button->OnClicked.Broadcast();
    return true;
}

bool UPackageBuilderPreviewEditorLibrary::SetPreviewSlider(UUserWidget* Widget, FName Name, float Value)
{
    USlider* Slider = Widget ? Cast<USlider>(Widget->GetWidgetFromName(Name)) : nullptr;
    if (!Slider) { return false; }
    Slider->SetValue(Value);
    Slider->OnValueChanged.Broadcast(Value);
    return true;
}

bool UPackageBuilderPreviewEditorLibrary::CapturePreview(UUserWidget* Widget, FString Filename)
{
    UGameViewportClient* Client = Widget && Widget->GetWorld() ? Widget->GetWorld()->GetGameViewport() : nullptr;
    if (!Client || !Client->GetGameViewportWidget().IsValid()) { return false; }
    TArray<FColor> Pixels;
    FIntVector Size;
    if (!FSlateApplication::Get().TakeScreenshot(Client->GetGameViewportWidget().ToSharedRef(), Pixels, Size)) { return false; }
    for (FColor& Pixel : Pixels) { Pixel.A = 255; }
    return FImageUtils::SaveImageByExtension(*Filename, FImageView(Pixels.GetData(), Size.X, Size.Y));
}

UK2Node_ComponentBoundEvent* UPackageBuilderPreviewEditorLibrary::BindWidgetEvent(
    UWidgetBlueprint* Blueprint, FName WidgetName, FName EventName)
{
    if (!IsValid(Blueprint) || !IsValid(Blueprint->SkeletonGeneratedClass))
    {
        return nullptr;
    }
    FObjectProperty* Property = FindFProperty<FObjectProperty>(Blueprint->SkeletonGeneratedClass, WidgetName);
    if (!Property || !FindFProperty<FMulticastDelegateProperty>(Property->PropertyClass, EventName))
    {
        return nullptr;
    }
    if (const UK2Node_ComponentBoundEvent* Existing = FKismetEditorUtilities::FindBoundEventForComponent(Blueprint, EventName, WidgetName))
    {
        // The lookup exposes a const view; this authoring operation owns the mutable blueprint.
        return const_cast<UK2Node_ComponentBoundEvent*>(Existing);
    }
    FKismetEditorUtilities::CreateNewBoundEventForClass(Property->PropertyClass, EventName, Blueprint, Property, false);
    return const_cast<UK2Node_ComponentBoundEvent*>(
        FKismetEditorUtilities::FindBoundEventForComponent(Blueprint, EventName, WidgetName));
}

UK2Node* UPackageBuilderPreviewEditorLibrary::AddCreateWidget(
    UEdGraph* Graph, TSubclassOf<UUserWidget> WidgetClass)
{
    if (!IsValid(Graph) || !WidgetClass) { return nullptr; }
    // The concrete node header is private to UMGEditor; use its reflected class and public base.
    UClass* NodeClass = LoadClass<UK2Node>(nullptr, TEXT("/Script/UMGEditor.K2Node_CreateWidget"));
    if (!NodeClass) { return nullptr; }
    UK2Node* Node = NewObject<UK2Node>(Graph, NodeClass);
    Graph->AddNode(Node, false, false);
    Node->CreateNewGuid();
    Node->PostPlacedNewNode();
    Node->AllocateDefaultPins();
    UEdGraphPin* ClassPin = Node->FindPin(TEXT("Class"));
    if (!ClassPin) { return nullptr; }
    Graph->GetSchema()->TrySetDefaultObject(*ClassPin, WidgetClass);
    return Node;
}

bool UPackageBuilderPreviewEditorLibrary::SendPreviewKey(UUserWidget* Widget, FName KeyName, bool Shift)
{
    if (!IsValid(Widget)) { return false; }
    const FModifierKeysState Modifiers(Shift, false, false, false, false, false, false, false, false);
    return Widget->OnPreviewKeyDown(Widget->GetCachedGeometry(),
        FKeyEvent(FKey(KeyName), Modifiers, 0, false, 0, 0)).NativeReply.IsEventHandled();
}

UK2Node_CallFunction* UPackageBuilderPreviewEditorLibrary::AddNativeCall(UEdGraph* Graph, FString FunctionPath)
{
    if (!IsValid(Graph)) { return nullptr; }
    UBlueprint* Blueprint = FBlueprintEditorUtils::FindBlueprintForGraph(Graph);
    UFunction* Function = FunctionPath.StartsWith(TEXT("/"))
        ? TSoftObjectPtr<UFunction>(FSoftObjectPath(FunctionPath)).LoadSynchronous()
        : (Blueprint && Blueprint->SkeletonGeneratedClass
            ? Blueprint->SkeletonGeneratedClass->FindFunctionByName(*FunctionPath) : nullptr);
    if (!Function) { return nullptr; }
    FGraphNodeCreator<UK2Node_CallFunction> Creator(*Graph);
    UK2Node_CallFunction* Node = Creator.CreateNode();
    Node->SetFromFunction(Function);
    Creator.Finalize();
    return Node;
}

bool UPackageBuilderPreviewEditorLibrary::SendPreviewPointer(
    UUserWidget* Widget, FName Action, FVector2D Delta, float Wheel)
{
    if (!IsValid(Widget)) { return false; }
    TSet<FKey> Pressed;
    if (Action == "down" || Action == "move") { Pressed.Add(EKeys::LeftMouseButton); }
    const FPointerEvent Event(0, Delta, FVector2D::ZeroVector, Pressed, EKeys::LeftMouseButton, Wheel, FModifierKeysState());
    const FGeometry Geometry = Widget->GetCachedGeometry();
    if (Action == "down") { return Widget->OnMouseButtonDown(Geometry, Event).NativeReply.IsEventHandled(); }
    if (Action == "move") { return Widget->OnMouseMove(Geometry, Event).NativeReply.IsEventHandled(); }
    if (Action == "up") { return Widget->OnMouseButtonUp(Geometry, Event).NativeReply.IsEventHandled(); }
    if (Action == "wheel") { return Widget->OnMouseWheel(Geometry, Event).NativeReply.IsEventHandled(); }
    return false;
}
