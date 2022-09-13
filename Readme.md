# Intro
A mod to streamline conversations by using number keys to select answers.

# Technical details
Quick overview of how this mod works.

## Input handling
This mod patches postfix function into methods `Sandbox.GauntletUI.Missions.MissionGauntletConversationView::OnMissionScreenTick()` and `Sandbox.GauntletUI.Map.GauntletMapConversationView::Tick()`.

Postfix function listens for inputs from number keys in number row and numpad and converts them to index numbers, which is used to select appropriate answer.

## Number prompt for answer
Number prompts are inserted into answer texts by performing transpiler patch into `TaleWorlds.CampaignSystem.ViewModelCollection.Conversation.MissionConversationVM::Refresh()`. Loop inside original function iterates over possible answers and transpiler patch simply uses loop index to calculate appropriate prompt to add before each answer.

Once loop index goes past 10 options, no more prompts are added. This means prompts go from '1.' for first option to '0.' for tenth option. This is edge case, but theoretically possible.
