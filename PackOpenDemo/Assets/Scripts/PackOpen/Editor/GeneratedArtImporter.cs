using UnityEditor;
using UnityEngine;

namespace PackOpen.Editor
{
    /// <summary>
    /// Imports art as clean sprites.
    /// Art/Generated: always enforced.
    /// Art/Final: applied only on first import, so any settings you change later are kept.
    /// </summary>
    public class GeneratedArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            bool generated = assetPath.StartsWith("Assets/Art/Generated/");
            bool final = assetPath.StartsWith("Assets/Art/Final/");
            if (!generated && !final) return;

            var ti = (TextureImporter)assetImporter;
            if (final && !ti.importSettingsMissing) return;

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaIsTransparency = true;
            ti.maxTextureSize = 2048;

            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Center;
            ti.SetTextureSettings(s);
        }
    }
}
