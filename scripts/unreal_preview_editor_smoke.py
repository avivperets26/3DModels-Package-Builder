"""Run inside the owned Unreal commandlet; verify widget registration and native compilation."""

import json
import os
from pathlib import Path

import unreal as u

settings = u.get_default_object(
    u.load_class(None, "/Script/UMGEditor.WidgetEditingProjectSettings")
)
previous = settings.get_editor_property("DefaultRootWidget")
try:
    settings.set_editor_property("DefaultRootWidget", u.VerticalBox)
    blueprint = u.AssetToolsHelpers.get_asset_tools().create_asset(
        "WBP_Probe", "/Game/PBEditorBuild/Preview", u.WidgetBlueprint, u.WidgetBlueprintFactory()
    )
finally:
    settings.set_editor_property("DefaultRootWidget", previous)
tree = u.find_object(blueprint, "WidgetTree")
root = u.find_object(tree, "VerticalBox_0")
button = u.new_object(u.Button, outer=tree, name="ResetView")
root.add_child(button)
text = u.new_object(u.TextBlock, outer=tree, name="ResetViewLabel")
text.set_text("Reset View")
button.add_child(text)
for _ in range(2):
    if not u.PackageBuilderPreviewEditorLibrary.register_widget_variables(blueprint):
        raise RuntimeError("Widget identity registration failed.")
if not u.BlueprintEditorLibrary.compile_blueprint(blueprint):
    raise RuntimeError("Generated widget compilation failed.")
event = u.PackageBuilderPreviewEditorLibrary.bind_widget_event(blueprint, "ResetView", "OnClicked")
if event is None or event != u.PackageBuilderPreviewEditorLibrary.bind_widget_event(
    blueprint, "ResetView", "OnClicked"
):
    raise RuntimeError("Widget event binding was not idempotent.")
if not u.BlueprintEditorLibrary.compile_blueprint(blueprint):
    raise RuntimeError("Bound widget compilation failed.")
if not u.EditorAssetLibrary.save_loaded_asset(blueprint, only_if_is_dirty=False):
    raise RuntimeError("Generated widget save failed.")
Path(os.environ["PB_HELPER_EVIDENCE"], "widget.json").write_text(
    json.dumps(
        {"compiled": True, "saved": True, "idempotentRegistration": True, "boundEvent": True}
    ),
    encoding="utf-8",
)
