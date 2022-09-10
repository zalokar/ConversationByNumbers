using HarmonyLib;
using SandBox.GauntletUI.Map;
using SandBox.GauntletUI.Missions;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.ViewModelCollection.Conversation;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapConversation;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ConversationByNumbers
{
    [HarmonyPatch(typeof(MissionGauntletConversationView), nameof(MissionGauntletConversationView.OnMissionScreenTick))]
    class MissionConversationPatch
    {
        static void Postfix(float dt, ref MissionConversationVM ____dataSource)
        {
            int numRowKeyPressedCode = Input.GetFirstKeyPressedInRange((int)InputKey.D1);
            int numPadKeyPressedCode = Input.GetFirstKeyPressedInRange((int)InputKey.Numpad1);
            int answerIndex = -1;

            if (numRowKeyPressedCode >= (int)InputKey.D1 && numRowKeyPressedCode <= (int)InputKey.D9)
            {
                answerIndex = numRowKeyPressedCode - 2;
            } else if (numPadKeyPressedCode >= (int)InputKey.Numpad1 && numPadKeyPressedCode <= (int)InputKey.Numpad9)
            {
                answerIndex = numPadKeyPressedCode - 79;
            } else
            {
                return;
            }

            var dataSource = Traverse.Create(____dataSource).GetValue<MissionConversationVM>();
            var answerListLength = dataSource.AnswerList.Count;
            if (answerIndex < 0 || answerIndex >= answerListLength)
            {
                return;
            }

            if (!dataSource.AnswerList[answerIndex].IsEnabled)
            {
                return;
            }
            
            var OnSelectOption = AccessTools.Method(typeof(MissionConversationVM), "OnSelectOption");
            OnSelectOption.Invoke(____dataSource, new object[] { answerIndex });
        }
    }

    [HarmonyPatch(typeof(GauntletMapConversationView), "Tick")]
    class MapConversationPatch
    {
        static void Postfix(ref MapConversationVM ____dataSource)
        {
            int numRowKeyPressedCode = Input.GetFirstKeyPressedInRange((int)InputKey.D1);
            int numPadKeyPressedCode = Input.GetFirstKeyPressedInRange((int)InputKey.Numpad1);
            int answerIndex = -1;

            if (numRowKeyPressedCode >= (int)InputKey.D1 && numRowKeyPressedCode <= (int)InputKey.D9)
            {
                answerIndex = numRowKeyPressedCode - 2;
            } else if (numPadKeyPressedCode >= (int)InputKey.Numpad1 && numPadKeyPressedCode <= (int)InputKey.Numpad9)
            {
                answerIndex = numPadKeyPressedCode - 79;
            } else
            {
                return;
            }

            var traverse = Traverse.Create(____dataSource);
            var dialogController = traverse.Field("_dialogController").GetValue<MissionConversationVM>();
            var answerListLength = dialogController.AnswerList.Count;
            if (answerIndex < 0 || answerIndex >= answerListLength)
            {
                return;
            }

            if (!dialogController.AnswerList[answerIndex].IsEnabled)
            {
                return;
            }

            var OnSelectOption = AccessTools.Method(typeof(MissionConversationVM), "OnSelectOption");
            OnSelectOption.Invoke(dialogController, new object[] { answerIndex });
        }
    }

    [HarmonyPatch(typeof(MissionConversationVM), "Refresh")]
    class NumbersToAnswersPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var setForceCameraToTurnToPlayer = AccessTools.Method(typeof(ConversationManager), "set_ForceCameraToTurnToPlayer");
            var foundInstructionBeforeLoop = false;
            var foundInstructionInsideLoop = false;

            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Callvirt && instruction.operand == (object)setForceCameraToTurnToPlayer)
                {
                    foundInstructionBeforeLoop = true;
                }

                if (!foundInstructionInsideLoop && foundInstructionBeforeLoop && instruction.opcode == OpCodes.Ldarg_0)
                {
                    // X = i + ". "
                    yield return new CodeInstruction(OpCodes.Ldloc, 6);
                    yield return new CodeInstruction(OpCodes.Ldstr, ". ");
                    yield return new CodeInstruction(OpCodes.Box, typeof(Int32));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(string), nameof(String.Concat), parameters: new Type[] { typeof(object), typeof(object) }));

                    // X += this._conversationManager.CurOptions[i].Text.ToString()
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MissionConversationVM), "_conversationManager"));
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ConversationManager), "get_CurOptions"));
                    yield return new CodeInstruction(OpCodes.Ldloc, 6);
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(List<ConversationSentenceOption>), "get_Item"));
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ConversationSentenceOption), nameof(ConversationSentenceOption.Text)));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TextObject), nameof(TextObject.ToString)));
                    yield return new CodeInstruction(OpCodes.Box, typeof(Int32));
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(String), nameof(String.Concat), parameters: new Type[] { typeof(object), typeof(object) }));

                    // this._conversationManager.CurOptions[i].Text = X
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MissionConversationVM), "_conversationManager"));
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ConversationManager), "get_CurOptions"));
                    yield return new CodeInstruction(OpCodes.Ldloc, 6);
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(List<ConversationSentenceOption>), "get_Item"));
                    yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(ConversationSentenceOption), nameof(ConversationSentenceOption.Text)));
                    foundInstructionInsideLoop = true;
                }
                yield return instruction;
            }

            if (foundInstructionBeforeLoop is false)
            {
                throw new ArgumentException("Cannot find ConversationManager::set_ForceCameraToTurnToPlayer in Conversation.MissionConversationVM");
            }
        }
    }
}
