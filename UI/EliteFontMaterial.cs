using TMPro;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

internal static class EliteFontMaterial
{
    internal static bool Ensure(TMP_FontAsset? font)
    {
        if (font == null || font.atlasTexture == null) return false;
        Texture2D atlas = font.atlasTexture;
        Material? existing = font.material;
        if (existing != null && existing.shader != null && existing.mainTexture == atlas) return true;

        // Unity 6 TMP_Asset uses m_Material; the older bundled font serialized
        // material instead. Its atlas and glyph tables are still valid.
        Shader? shader = TMP_Settings.defaultFontAsset?.material?.shader ??
            Shader.Find("TextMeshPro/Distance Field");
        if (shader == null) return false;
        Material material = new(shader)
        {
            name = font.name + " EliteEnemyVariants SDF",
            hideFlags = HideFlags.HideAndDontSave,
            mainTexture = atlas
        };
        material.SetFloat("_TextureWidth", atlas.width);
        material.SetFloat("_TextureHeight", atlas.height);
        material.SetFloat("_GradientScale", font.atlasPadding + 1);
        material.SetFloat("_WeightNormal", font.normalStyle);
        material.SetFloat("_WeightBold", font.boldStyle);
        material.SetFloat("_ScaleX", 1f);
        material.SetFloat("_ScaleY", 1f);
        font.material = material;
        font.ReadFontAssetDefinition();
        return true;
    }
}
