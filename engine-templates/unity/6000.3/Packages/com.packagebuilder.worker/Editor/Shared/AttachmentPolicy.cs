using System;
using System.Collections.Generic;
using System.Linq;

// Unity JsonUtility serializes fields, not properties; these are explicit wire DTOs.
#pragma warning disable CA1051

namespace PackageBuilder.MultiItem
{
    /// <summary>A required logical slot from the reviewed set membership.</summary>
    [Serializable]
    public sealed class AttachmentRequirement
    {
        public string itemId = string.Empty;
        public string slot = string.Empty;
    }

    /// <summary>One explicitly declared target point, never a fuzzy name match or implicit retarget.</summary>
    [Serializable]
    public sealed class AttachmentBinding
    {
        public string itemId = string.Empty;
        public string slot = string.Empty;
        public string targetId = string.Empty;
        public string kind = string.Empty;
        public string point = string.Empty;
    }

    /// <summary>A point observed by an adapter in the current declared target snapshot.</summary>
    [Serializable]
    public sealed class AttachmentPoint
    {
        public string targetId = string.Empty;
        public string kind = string.Empty;
        public string point = string.Empty;
    }

    /// <summary>Canonical attachment applicability/identity checks shared by Application and target adapters.</summary>
    public static class AttachmentPolicy
    {
        /// <summary>Requires exactly one binding per slotted member and exactly one observed point per binding.</summary>
        public static bool Validate(AttachmentRequirement[] members, AttachmentBinding[] bindings, AttachmentPoint[] points, out string diagnostic)
        {
            diagnostic = "ATTACHMENT_METADATA_INVALID";
            if (members == null || bindings == null || points == null)
            { return false; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (AttachmentRequirement member in members)
            {
                if (member == null || string.IsNullOrEmpty(member.itemId) || member.slot == null || !ids.Add(member.itemId))
                { return false; }
                AttachmentBinding[] matches = bindings.Where(binding => binding != null && binding.itemId == member.itemId).ToArray();
                if (member.slot.Length == 0)
                { if (matches.Length != 0) { return false; } continue; }
                if (matches.Length != 1)
                { diagnostic = "ATTACHMENT_BINDING_REQUIRED"; return false; }
                AttachmentBinding value = matches[0];
                if (value.slot != member.slot || string.IsNullOrEmpty(value.targetId) || string.IsNullOrEmpty(value.point) ||
                    (value.kind != "bone" && value.kind != "socket" && value.kind != "body-slot"))
                { return false; }
                if (points.Count(point => point != null && point.targetId == value.targetId && point.kind == value.kind && point.point == value.point) != 1)
                { diagnostic = "ATTACHMENT_TARGET_MISSING_OR_AMBIGUOUS"; return false; }
            }
            if (bindings.Any(binding => binding == null || !ids.Contains(binding.itemId)))
            { return false; }
            diagnostic = string.Empty;
            return true;
        }
    }
}
