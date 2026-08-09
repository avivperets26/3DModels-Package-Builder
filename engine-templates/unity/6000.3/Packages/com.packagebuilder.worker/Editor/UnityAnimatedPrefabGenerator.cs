using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Defines the case-3 animated prefab output and runtime controller.</summary>
    internal sealed class UnityAnimatedPrefabRequest
    {
        internal string AssetId { get; set; }

        internal string SourceModelReference { get; set; }

        internal string AnimatorControllerReference { get; set; }

        internal string OutputPrefabReference { get; set; }

        internal int AllowedMaximumInfluences { get; set; } = 4;

        internal bool ApplyRootMotion { get; set; }
    }

    /// <summary>Creates case-3 prefabs while preserving every validated skinned renderer.</summary>
    internal static class UnityAnimatedPrefabGenerator
    {
        /// <summary>Creates and verifies a reset animated prefab with one assigned Animator.</summary>
        internal static bool TryCreate(
            UnityAnimatedPrefabRequest request,
            out GameObject prefabAsset,
            out UnitySkinSkeletonReport report,
            out string diagnosticCode)
        {
            prefabAsset = null;
            report = null;
            diagnosticCode = "UNITY_ANIMATED_PREFAB_INVALID";
            AnimatorController controller;
            if (!TryValidate(request, out controller))
            {
                return false;
            }

            GameObject root = null;
            GameObject model = null;
            try
            {
                if (!UnityPrefabHierarchyUtility.TryCreate(
                    request.AssetId, request.SourceModelReference, out root, out model))
                {
                    diagnosticCode = "UNITY_ANIMATED_PREFAB_INSTANTIATION_FAILED";
                    return false;
                }

                report = UnitySkinSkeletonValidator.Validate(
                    model, request.AllowedMaximumInfluences);
                if (!report.IsValid)
                {
                    diagnosticCode = report.Findings[0].Code;
                    return false;
                }

                SkinnedMeshRenderer[] renderers = model
                    .GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var expectedMeshes = new HashSet<Mesh>(
                    renderers.Select(renderer => renderer.sharedMesh));
                foreach (Animation animation in model.GetComponentsInChildren<Animation>(true))
                {
                    UnityEngine.Object.DestroyImmediate(animation);
                }

                Animator[] animators = model.GetComponentsInChildren<Animator>(true);
                if (animators.Length > 1)
                {
                    diagnosticCode = "UNITY_ANIMATED_PREFAB_MULTIPLE_ANIMATORS";
                    return false;
                }

                Animator animator = animators.Length == 1 ? animators[0] : model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = request.ApplyRootMotion;

                bool saved;
                PrefabUtility.SaveAsPrefabAsset(root, request.OutputPrefabReference, out saved);
                if (!saved)
                {
                    diagnosticCode = "UNITY_ANIMATED_PREFAB_CREATE_FAILED";
                    return false;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(request.OutputPrefabReference,
                    ImportAssetOptions.ForceSynchronousImport);
                prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
                    request.OutputPrefabReference);
                if (!Verify(request, prefabAsset, controller, expectedMeshes, out report))
                {
                    prefabAsset = null;
                    diagnosticCode = "UNITY_ANIMATED_PREFAB_VERIFY_FAILED";
                    return false;
                }

                diagnosticCode = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException ||
                exception is InvalidOperationException || exception is UnityException)
            {
                prefabAsset = null;
                diagnosticCode = "UNITY_ANIMATED_PREFAB_CREATE_FAILED";
                return false;
            }
            finally
            {
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
                if (prefabAsset == null)
                {
                    AssetDatabase.DeleteAsset(request.OutputPrefabReference);
                }
            }
        }

        private static bool TryValidate(
            UnityAnimatedPrefabRequest request,
            out AnimatorController controller)
        {
            controller = null;
            if (request == null || !UnityAssetNameValidator.IsProductFolder(request.AssetId) ||
                !UnityPrefabHierarchyUtility.IsSafeReference(
                    request.SourceModelReference, "/Source/", ".fbx") ||
                !UnityPrefabHierarchyUtility.IsSafeReference(
                    request.AnimatorControllerReference, "/Controllers/AC_", ".controller") ||
                !UnityPrefabHierarchyUtility.IsSafeReference(
                    request.OutputPrefabReference, "/Prefabs/P_", ".prefab") ||
                !request.OutputPrefabReference.EndsWith(
                    "/P_" + request.AssetId + ".prefab", StringComparison.Ordinal) ||
                request.AllowedMaximumInfluences <= 0 ||
                request.AllowedMaximumInfluences > 255 ||
                AssetDatabase.LoadMainAssetAtPath(request.OutputPrefabReference) != null)
            {
                return false;
            }

            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                request.AnimatorControllerReference);
            return controller != null;
        }

        private static bool Verify(
            UnityAnimatedPrefabRequest request,
            GameObject prefab,
            RuntimeAnimatorController controller,
            HashSet<Mesh> expectedMeshes,
            out UnitySkinSkeletonReport report)
        {
            report = null;
            if (prefab == null || prefab.name != "P_" + request.AssetId ||
                prefab.transform.childCount != 1 ||
                !UnityPrefabHierarchyUtility.IsReset(prefab.transform))
            {
                return false;
            }

            Transform model = prefab.transform.GetChild(0);
            Animator[] animators = model.GetComponentsInChildren<Animator>(true);
            SkinnedMeshRenderer[] renderers = model
                .GetComponentsInChildren<SkinnedMeshRenderer>(true);
            report = UnitySkinSkeletonValidator.Validate(
                model.gameObject, request.AllowedMaximumInfluences);
            return model.name == "P_Model" && UnityPrefabHierarchyUtility.IsReset(model) &&
                report.IsValid && animators.Length == 1 &&
                animators[0].runtimeAnimatorController == controller &&
                animators[0].applyRootMotion == request.ApplyRootMotion &&
                renderers.Length == expectedMeshes.Count &&
                expectedMeshes.SetEquals(renderers.Select(renderer => renderer.sharedMesh)) &&
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) == 0 &&
                model.GetComponentsInChildren<Animation>(true).Length == 0;
        }
    }
}
