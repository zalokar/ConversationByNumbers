using HarmonyLib;
using SandBox.GauntletUI.Map;
using SandBox.GauntletUI.Missions;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.ViewModelCollection.Conversation;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapConversation;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;

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

            var traverse = Traverse.Create(____dataSource);
            var answerListLength = traverse.Field("_answerList").Property("Count").GetValue<int>();
            if (answerIndex < 0 || answerIndex >= answerListLength)
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

            var OnSelectOption = AccessTools.Method(typeof(MissionConversationVM), "OnSelectOption");
            OnSelectOption.Invoke(dialogController, new object[] { answerIndex });
        }
    }
}
