using System;
using System.Collections.Generic;
using System.Linq;
using PackageBuilder.MultiItem;
using UnityEditor;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Explicit attachment metadata emitted by the application; point paths are relative to declared target roots.</summary>
    [Serializable]
    internal sealed class UnityAttachmentRequest
    {
        public int schemaVersion = 0;
        public string setId = string.Empty;
        public AttachmentRequirement[] members = Array.Empty<AttachmentRequirement>();
        public AttachmentBinding[] bindings = Array.Empty<AttachmentBinding>();
    }

    /// <summary>Inspects exact target hierarchy paths and skin bones, then applies the shared attachment policy.</summary>
    internal static class UnitySetAttachmentValidator
    {
        /// <summary>Returns a validation report only after current target assets satisfy every declared slot binding.</summary>
        internal static bool Validate(UnitySetPlan set, string json, IDictionary<string, string> targetReferences, out string diagnostic)
        {
            diagnostic = "ATTACHMENT_METADATA_INVALID";
            UnityAttachmentRequest request;
            try { request = JsonUtility.FromJson<UnityAttachmentRequest>(json); }
            catch (ArgumentException) { return false; }
            if (set == null || set.members == null || request == null || request.schemaVersion != 1 || request.setId != set.setId || request.members == null ||
                request.bindings == null || targetReferences == null || request.members.Length != set.members.Length) { return false; }
            for (int index = 0; index < set.members.Length; index++)
            {
                if (set.members[index] == null || request.members[index] == null || request.members[index].itemId != set.members[index].itemId ||
                    request.members[index].slot != set.members[index].slot) { return false; }
            }
            var observed = new List<AttachmentPoint>();
            foreach (AttachmentBinding binding in request.bindings)
            {
                string reference;
                if (binding == null || string.IsNullOrEmpty(binding.targetId) || !targetReferences.TryGetValue(binding.targetId, out reference) ||
                    !UnityPrefabGenerator.IsSafeAssetReference(reference)) { diagnostic = "ATTACHMENT_TARGET_MISSING_OR_AMBIGUOUS"; return false; }
                GameObject target = AssetDatabase.LoadAssetAtPath<GameObject>(reference);
                if (target == null) { diagnostic = "ATTACHMENT_TARGET_MISSING_OR_AMBIGUOUS"; return false; }
                Transform[] matches = target.GetComponentsInChildren<Transform>(true).Where(transform =>
                    AnimationUtility.CalculateTransformPath(transform, target.transform) == binding.point).ToArray();
                foreach (Transform point in matches)
                {
                    if (binding.kind == "bone" && !target.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(renderer => renderer.bones.Contains(point))) { continue; }
                    if (!observed.Any(value => value.targetId == binding.targetId && value.kind == binding.kind && value.point == binding.point))
                    { observed.Add(new AttachmentPoint { targetId = binding.targetId, kind = binding.kind, point = binding.point }); }
                }
                if (matches.Length > 1) { diagnostic = "ATTACHMENT_TARGET_MISSING_OR_AMBIGUOUS"; return false; }
            }
            return AttachmentPolicy.Validate(request.members, request.bindings, observed.ToArray(), out diagnostic);
        }
    }
}
