using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Defines deterministic animation clips, default state, and controller output.</summary>
    internal sealed class UnityAnimatorControllerRequest
    {
        internal string AssetId { get; set; }

        internal string OutputControllerReference { get; set; }

        internal string DefaultClipReference { get; set; }

        internal string[] ClipReferences { get; set; } = Array.Empty<string>();
    }

    /// <summary>Creates AC_ controllers with one stable state and replay trigger per clip.</summary>
    internal static class UnityAnimatorControllerGenerator
    {
        /// <summary>Creates and verifies a deterministic controller without missing motions.</summary>
        internal static bool TryCreate(
            UnityAnimatorControllerRequest request,
            out AnimatorController controller,
            out string diagnosticCode)
        {
            controller = null;
            diagnosticCode = "UNITY_ANIMATOR_CONTROLLER_INVALID";
            AnimationClip[] clips;
            if (!TryValidate(request, out clips, out diagnosticCode))
            {
                return false;
            }

            try
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(
                    request.OutputControllerReference);
                if (controller == null || controller.layers.Length != 1)
                {
                    diagnosticCode = "UNITY_ANIMATOR_CONTROLLER_CREATE_FAILED";
                    return false;
                }

                AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
                AnimationClip defaultClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    request.DefaultClipReference);
                var states = new Dictionary<string, AnimatorState>(StringComparer.Ordinal);
                foreach (AnimationClip clip in clips.OrderBy(value => value.name, StringComparer.Ordinal))
                {
                    AnimatorState state = stateMachine.AddState(clip.name);
                    state.motion = clip;
                    states.Add(clip.name, state);
                    string parameter = ReplayParameter(clip.name, request.AssetId);
                    controller.AddParameter(parameter, AnimatorControllerParameterType.Trigger);
                    AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(state);
                    transition.hasExitTime = false;
                    transition.duration = 0f;
                    transition.canTransitionToSelf = true;
                    transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);
                }

                stateMachine.defaultState = states[defaultClip.name];
                AssetDatabase.SaveAssets();
                controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    request.OutputControllerReference);
                if (!Verify(request, controller, clips))
                {
                    controller = null;
                    diagnosticCode = "UNITY_ANIMATOR_CONTROLLER_VERIFY_FAILED";
                    return false;
                }

                diagnosticCode = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException ||
                exception is InvalidOperationException || exception is UnityException)
            {
                controller = null;
                diagnosticCode = "UNITY_ANIMATOR_CONTROLLER_CREATE_FAILED";
                return false;
            }
            finally
            {
                if (controller == null)
                {
                    AssetDatabase.DeleteAsset(request.OutputControllerReference);
                }
            }
        }

        private static bool TryValidate(
            UnityAnimatorControllerRequest request,
            out AnimationClip[] clips,
            out string diagnosticCode)
        {
            clips = Array.Empty<AnimationClip>();
            diagnosticCode = "UNITY_ANIMATOR_CONTROLLER_INVALID";
            if (request == null || !UnityAssetNameValidator.IsProductFolder(request.AssetId) ||
                !UnityPrefabHierarchyUtility.IsSafeReference(
                    request.OutputControllerReference, "/Controllers/AC_", ".controller") ||
                !request.OutputControllerReference.EndsWith(
                    "/AC_" + request.AssetId + ".controller", StringComparison.Ordinal) ||
                AssetDatabase.LoadMainAssetAtPath(request.OutputControllerReference) != null ||
                request.ClipReferences == null || request.ClipReferences.Length == 0 ||
                request.ClipReferences.Any(value => !IsClipReference(value, request.AssetId)) ||
                request.ClipReferences.Distinct(StringComparer.Ordinal).Count() !=
                    request.ClipReferences.Length ||
                !request.ClipReferences.Contains(request.DefaultClipReference, StringComparer.Ordinal))
            {
                return false;
            }

            clips = request.ClipReferences
                .OrderBy(value => value, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<AnimationClip>)
                .ToArray();
            if (clips.Any(clip => clip == null))
            {
                diagnosticCode = "UNITY_ANIMATOR_CONTROLLER_MOTION_MISSING";
                return false;
            }

            return clips.Select(clip => clip.name).Distinct(StringComparer.Ordinal).Count() ==
                clips.Length;
        }

        private static bool Verify(
            UnityAnimatorControllerRequest request,
            AnimatorController controller,
            AnimationClip[] clips)
        {
            if (controller == null || controller.name != "AC_" + request.AssetId ||
                controller.layers.Length != 1)
            {
                return false;
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            ChildAnimatorState[] states = stateMachine.states;
            if (states.Length != clips.Length || states.Any(value => value.state.motion == null) ||
                !states.Select(value => value.state.name).OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(clips.Select(value => value.name)
                        .OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal) ||
                stateMachine.defaultState == null ||
                AssetDatabase.GetAssetPath(stateMachine.defaultState.motion) !=
                    request.DefaultClipReference)
            {
                return false;
            }

            string[] expectedParameters = clips.Select(clip => ReplayParameter(
                    clip.name, request.AssetId))
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string[] actualParameters = controller.parameters.Select(value => value.name)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            return actualParameters.SequenceEqual(expectedParameters, StringComparer.Ordinal) &&
                controller.parameters.All(value =>
                    value.type == AnimatorControllerParameterType.Trigger) &&
                stateMachine.anyStateTransitions.Length == clips.Length &&
                stateMachine.anyStateTransitions.All(transition =>
                    transition.destinationState != null && transition.destinationState.motion != null &&
                    !transition.hasExitTime && transition.duration == 0f &&
                    transition.canTransitionToSelf && transition.conditions.Length == 1);
        }

        private static bool IsClipReference(string value, string assetId)
        {
            return UnityPrefabHierarchyUtility.IsSafeReference(value, "/Animations/A_", ".anim") &&
                value.Substring(value.LastIndexOf('/') + 1)
                    .StartsWith("A_" + assetId + "_", StringComparison.Ordinal);
        }

        private static string ReplayParameter(string clipName, string assetId)
        {
            return "Replay_" + clipName.Substring(("A_" + assetId + "_").Length);
        }
    }
}
