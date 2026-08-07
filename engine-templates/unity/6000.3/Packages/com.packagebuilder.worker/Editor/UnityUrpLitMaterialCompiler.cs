using System;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Contains the renderer-independent values and resolved Unity texture references for one URP material.</summary>
    internal sealed class UnityUrpLitMaterialRequest
    {
        internal string OutputAssetReference { get; set; }

        internal string BaseMapAssetReference { get; set; }

        internal string NormalMapAssetReference { get; set; }

        internal string MetallicSmoothnessAssetReference { get; set; }

        internal string EmissionMapAssetReference { get; set; }

        internal string AmbientOcclusionMapAssetReference { get; set; }

        internal Color BaseColour { get; set; } = Color.white;

        internal Color EmissionColour { get; set; } = Color.black;

        internal float MetallicFactor { get; set; }

        internal float RoughnessFactor { get; set; } = 0.5f;

        internal float NormalScale { get; set; } = 1f;

        internal float AmbientOcclusionStrength { get; set; } = 1f;

        internal float Opacity { get; set; } = 1f;

        internal string SurfaceMode { get; set; } = "opaque";

        internal float? AlphaCutoff { get; set; }

        internal bool DoubleSided { get; set; }
    }

    /// <summary>Compiles canonical material intent into a fully synchronized URP/Lit material asset.</summary>
    internal static class UnityUrpLitMaterialCompiler
    {
        /// <summary>Creates a new material, applies all properties, then delegates keyword and queue setup to URP.</summary>
        internal static bool TryCompile(
            UnityUrpLitMaterialRequest request,
            out Material material,
            out string diagnosticCode)
        {
            material = null;
            diagnosticCode = "UNITY_URP_MATERIAL_INVALID";
            if (!TryValidate(request, out diagnosticCode))
            {
                return false;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                diagnosticCode = "UNITY_URP_LIT_SHADER_MISSING";
                return false;
            }

            var candidate = new Material(shader);
            try
            {
                Texture2D baseMap;
                Texture2D normalMap;
                Texture2D metallicSmoothness;
                Texture2D emissionMap;
                Texture2D ambientOcclusion;
                if (!TryLoadTexture(request.BaseMapAssetReference, false, out baseMap) ||
                    !TryLoadTexture(request.NormalMapAssetReference, true, out normalMap) ||
                    !TryLoadTexture(request.MetallicSmoothnessAssetReference, false, out metallicSmoothness) ||
                    !TryLoadTexture(request.EmissionMapAssetReference, false, out emissionMap) ||
                    !TryLoadTexture(request.AmbientOcclusionMapAssetReference, false, out ambientOcclusion))
                {
                    diagnosticCode = "UNITY_URP_MATERIAL_TEXTURE_INVALID";
                    return false;
                }

                candidate.SetTexture("_BaseMap", baseMap);
                candidate.SetColor("_BaseColor", WithAlpha(request.BaseColour, request.Opacity));
                candidate.SetTexture("_BumpMap", normalMap);
                candidate.SetFloat("_BumpScale", request.NormalScale);
                candidate.SetTexture("_MetallicGlossMap", metallicSmoothness);
                candidate.SetFloat("_WorkflowMode", 1f);
                candidate.SetFloat("_Metallic", request.MetallicFactor);
                candidate.SetFloat("_Smoothness", 1f - request.RoughnessFactor);
                candidate.SetFloat("_SmoothnessTextureChannel", 0f);
                candidate.SetTexture("_EmissionMap", emissionMap);
                candidate.SetColor("_EmissionColor", request.EmissionColour);
                bool hasEmission = emissionMap != null ||
                    request.EmissionColour.maxColorComponent > 0f;
                candidate.globalIlluminationFlags = hasEmission
                    ? MaterialGlobalIlluminationFlags.BakedEmissive
                    : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                candidate.SetTexture("_OcclusionMap", ambientOcclusion);
                candidate.SetFloat("_OcclusionStrength", request.AmbientOcclusionStrength);
                candidate.SetFloat("_Cull", request.DoubleSided ? 0f : 2f);
                candidate.SetFloat("_QueueOffset", 0f);
                if (candidate.HasProperty("_QueueControl"))
                {
                    candidate.SetFloat("_QueueControl", 0f);
                }

                bool transparent = string.Equals(request.SurfaceMode, "transparent", StringComparison.Ordinal);
                bool cutout = string.Equals(request.SurfaceMode, "cutout", StringComparison.Ordinal);
                candidate.SetFloat("_Surface", transparent ? 1f : 0f);
                candidate.SetFloat("_Blend", 0f);
                candidate.SetFloat("_AlphaClip", cutout ? 1f : 0f);
                candidate.SetFloat("_Cutoff", cutout ? request.AlphaCutoff.Value : 0.5f);

                // These public URP functions are the same canonicalizers used by the Lit Inspector.
                // Applying them before persistence prevents stale blend, keyword, GI, and queue state.
                BaseShaderGUI.SetMaterialKeywords(candidate, LitGUI.SetMaterialKeywords);
                BaseShaderGUI.SetupMaterialBlendMode(candidate);

                AssetDatabase.CreateAsset(candidate, request.OutputAssetReference);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    request.OutputAssetReference,
                    ImportAssetOptions.ForceSynchronousImport);
                material = AssetDatabase.LoadAssetAtPath<Material>(request.OutputAssetReference);
                if (material == null)
                {
                    diagnosticCode = "UNITY_URP_MATERIAL_CREATE_FAILED";
                    return false;
                }

                diagnosticCode = string.Empty;
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is UnityException)
            {
                diagnosticCode = "UNITY_URP_MATERIAL_CREATE_FAILED";
                return false;
            }
            finally
            {
                if (material == null)
                {
                    UnityEngine.Object.DestroyImmediate(candidate);
                    if (AssetDatabase.LoadMainAssetAtPath(request.OutputAssetReference) != null)
                    {
                        AssetDatabase.DeleteAsset(request.OutputAssetReference);
                    }
                }
            }
        }

        /// <summary>Reapplies URP's canonicalizers so tests can prove the compiled asset is already stable.</summary>
        internal static void SynchronizeUrpState(Material material)
        {
            BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
            BaseShaderGUI.SetupMaterialBlendMode(material);
        }

        private static bool TryValidate(UnityUrpLitMaterialRequest request, out string diagnosticCode)
        {
            diagnosticCode = "UNITY_URP_MATERIAL_INVALID";
            if (request == null || !IsSafeAssetReference(request.OutputAssetReference) ||
                !request.OutputAssetReference.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) ||
                AssetDatabase.LoadMainAssetAtPath(request.OutputAssetReference) != null)
            {
                return false;
            }

            string folder = System.IO.Path.GetDirectoryName(
                request.OutputAssetReference.Replace('/', System.IO.Path.DirectorySeparatorChar))
                ?.Replace(System.IO.Path.DirectorySeparatorChar, '/');
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                diagnosticCode = "UNITY_URP_MATERIAL_OUTPUT_FOLDER_MISSING";
                return false;
            }

            if (!IsFiniteUnit(request.MetallicFactor) || !IsFiniteUnit(request.RoughnessFactor) ||
                !IsFiniteNonNegative(request.NormalScale) ||
                !IsFiniteUnit(request.AmbientOcclusionStrength) || !IsFiniteUnit(request.Opacity) ||
                !IsFiniteColour(request.BaseColour) || !IsFiniteColour(request.EmissionColour))
            {
                return false;
            }

            bool opaque = string.Equals(request.SurfaceMode, "opaque", StringComparison.Ordinal);
            bool cutout = string.Equals(request.SurfaceMode, "cutout", StringComparison.Ordinal);
            bool transparent = string.Equals(request.SurfaceMode, "transparent", StringComparison.Ordinal);
            if ((!opaque && !cutout && !transparent) ||
                (opaque && request.Opacity != 1f) ||
                (cutout && (!request.AlphaCutoff.HasValue || !IsFiniteUnit(request.AlphaCutoff.Value))) ||
                (!cutout && request.AlphaCutoff.HasValue))
            {
                diagnosticCode = "UNITY_URP_MATERIAL_SURFACE_INVALID";
                return false;
            }

            return true;
        }

        private static bool TryLoadTexture(string assetReference, bool requireNormalMap, out Texture2D texture)
        {
            texture = null;
            if (string.IsNullOrEmpty(assetReference))
            {
                return true;
            }

            if (!IsSafeAssetReference(assetReference))
            {
                return false;
            }

            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetReference);
            var importer = AssetImporter.GetAtPath(assetReference) as TextureImporter;
            return texture != null && importer != null &&
                (!requireNormalMap || importer.textureType == TextureImporterType.NormalMap);
        }

        private static Color WithAlpha(Color colour, float alpha)
        {
            return new Color(colour.r, colour.g, colour.b, alpha);
        }

        private static bool IsFiniteColour(Color value)
        {
            return IsFinite(value.r) && IsFinite(value.g) && IsFinite(value.b) && IsFinite(value.a);
        }

        private static bool IsFiniteUnit(float value)
        {
            return IsFinite(value) && value >= 0f && value <= 1f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return IsFinite(value) && value >= 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsSafeAssetReference(string value)
        {
            if (string.IsNullOrEmpty(value) || !value.StartsWith("Assets/", StringComparison.Ordinal) ||
                value.IndexOf('\\') >= 0 || value.IndexOf(':') >= 0 ||
                value.IndexOf("//", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            foreach (string segment in value.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    return false;
                }
            }

            return true;
        }
    }
}
