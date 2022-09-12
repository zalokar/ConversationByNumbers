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
using TaleWorlds.Localization;

namespace ConversationByNumbers
{
    class ConversationInputHandler
    {
        static List<InputKey> numRowKeys = new() {
            InputKey.D0,
            InputKey.D1,
            InputKey.D2,
            InputKey.D3,
            InputKey.D4,
            InputKey.D5,
            InputKey.D6,
            InputKey.D7,
            InputKey.D8,
            InputKey.D9
        };
        static List<InputKey> numPadKeys = new() {
            InputKey.Numpad0,
            InputKey.Numpad1,
            InputKey.Numpad2,
            InputKey.Numpad3,
            InputKey.Numpad4,
            InputKey.Numpad5,
            InputKey.Numpad6,
            InputKey.Numpad7,
            InputKey.Numpad8,
            InputKey.Numpad9
        };

        public static void HandleConversationInput(ref MissionConversationVM dataSource, bool isBarterActive)
        {
            if (dataSource == null)
            {
                return;
            }

            int answerIndex = GetAnswerIndexOfPressedNumKey();
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

        private static int GetAnswerIndexOfPressedNumKey()
        {
            // number row keys are linear, but numpad keys are broken up by row
            foreach (InputKey numRowKey in numRowKeys)
            {
                if (Input.IsKeyPressed(numRowKey)) {
                    return (int)numRowKey - 2;
                }
            }

            foreach (InputKey numPadKey in numPadKeys)
            {
                if (Input.IsKeyPressed(numPadKey))
                {
                    switch (numPadKey)
                    {
                        case InputKey.Numpad0:
                            return 9;
                        case InputKey.Numpad1:
                        case InputKey.Numpad2:
                        case InputKey.Numpad3:
                            return (int)numPadKey - 79;
                        case InputKey.Numpad4:
                        case InputKey.Numpad5:
                        case InputKey.Numpad6:
                            return (int)numPadKey - 72;
                        case InputKey.Numpad7:
                        case InputKey.Numpad8:
                        case InputKey.Numpad9:
                            return (int)numPadKey - 65;
                    }
                }
            }

            return -1;
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
        // patch MissionConversationVM::Refresh to insert number prompts into answers
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
                    // ConversationSentenceOption local_var_7 = this._conversationManager.CurOptions[i];
                    LocalBuilder csoAddress = gen.DeclareLocal(typeof(ConversationSentenceOption));
                    yield return new CodeInstruction(OpCodes.Stloc_S, 7);
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 7); // this address used by Stfld instruction below

                    // TODO: there is potential edge case where i/local_var_6 goes past 9; in that case, skip string modding below

                    // int j = (i + 1) % 10;
                    // local_var_7.Text = new TextObject(j + ". " + x.Text.ToString());
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 6);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                    yield return new CodeInstruction(OpCodes.Add);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_S, 10);
                    yield return new CodeInstruction(OpCodes.Rem);
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
