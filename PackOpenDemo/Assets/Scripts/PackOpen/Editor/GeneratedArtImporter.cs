using UnityEditor;
using UnityEngine;

namespace PackOpen.Editor
{
    /// <summary>Imports everything in Art/Generated as a clean sprite. Final art is yours to configure.</summary>
    public class GeneratedArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Art/Generated/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaIsTransparency = true;
            ti.maxTextureSize = 1024;
        }
    }
}
