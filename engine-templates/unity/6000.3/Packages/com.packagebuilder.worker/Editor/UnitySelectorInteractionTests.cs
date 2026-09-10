using System;
using System.Linq;
using PackageBuilder.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Runs real IMGUI pointer and keyboard input across Editor updates after a clean equipment package import.</summary>
    internal sealed class UnitySelectorInteractionTests : EditorWindow
    {
        private PackageBuilderItemSelector selector;
        private PackageBuilderPreviewController controller;
        private string requestedFocus;
        private bool hasDrawn;
        private int step;
        private double nextUpdate;
        private double deadline;

        /// <summary>Starts a bounded asynchronous UI test; Unity exits only after all input effects have been observed.</summary>
        public static void Run()
        {
            string scenePath = Environment.GetEnvironmentVariable("PACKAGEBUILDER_SELECTOR_SCENE") ?? "Assets/PBEquipmentTests/Scenes/S_EquipmentSet_Overview.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var window = CreateInstance<UnitySelectorInteractionTests>();
            window.controller = scene.GetRootGameObjects().Single(root => root.name == "PackageBuilderOverview").GetComponent<PackageBuilderPreviewController>();
            window.selector = window.controller.ItemSelector;
            window.position = new Rect(0, 0, 260, 230);
            window.deadline = EditorApplication.timeSinceStartup + 30;
            window.ShowUtility();
            window.Focus();
            window.Repaint();
            EditorApplication.update += window.Tick;
        }

        private void Tick()
        {
            try
            {
                double now = EditorApplication.timeSinceStartup;
                Require(now < deadline, "Timed out awaiting selector UI input at step " + step + "; drawn=" + hasDrawn);
                if (now < nextUpdate) { return; }
                nextUpdate = now + 0.1;
                Repaint();
                if (!hasDrawn) { return; }
                switch (step++)
                {
                    case 0: selector.ShowAll(); Click(180, 48); break;
                    case 1: Require(selector.SelectedIndex == 0, "Pointer Next failed."); Click(60, 48); break;
                    case 2: Require(selector.SelectedIndex == selector.Count - 1, "Pointer Previous failed to wrap."); Click(60, 82); break;
                    case 3: Click(60, 116); break;
                    case 4: Require(selector.SelectedIndex == 0, "Pointer direct picker failed."); Click(180, 82); break;
                    case 5:
                        Require(selector.SelectedIndex == -1, "Pointer overview restore failed.");
                        requestedFocus = PackageBuilderItemSelector.PreviousControl; Key(KeyCode.Tab); break;
                    case 6: Key(KeyCode.Return); break;
                    case 7:
                        Require(selector.SelectedIndex == 0, "Controller Tab/Enter failed without animation transport.");
                        requestedFocus = PackageBuilderItemSelector.SelectControl; Key(KeyCode.End); break;
                    case 8:
                        Require(selector.SelectedIndex == selector.Count - 1, "Controller direct keyboard selection failed.");
                        controller.SetControlsVisible(false); Key(KeyCode.Home); break;
                    case 9:
                        Require(selector.SelectedIndex == selector.Count - 1, "Hidden controls consumed a selection command.");
                        if (selector.Count <= 3) { Finish(true, string.Empty); break; }
                        controller.RestoreControls(); selector.ShowAll(); Click(60, 82); break;
                    case 10:
                        SendEvent(new Event { type = EventType.ScrollWheel, mousePosition = new Vector2(100, 150), delta = new Vector2(0, 100) }); break;
                    case 11: Click(60, 180); break;
                    case 12:
                        Require(selector.SelectedIndex == selector.Count - 1, "Pointer scrolled last-item selection failed.");
                        Click(180, 82); break;
                    case 13:
                        Require(selector.SelectedIndex == -1, "Pointer overview after scrolling failed.");
                        Finish(true, string.Empty); break;
                }
            }
            catch (Exception exception) { Finish(false, exception.Message); }
        }

        private void OnGUI()
        {
            if (selector == null) { return; }
            if (requestedFocus != null) { GUI.FocusControl(requestedFocus); requestedFocus = null; }
            if (Event.current.type == EventType.KeyDown)
            {
                typeof(PackageBuilderPreviewController).GetMethod("HandleAnimationKeyboardEvent",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { Event.current });
            }
            selector.Draw(new Rect(12, 12, 232, 190));
            hasDrawn = true;
        }

        private void Key(KeyCode key) => SendEvent(new Event { type = EventType.KeyDown, keyCode = key });
        private void Click(float x, float y)
        {
            SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(x, y) });
            SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = new Vector2(x, y) });
        }
        private void Finish(bool passed, string message)
        {
            EditorApplication.update -= Tick;
            selector.ShowAll();
            controller.RestoreControls();
            Close();
            if (passed) { Debug.Log("PACKAGEBUILDER_SELECTOR_UI_PASS"); }
            else { Debug.LogError("PACKAGEBUILDER_SELECTOR_UI_FAIL:" + message); }
            EditorApplication.Exit(passed ? 0 : 1);
        }
        private static void Require(bool passed, string message) { if (!passed) { throw new InvalidOperationException(message); } }
    }
}
