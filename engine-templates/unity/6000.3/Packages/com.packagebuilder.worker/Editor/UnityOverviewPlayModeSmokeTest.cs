using System;
using System.Linq;
using PackageBuilder.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Runs the retained composed overview scene through one real Play mode cycle.</summary>
    public static class UnityOverviewPlayModeSmokeTest
    {
        private const string DefaultSceneReference =
            "Assets/PBModelTests/Scenes/S_StoneArch_Overview.unity";

        private static bool enteredPlayMode;
        private static bool loggedError;
        private static bool originalOptionsEnabled;
        private static EnterPlayModeOptions originalOptions;

        /// <summary>Starts an asynchronous Play mode validation and exits Unity with a stable status.</summary>
        public static void Run()
        {
            try
            {
                string sceneReference = Environment.GetEnvironmentVariable(
                    "PACKAGEBUILDER_UNITY_OVERVIEW_SCENE");
                if (string.IsNullOrEmpty(sceneReference))
                {
                    sceneReference = DefaultSceneReference;
                }

                EditorSceneManager.OpenScene(sceneReference, OpenSceneMode.Single);
                originalOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
                originalOptions = EditorSettings.enterPlayModeOptions;
                EditorSettings.enterPlayModeOptionsEnabled = true;
                EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
                Application.logMessageReceived += OnLog;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                Fail("startup:" + exception.GetType().Name);
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            try
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    enteredPlayMode = true;
                    PackageBuilderPreviewController controller = UnityEngine.Object
                        .FindObjectsByType<PackageBuilderPreviewController>(
                            FindObjectsInactive.Include,
                            FindObjectsSortMode.None)
                        .SingleOrDefault();
                    if (controller == null || controller.PreviewTarget == null ||
                        controller.PreviewCamera == null || controller.PreviewTarget.childCount != 1)
                    {
                        throw new InvalidOperationException("Overview controller references are invalid.");
                    }

                    Transform[] productTransforms = controller.PreviewTarget.GetChild(0)
                        .GetComponentsInChildren<Transform>(true);
                    Vector3[] positions = productTransforms.Select(value => value.localPosition).ToArray();
                    Quaternion[] rotations = productTransforms.Select(value => value.localRotation).ToArray();
                    Vector3[] scales = productTransforms.Select(value => value.localScale).ToArray();
                    Vector3 cameraBefore = controller.PreviewCamera.transform.position;
                    if (!controller.AutoFrame() || !controller.Orbit(20f, 8f) || !controller.Zoom(0.2f) ||
                        cameraBefore == controller.PreviewCamera.transform.position)
                    {
                        throw new InvalidOperationException("Overview camera navigation failed.");
                    }

                    for (int index = 0; index < productTransforms.Length; index++)
                    {
                        if (productTransforms[index].localPosition != positions[index] ||
                            productTransforms[index].localRotation != rotations[index] ||
                            productTransforms[index].localScale != scales[index])
                        {
                            throw new InvalidOperationException(
                                "Camera navigation changed a product transform.");
                        }
                    }

                    EditorApplication.ExitPlaymode();
                }
                else if (state == PlayModeStateChange.EnteredEditMode && enteredPlayMode)
                {
                    if (loggedError)
                    {
                        Fail("play-mode-log-error");
                        return;
                    }

                    Cleanup();
                    Debug.Log("PACKAGEBUILDER_UNITY_OVERVIEW_PLAYMODE_PASS");
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Fail("validation:" + exception.GetType().Name);
            }
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                loggedError = true;
            }
        }

        private static void Fail(string diagnostic)
        {
            Cleanup();
            Debug.LogError("PACKAGEBUILDER_UNITY_OVERVIEW_PLAYMODE_FAIL:" + diagnostic);
            EditorApplication.Exit(1);
        }

        private static void Cleanup()
        {
            Application.logMessageReceived -= OnLog;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorSettings.enterPlayModeOptionsEnabled = originalOptionsEnabled;
            EditorSettings.enterPlayModeOptions = originalOptions;
        }
    }
}
