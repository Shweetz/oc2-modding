using HarmonyLib;
using UnityEngine;
using Team17.Online.Multiplayer.Messaging;

namespace OC2Modding
{
    public static class Nerfs
    {
        static int removedPlates = 0;
        static bool finishedFirstPass = false;

        public static void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(Nerfs));
        }

        [HarmonyPatch(typeof(LoadingScreenFlow), nameof(LoadingScreenFlow.LoadScene))]
        [HarmonyPrefix]
        private static void LoadScene()
        {
            removedPlates = 0;
            finishedFirstPass = false;
        }

        [HarmonyPatch(typeof(ServerAttachStation), "OnItemPlaced")]
        [HarmonyPrefix]
        private static bool OnItemPlaced(ref GameObject _objectToPlace)
        {
            OC2Modding.Log.LogInfo($"Placed '{_objectToPlace.name}'");

            if (finishedFirstPass)
            {
                return true; // this is just regular gameplay
            }

            if (OC2Config.DisableCoal && _objectToPlace.name == "utensil_coalbucket_01")
            {
                _objectToPlace.Destroy();
                return false;
            }

            bool isPlate = _objectToPlace.name.StartsWith("equipment_plate_01") || _objectToPlace.name.StartsWith("Plate ");

            if (isPlate)
            {
                if (OC2Config.PlatesStartDirty)
                {
                    removedPlates++;
                    _objectToPlace.Destroy();
                    return false;
                }
                else if (OC2Config.DisableOnePlate && removedPlates == 0)
                {
                    removedPlates++;
                    _objectToPlace.Destroy();
                    return false;
                }
            }

            if (OC2Config.DisableFireExtinguisher && _objectToPlace.name == "utensil_fire_extinguisher_01")
            {
                _objectToPlace.Destroy();
                return false;
            }

            if (OC2Config.DisableBellows && _objectToPlace.name == "utensil_bellows_01")
            {
                _objectToPlace.Destroy();
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(ServerPlateReturnStation), nameof(ServerPlateReturnStation.UpdateSynchronising))]
        [HarmonyPrefix]
        private static void UpdateSynchronising(ref PlateReturnStation ___m_returnStation)
        {
            if (___m_returnStation.m_startingPlateNumber != 0 || removedPlates == 0 || finishedFirstPass)
            {
                return; // We already did this patch
            }

            if (___m_returnStation.name.Contains("rying"))
            {
                return; // This is the drying station for a sink
            }

            ___m_returnStation.m_startingPlateNumber = removedPlates;

            if (OC2Config.DisableOnePlate)
            {
                ___m_returnStation.m_startingPlateNumber -= 1;
            }

            finishedFirstPass = true;

            OC2Modding.Log.LogInfo($"Added {___m_returnStation.m_startingPlateNumber} plates to the serving window");
        }

        [HarmonyPatch(typeof(ServerKitchenFlowControllerBase), "OnSuccessfulDelivery")]
        [HarmonyPostfix]
        private static void OnSuccessfulDelivery(ref ServerKitchenFlowControllerBase __instance)
        {
            var monitor = __instance.GetMonitorForTeam(0);
            if (monitor.Score.TotalMultiplier > OC2Config.MaxTipCombo)
            {
                monitor.Score.TotalMultiplier = OC2Config.MaxTipCombo;
            }
        }

        /* Edit the message sent to clients to also show the new limit */
        [HarmonyPatch(typeof(KitchenFlowMessage), nameof(KitchenFlowMessage.SetScoreData))]
        [HarmonyPostfix]
        private static void SetScoreData(ref TeamMonitor.TeamScoreStats ___m_teamScore)
        {
            if (___m_teamScore.TotalMultiplier > OC2Config.MaxTipCombo)
            {
                ___m_teamScore.TotalMultiplier = OC2Config.MaxTipCombo;
            }
        }

        // TODO: You can stop client players from doing these things by ignoring at OnChefEvent() as well
        [HarmonyPatch(typeof(ClientPlayerControlsImpl_Default), nameof(ClientPlayerControlsImpl_Default.ApplyServerEvent))]
        [HarmonyPrefix]
        private static bool ApplyServerEvent(ref Serialisable serialisable)
        {
            InputEventMessage inputEventMessage = (InputEventMessage)serialisable;
            InputEventMessage.InputEventType inputEventType = inputEventMessage.inputEventType;

            switch (inputEventType)
            {
                case InputEventMessage.InputEventType.Dash:
                case InputEventMessage.InputEventType.DashCollision:
                    {
                        return !OC2Config.DisableDash;
                    }
                case InputEventMessage.InputEventType.Catch:
                    {
                        return !OC2Config.DisableCatch;
                    }

                case InputEventMessage.InputEventType.BeginInteraction:
                case InputEventMessage.InputEventType.EndInteraction:
                    {
                        // return !OC2Config.DisableInteract;
                        break;
                    }
                case InputEventMessage.InputEventType.EndThrow:
                    {
                        return !OC2Config.DisableThrow;
                    }
                case InputEventMessage.InputEventType.Curse:
                    {
                        break;
                    }
                case InputEventMessage.InputEventType.TriggerInteraction: // What is this?
                    {
                        break;
                    }
                default:
                    {
                        break;
                    }
            }

            return true;
        }
        
        [HarmonyPatch(typeof(ServerThrowableItem), nameof(ServerThrowableItem.CanHandleThrow))]
        [HarmonyPostfix]
        private static void CanHandleThrow(ref bool __result, ref ServerThrowableItem __instance)
        {
            if (OC2Config.DisableThrow)
            {
                GameUtils.TriggerAudio(GameOneShotAudioTag.RecipeTimeOut, __instance.gameObject.layer);
                __result = false;
            }
        }

        [HarmonyPatch(typeof(ServerPilotMovement), "Update_Movement")]
        [HarmonyPrefix]
        private static bool Update_Movement(ref ServerThrowableItem __instance)
        {
            return !OC2Config.DisableInteract;
        }

        [HarmonyPatch(typeof(PlayerControls), nameof(PlayerControls.ScanForCatch))]
        [HarmonyPostfix]
        private static void ScanForCatch(ref ICatchable __result, ref PlayerControls __instance)
        {
            if (__result != null && OC2Config.DisableCatch)
            {
                __result = null;
            }
        }

        [HarmonyPatch(typeof(ServerPlayerControlsImpl_Default), nameof(ServerPlayerControlsImpl_Default.StartDash))]
        [HarmonyPrefix]
        private static bool StartDash()
        {
            return !OC2Config.DisableDash;
        }

        [HarmonyPatch(typeof(ClientPlayerControlsImpl_Default), "Update_Movement")]
        [HarmonyPrefix]
        private static void Update_Movement(ref float ___m_dashTimer, ref ServerPlayerControlsImpl_Default __instance)
        {
            if (___m_dashTimer > 0f && OC2Config.DisableDash)
            {
                ___m_dashTimer = 0f;
                GameUtils.TriggerAudio(GameOneShotAudioTag.RecipeTimeOut, __instance.gameObject.layer);
            }
        }

        [HarmonyPatch(typeof(ClientPlayerControlsImpl_Default), "DoDash")]
        [HarmonyPrefix]
        private static bool DoDash()
        {
            return !OC2Config.DisableDash;
        }

        [HarmonyPatch(typeof(ServerWashingStation), nameof(ServerWashable.UpdateSynchronising))]
        [HarmonyPrefix]
        private static void UpdateSynchronising(ref WashingStation ___m_washingStation)
        {
            ___m_washingStation.m_cleanPlateTime = 2.0f * OC2Config.WashTimeMultiplier;
        }
    }
}
