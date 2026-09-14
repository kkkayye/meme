using UnityEditor;
using UnityEngine;

namespace RuneArena.Editor
{
    /// <summary>Import settings for Tripo FBX files under Assets/Art/Tripo: generic rig, no root motion, looping locomotion clips, standard materials.</summary>
    public sealed class ArtImportPostprocessor : AssetPostprocessor
    {
        private const string ArtFolder = "Assets/Art/Tripo/";
        private static readonly string[] LoopingClips = { "idle", "walk", "run" };

        private bool IsTripoAsset => assetPath.StartsWith(ArtFolder, System.StringComparison.Ordinal);

        private void OnPreprocessModel()
        {
            if (!IsTripoAsset) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.importCameras = false;
            importer.importLights = false;
            importer.bakeAxisConversion = true;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsTripoAsset) return;
            var importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            for (int i = 0; i < clips.Length; i++)
            {
                string name = clips[i].name.ToLowerInvariant();
                clips[i].loopTime = ShouldLoop(name);
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].keepOriginalOrientation = true;
                clips[i].keepOriginalPositionY = true;
                clips[i].keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }

        private static bool ShouldLoop(string name)
        {
            for (int i = 0; i < LoopingClips.Length; i++)
            {
                if (name.Contains(LoopingClips[i])) return true;
            }
            return false;
        }
    }
}
