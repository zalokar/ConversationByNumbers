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
using TaleWorlds.MountAndBlade;

namespace ConversationByNumbers
{
    class ConversationInputHandler
    {
        public static void HandleConversationInput(ref MissionConversationVM dataSource, bool isBarterActive)
        {
            int numRowKeyPressedCode = Input.GetFirstKeyPressedInRange((int)InputKey.D1);
            int numPadKeyPressedCode = Input.GetFirstKeyPressedInRange((int)InputKey.Numpad1);
            int answerIndex;

            if (numRowKeyPressedCode >= (int)InputKey.D1 && numRowKeyPressedCode <= (int)InputKey.D9)
            {
                answerIndex = numRowKeyPressedCode - 2;
            }
            else if (numPadKeyPressedCode >= (int)InputKey.Numpad1 && numPadKeyPressedCode <= (int)InputKey.Numpad9)
            {
                answerIndex = numPadKeyPressedCode - 79;
            }
            else
            {
                return;
            }

            var answerListLength = dataSource.AnswerList.Count;

            // allow numkeys to continue conversation
            if (answerListLength <= 0 && !isBarterActive)
            {
                var ExecuteContinue = AccessTools.Method(typeof(MissionConversationVM), nameof(MissionConversationVM.ExecuteContinue));
                ExecuteContinue.Invoke(dataSource, new object[] { });
                return;
            }

            if (answerIndex < 0 || answerIndex >= answerListLength)
            {
                return;
            }

            // only allow selecting options that aren't greyed out
            if (!dataSource.AnswerList[answerIndex].IsEnabled)
            {
                return;
            }

            var OnSelectOption = AccessTools.Method(typeof(MissionConversationVM), "OnSelectOption");
            OnSelectOption.Invoke(dataSource, new object[] { answerIndex });
        }
    }

    [HarmonyPatch(typeof(MissionGauntletConversationView), nameof(MissionGauntletConversationView.OnMissionScreenTick))]
    class MissionConversationPatch
    {
        static void Postfix(float dt, ref MissionGauntletConversationView __instance, ref MissionConversationVM ____dataSource)
        {
            bool isBarterActive = __instance.Mission.Mode == TaleWorlds.Core.MissionMode.Barter;
            ConversationInputHandler.HandleConversationInput(ref ____dataSource, isBarterActive);
        }
    }

    [HarmonyPatch(typeof(GauntletMapConversationView), "Tick")]
    class MapConversationPatch
    {
        static void Postfix(ref bool ____isBarterActive, ref MapConversationVM ____dataSource)
        {
            var dialogController = Traverse.Create(____dataSource).Field("_dialogController").GetValue<MissionConversationVM>();
            ConversationInputHandler.HandleConversationInput(ref dialogController, ____isBarterActive);
        }
    }

    [HarmonyPatch(typeof(MissionConversationVM), "Refresh")]
    class NumbersToAnswersPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator gen)
        {
            // anchors for transpiler to insert new code
            bool foundLdfldConversationManager = false;
            bool foundCallvirtGetItem = false;

            // what to look for in instructions
            var fieldConversationManager = AccessTools.Field(typeof(MissionConversationVM), "_conversationManager");
            var virtGetItem = AccessTools.Method(typeof(List<ConversationSentenceOption>), "get_Item");

            foreach (var instruction in instructions)
            {
                yield return instruction;

                // find `ldfld ConversationManager`, then find `callvirt get_Item()`
                if (instruction.LoadsField(fieldConversationManager))
                {
                    foundLdfldConversationManager = true;
                }

                if (!foundCallvirtGetItem && foundLdfldConversationManager && instruction.Calls(virtGetItem))
                {
                    LocalBuilder csoAddress = gen.DeclareLocal(typeof(ConversationSentenceOption));
                    yield return new CodeInstruction(OpCodes.Stloc_S, 7);
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 7); // this address used by Stfld instruction below

                    // given: ConversationSentenceOption temp = curOption[i];
                    // temp.Text = new TextObject(i + ". " + x.Text.ToString());
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 6);
                    yield return new CodeInstruction(OpCodes.Box, typeof(int));
                    yield return new CodeInstruction(OpCodes.Ldstr, ". ");

                    yield return new CodeInstruction(OpCodes.Ldloca_S, 7);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(ConversationSentenceOption), nameof(ConversationSentenceOption.Text)));
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(object), nameof(Object.ToString)));
                    yield return new CodeInstruction(
                        OpCodes.Call, AccessTools.Method(typeof(String), nameof(String.Concat), parameters: new Type[] { typeof(object), typeof(object), typeof(object) })
                    );

                    yield return new CodeInstruction(OpCodes.Ldnull);
                    yield return new CodeInstruction(OpCodes.Newobj, AccessTools.Constructor(typeof(TextObject), new Type[] { typeof(string), typeof(Dictionary<string, object>) }));

                    yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(ConversationSentenceOption), nameof(ConversationSentenceOption.Text)));
                    
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 7);

                    foundCallvirtGetItem = true;
                }
            }

            if (foundLdfldConversationManager is false)
            {
                throw new ArgumentException("Cannot find field `_conversationManager` in Conversation.MissionConversationVM");
            }
        }
    }
}
