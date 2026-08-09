using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Represents one stable, machine-readable skin or skeleton validation finding.</summary>
    internal sealed class UnitySkinSkeletonFinding
    {
        internal UnitySkinSkeletonFinding(string code, string objectPath)
        {
            Code = code;
            ObjectPath = objectPath;
        }

        internal string Code { get; private set; }

        internal string ObjectPath { get; private set; }
    }

    /// <summary>Summarizes the validated skinning characteristics of one imported model hierarchy.</summary>
    internal sealed class UnitySkinSkeletonReport
    {
        internal UnitySkinSkeletonReport(
            int rendererCount,
            int uniqueBoneCount,
            int maximumInfluences,
            int unweightedVertexCount,
            UnitySkinSkeletonFinding[] findings)
        {
            RendererCount = rendererCount;
            UniqueBoneCount = uniqueBoneCount;
            MaximumInfluences = maximumInfluences;
            UnweightedVertexCount = unweightedVertexCount;
            Findings = findings;
        }

        internal int RendererCount { get; private set; }

        internal int UniqueBoneCount { get; private set; }

        internal int MaximumInfluences { get; private set; }

        internal int UnweightedVertexCount { get; private set; }

        internal UnitySkinSkeletonFinding[] Findings { get; private set; }

        internal bool IsValid
        {
            get { return Findings.Length == 0; }
        }
    }

    /// <summary>Validates skin renderers, bone hierarchies, bind poses, and vertex influence data.</summary>
    internal static class UnitySkinSkeletonValidator
    {
        /// <summary>Inspects an imported or instantiated hierarchy without modifying any Unity object.</summary>
        internal static UnitySkinSkeletonReport Validate(GameObject modelRoot, int allowedMaximumInfluences)
        {
            var findings = new List<UnitySkinSkeletonFinding>();
            if (modelRoot == null || allowedMaximumInfluences < 1 || allowedMaximumInfluences > 255)
            {
                findings.Add(new UnitySkinSkeletonFinding("UNITY_SKIN_REQUEST_INVALID", string.Empty));
                return new UnitySkinSkeletonReport(0, 0, 0, 0, findings.ToArray());
            }

            SkinnedMeshRenderer[] renderers = modelRoot
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .OrderBy(renderer => RelativePath(modelRoot.transform, renderer.transform),
                    StringComparer.Ordinal)
                .ToArray();
            if (renderers.Length == 0)
            {
                findings.Add(new UnitySkinSkeletonFinding("UNITY_SKIN_RENDERER_MISSING", modelRoot.name));
                return new UnitySkinSkeletonReport(0, 0, 0, 0, findings.ToArray());
            }

            var uniqueBones = new HashSet<Transform>();
            int maximumInfluences = 0;
            int unweightedVertices = 0;
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                string rendererPath = RelativePath(modelRoot.transform, renderer.transform);
                ValidateRenderer(modelRoot.transform, renderer, rendererPath, allowedMaximumInfluences,
                    uniqueBones, findings, ref maximumInfluences, ref unweightedVertices);
            }

            return new UnitySkinSkeletonReport(renderers.Length, uniqueBones.Count,
                maximumInfluences, unweightedVertices, findings
                    .OrderBy(finding => finding.Code, StringComparer.Ordinal)
                    .ThenBy(finding => finding.ObjectPath, StringComparer.Ordinal)
                    .ToArray());
        }

        private static void ValidateRenderer(
            Transform modelRoot,
            SkinnedMeshRenderer renderer,
            string rendererPath,
            int allowedMaximumInfluences,
            ISet<Transform> uniqueBones,
            ICollection<UnitySkinSkeletonFinding> findings,
            ref int maximumInfluences,
            ref int unweightedVertices)
        {
            Mesh mesh = renderer.sharedMesh;
            if (mesh == null)
            {
                findings.Add(new UnitySkinSkeletonFinding("UNITY_SKIN_MESH_MISSING", rendererPath));
                return;
            }

            Transform[] bones = renderer.bones ?? Array.Empty<Transform>();
            if (renderer.rootBone == null)
            {
                findings.Add(new UnitySkinSkeletonFinding("UNITY_SKIN_ROOT_BONE_MISSING", rendererPath));
            }
            else if (!IsDescendantOrSelf(modelRoot, renderer.rootBone))
            {
                findings.Add(new UnitySkinSkeletonFinding("UNITY_SKIN_ROOT_BONE_OUTSIDE_MODEL", rendererPath));
            }

            if (bones.Length == 0 || bones.Any(bone => bone == null))
            {
                findings.Add(new UnitySkinSkeletonFinding("UNITY_SKIN_BONE_MISSING", rendererPath));
            }
            if (bones.Where(bone => bone != null).Distinct().Count() != bones.Count(bone => bone != null))
            {
                findings.Add(new UnitySkinSkeletonFinding("UNITY_SKIN_BONE_DUPLICATE", rendererPath));
            }
            foreach (Transform bone in bones.Where(bone => bone != null))
            {
                uniqueBones.Add(bone);
                if (!IsDescendantOrSelf(modelRoot, bone))
                {
                    findings.Add(new UnitySkinSkeletonFinding("UNITY_SKIN_BONE_OUTSIDE_MODEL", rendererPath));
                }
            }

            if (mesh.bindposes.Length != bones.Length)
            {
                findings.Add(new UnitySkinSkeletonFinding("UNITY_SKIN_BINDPOSE_COUNT_INVALID", rendererPath));
            }

            using (var counts = mesh.GetBonesPerVertex())
            using (var weights = mesh.GetAllBoneWeights())
            {
                if (counts.Length != mesh.vertexCount)
                {
                    findings.Add(new UnitySkinSkeletonFinding("UNITY_SKIN_VERTEX_WEIGHT_DATA_INVALID",
                        rendererPath));
                    return;
                }

                int weightIndex = 0;
                int rendererUnweightedVertices = 0;
                for (int vertexIndex = 0; vertexIndex < counts.Length; vertexIndex++)
                {
                    int influenceCount = counts[vertexIndex];
                    maximumInfluences = Math.Max(maximumInfluences, influenceCount);
                    float totalWeight = 0f;
                    for (int influenceIndex = 0; influenceIndex < influenceCount; influenceIndex++)
                    {
                        if (weightIndex >= weights.Length || weights[weightIndex].boneIndex >= bones.Length)
                        {
                            findings.Add(new UnitySkinSkeletonFinding(
                                "UNITY_SKIN_WEIGHT_BONE_INDEX_INVALID", rendererPath));
                            return;
                        }

                        totalWeight += weights[weightIndex].weight;
                        weightIndex++;
                    }

                    if (influenceCount == 0 || totalWeight <= 0.000001f)
                    {
                        unweightedVertices++;
                        rendererUnweightedVertices++;
                    }
                    if (influenceCount > allowedMaximumInfluences)
                    {
                        findings.Add(new UnitySkinSkeletonFinding(
                            "UNITY_SKIN_MAXIMUM_INFLUENCES_EXCEEDED", rendererPath));
                    }
                }

                if (weightIndex != weights.Length)
                {
                    findings.Add(new UnitySkinSkeletonFinding("UNITY_SKIN_VERTEX_WEIGHT_DATA_INVALID",
                        rendererPath));
                }
                if (rendererUnweightedVertices > 0)
                {
                    findings.Add(new UnitySkinSkeletonFinding(
                        "UNITY_SKIN_UNWEIGHTED_VERTICES", rendererPath));
                }
            }
        }

        private static bool IsDescendantOrSelf(Transform root, Transform candidate)
        {
            for (Transform current = candidate; current != null; current = current.parent)
            {
                if (current == root)
                {
                    return true;
                }
            }

            return false;
        }

        private static string RelativePath(Transform root, Transform value)
        {
            var segments = new Stack<string>();
            for (Transform current = value; current != null && current != root; current = current.parent)
            {
                segments.Push(current.name);
            }

            return segments.Count == 0 ? root.name : string.Join("/", segments.ToArray());
        }
    }
}
