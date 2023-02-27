using System;
using System.IO;
using System.Collections.Generic;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using HarmonyLib;

using UnityEngine;

namespace OC2Modding
{
    public static class ImportAYCEAssets
    {
        private const long AVATAR_DIRECTORY_ID = -1326050724655347751;

        private static AssetsManager Manager = new AssetsManager();
     
        private struct ChefMetadata
        {
            public long variantID;
            public AssetTypeValueField variantData;
        };

        private struct MonoBehaviourTypeData
        {
            public int index;
            public int fileID;
            public long pathID;
            public AssetTypeTemplateField template;
        };
     
        public static void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(ImportAYCEAssets));

            Manager.LoadClassPackage("classdata.tpk");
            Manager.MonoTempGenerator = new MonoCecilTempGenerator("C:/Other/Games/Steam/steamapps/common/Overcooked! 2/Overcooked2_Data/Managed");

            if (Manager.MonoTempGenerator == null)
            {
                OC2Modding.Log.LogError($"Failed to init Mono template generator at (Managed folder)");
                return;
            }

            var oc2AvatarBundlePath = "C:/Other/Games/Steam/steamapps/common/Overcooked! 2/Overcooked2_Data/StreamingAssets/Windows/bundle18";
            var oc2AvatarBundleBakPath = oc2AvatarBundlePath + ".bak";

            if (!File.Exists(oc2AvatarBundleBakPath))
            {
                File.Copy(oc2AvatarBundlePath, oc2AvatarBundleBakPath);
            }

            ReadScriptInfo(oc2AvatarBundleBakPath);
            List<ChefMetadata> chefMetadata = LoadAyceAvatars("C:/Other/Games/Steam/steamapps/common/Overcooked! All You Can Eat/Overcooked All You Can Eat_Data/StreamingAssets/aa/Windows/StandaloneWindows64/persistent_assets_all.bundle");
            AddAvatarsToBundle(oc2AvatarBundleBakPath, oc2AvatarBundlePath, chefMetadata);
            Manager.UnloadAll();
        }

        private static MonoBehaviourTypeData ChefAvatarDataMB;

        private static void ReadScriptInfo(string bundlePath)
        {
            try
            {
                var bundle = Manager.LoadBundleFile(bundlePath);
                if (bundle == null)
                {
                    throw new Exception($"Failed to load bundle file: {bundlePath}");
                }
                
                var assets = Manager.LoadAssetsFileFromBundle(bundle, 0, loadDeps: false);
                if (assets == null || assets.file == null)
                {
                    throw new Exception($"Failed to load assets file from {bundlePath}");
                }

                ChefAvatarDataMB = ReadMonoBehaviorTypeData(assets, "ChefAvatarData");
            }
            catch (Exception e)
            {
                OC2Modding.Log.LogError($"Failed to read script type data for {bundlePath}: {e}");
                Manager.UnloadAll();
                return;
            }
        }

        private static MonoBehaviourTypeData ReadMonoBehaviorTypeData(AssetsFileInstance assets, string className)
        {
            var scriptTypeInfos = AssetHelper.GetAssetsFileScriptInfos(Manager, assets);
            if (scriptTypeInfos == null)
            {
                throw new Exception($"Failed to read scriptTypeInfos");
            }

            var scriptIndex = scriptTypeInfos.FindIndex(s => s.className == className);
            if (scriptTypeInfos == null)
            {
                throw new Exception($"Failed to find class of type '{className}'");
            }

            var scriptTypeInfo = scriptTypeInfos[scriptIndex];
            if (scriptTypeInfos == null)
            {
                throw new Exception($"Failed to get script info for class '{className}'");
            }

            var assemblyName = scriptTypeInfo.assemblyName;
            var nameSpace = scriptTypeInfo.nameSpace;

            if (className != scriptTypeInfo.className) {
                throw new Exception($"Expected class name '{scriptTypeInfo.className}' got '{className}'");
            }

            var unityVersion = new UnityVersion(assets.file.Metadata.UnityVersion);
            var mbTempField = Manager.GetTemplateBaseField(assets, assets.file.Reader, -1, (int)AssetClassID.MonoBehaviour, 0xffff, false, true);
            if (mbTempField == null)
            {
                throw new Exception($"Failed to get template base field for assets file");
            }

            var template = Manager.MonoTempGenerator.GetTemplateField(mbTempField, assemblyName, nameSpace, className, unityVersion);
            if (template == null)
            {
                throw new Exception($"Failed to make template for {className}");
            }

            return new MonoBehaviourTypeData { 
                index = scriptIndex,
                fileID = assets.file.Metadata.ScriptTypes[scriptIndex].FileId,
                pathID = assets.file.Metadata.ScriptTypes[scriptIndex].PathId,
                template = template,
            }; 
        }

        private static List<ChefMetadata> LoadAyceAvatars(string bundlePath)
        {
            var chefMetadata = new List<ChefMetadata>();
            try
            {
                var bundle = Manager.LoadBundleFile(bundlePath);
                if (bundle == null)
                {
                    throw new Exception($"Failed to load bundle file: {bundlePath}");
                }

                var assets = Manager.LoadAssetsFileFromBundle(bundle, 0, loadDeps: false);
                if (assets == null || assets.file == null)
                {
                    throw new Exception($"Failed to load assets file from {bundlePath}");
                }

                var mainAvatarDirectoryInfo = assets.file.GetAssetInfo(AVATAR_DIRECTORY_ID);
                var mainAvatarDirectory = Manager.GetBaseField(assets, mainAvatarDirectoryInfo);
                if (mainAvatarDirectory["m_Name"].AsString != "MainAvatarDirectory")
                {
                    throw new Exception($"Unexpected name for AYCE MainAvatarDirectory: {mainAvatarDirectory["m_Name"].AsString}");
                }

                var baseAvatars = mainAvatarDirectory["m_baseAvatars"][0];
                OC2Modding.Log.LogInfo($"{baseAvatars.AsArray.size} baseAvatars to parse");

                foreach (var baseAvatar in baseAvatars)
                {
                    string baseName = baseAvatar["m_name"].AsString;
                    var variants = baseAvatar["m_variants"][0];
                    // OC2Modding.Log.LogInfo($"{baseName} has {variants.AsArray.size} variants");

                    foreach (var variant in variants)
                    {
                        try
                        {
                            var pathID = variant["m_PathID"].AsLong;
                            var variantInfo = assets.file.GetAssetInfo(pathID);
                            var variantData = Manager.GetBaseField(assets, variantInfo);

                            // OC2Modding.Log.LogInfo($"  {pathID}");
                            // OC2Modding.Log.LogInfo($"{variantData["m_Name"].AsString}");

                            chefMetadata.Add(
                                new ChefMetadata {
                                    variantID = pathID,
                                    variantData = variantData,
                                }
                            );
                        }
                        catch (Exception e)
                        {
                            OC2Modding.Log.LogError($"Failed to load ayce chef variant: {e}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                OC2Modding.Log.LogError($"Failed to load AYCE chefs from bundle {e}");
            }

            return chefMetadata;
        }

        private static void AddAvatarsToBundle(string inBundlePath, string outBundlePath, List<ChefMetadata> chefMetadata)
        {
            try
            {
                var bundle = Manager.LoadBundleFile(inBundlePath, true);
                if (bundle == null)
                {
                    throw new Exception($"Failed to load bundle file");
                }

                var assets = Manager.LoadAssetsFileFromBundle(bundle, 0, loadDeps: false);
                if (assets == null || assets.file == null)
                {
                    throw new Exception($"Failed to load assets file");
                }

                var mainAvatarDirectoryInfo = assets.file.GetAssetInfo(AVATAR_DIRECTORY_ID);
                if (mainAvatarDirectoryInfo == null)
                {
                    throw new Exception($"Failed to find file id={AVATAR_DIRECTORY_ID}");
                }

                var mainAvatarDirectory = Manager.GetBaseField(assets, mainAvatarDirectoryInfo);
                if (mainAvatarDirectory["m_Name"].AsString != "MainAvatarDirectory")
                {
                    throw new Exception($"Unexpected name for id={AVATAR_DIRECTORY_ID} '{mainAvatarDirectory["m_Name"].AsString}'");
                }

                var bundleReplacers = new List<BundleReplacer>();
                var assetsReplacers = new List<AssetsReplacer>();

                var avatars = mainAvatarDirectory["Avatars.Array"];
                if (avatars == null)
                {
                    throw new Exception($"Failed to find avatars list in MainAvatarDirectory");
                }

                // avatars.Children.Clear();

                // TODO: Abort if the directory already has AYCE avatars in it (size > 100)

                foreach (var chef in chefMetadata)
                {
                    // Add chef entry to AvatarDirectoryData array
                    // TODO: skip doing so if that chef already exists in OC2
                    var avatar = ValueBuilder.DefaultValueFieldFromArrayTemplate(avatars);
                    avatar["m_FileID"].AsInt = 0;
                    avatar["m_PathID"].AsLong = chef.variantID;
                    avatars.Children.Add(avatar);

                    // Make a new asset of type "ChefAvatarData" and convert fields from AYCE
                    var chefBytes = ConvertVariant(chef.variantData).WriteToByteArray();
                    var replacer = new AssetsReplacerFromMemory(chef.variantID, (int)AssetClassID.MonoBehaviour, (ushort)ChefAvatarDataMB.index, chefBytes);
                    assetsReplacers.Add(replacer);

                    OC2Modding.Log.LogInfo($"Created {chef.variantData["m_Name"].AsString} ({chef.variantID})");
                }

                assetsReplacers.Add(new AssetsReplacerFromMemory(assets.file, mainAvatarDirectoryInfo, mainAvatarDirectory));
                bundleReplacers.Add(new BundleReplacerFromAssets(assets.name, null, assets.file, assetsReplacers));

                using (AssetsFileWriter writer = new AssetsFileWriter(outBundlePath))
                {
                    bundle.file.Write(writer, bundleReplacers);
                }
            }
            catch (Exception e)
            {
                OC2Modding.Log.LogError($"Failed make new bundle '{outBundlePath}' from base '{inBundlePath}': {e}");
            }
        }

        private static AssetTypeValueField ConvertVariant(AssetTypeValueField ayceVariantData)
        {
            var newBaseField = ValueBuilder.DefaultValueFieldFromTemplate(ChefAvatarDataMB.template);

            newBaseField["m_GameObject.m_FileID"].AsInt = 0;
            newBaseField["m_GameObject.m_PathID"].AsLong = 0;

            newBaseField["m_Enabled"].AsInt = 1;

            newBaseField["m_Script.m_FileID"].AsInt = ChefAvatarDataMB.fileID;
            newBaseField["m_Script.m_PathID"].AsLong = ChefAvatarDataMB.pathID; // -8987147349260460087

            newBaseField["m_Name"].AsString = ayceVariantData["m_Name"].AsString;

            newBaseField["ModelPrefab.m_FileID"].AsInt = ayceVariantData["ModelPrefab.m_FileID"].AsInt;
            newBaseField["ModelPrefab.m_PathID"].AsLong = ayceVariantData["ModelPrefab.m_PathID"].AsLong;

            newBaseField["FrontendModelPrefab.m_FileID"].AsInt = ayceVariantData["ModelPrefab_Frontend.m_FileID"].AsInt;
            newBaseField["FrontendModelPrefab.m_PathID"].AsLong = ayceVariantData["ModelPrefab_Frontend.m_PathID"].AsLong;

            newBaseField["UIModelPrefab.m_FileID"].AsInt = ayceVariantData["ModelPrefab_UI.m_FileID"].AsInt;
            newBaseField["UIModelPrefab.m_PathID"].AsLong = ayceVariantData["ModelPrefab_UI.m_PathID"].AsLong;

            newBaseField["HeadName"].AsString = ayceVariantData["Head"].AsString;

            newBaseField["ColourisationMode"].AsInt = (int)ChefMeshReplacer.ChefColourisationMode.SwapColourValue;
            newBaseField["ActuallyAllowed"].AsBool = true;

            // ForDlc left at 0;0

            newBaseField["m_PC"].AsBool = true;
            newBaseField["m_XboxOne"].AsBool = true;
            newBaseField["m_PS4"].AsBool = true;
            newBaseField["m_Switch"].AsBool = true;

            return newBaseField;
        }

        // [HarmonyPatch(typeof(MetaGameProgress), "Awake")]
        // [HarmonyPostfix]
        // private static void Awake(ref AvatarDirectoryData ___m_combinedAvatarDirectory)
        // {
        //     var dir = ___m_combinedAvatarDirectory;
        //     if (___m_combinedAvatarDirectory == null) return;

        //     OC2Modding.Log.LogInfo($"Before: {dir.Avatars.Length}|{dir.Colours.Length}");
        //     ApplyExtraAvatars(ref dir);
        //     OC2Modding.Log.LogInfo($"After: {dir.Avatars.Length}|{dir.Colours.Length}");
        // }

        [HarmonyPatch(typeof(MetaGameProgress), nameof(MetaGameProgress.GetUnlockedAvatars))]
        [HarmonyPostfix]
        private static void GetUnlockedAvatars(ref AvatarDirectoryData ___m_combinedAvatarDirectory, ref ChefAvatarData[] __result)
        {
            if (___m_combinedAvatarDirectory != null)
            {
                __result =___m_combinedAvatarDirectory.Avatars;
            }
        }
    }
}
