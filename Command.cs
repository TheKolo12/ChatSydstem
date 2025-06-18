using CommandSystem;
using Exiled.API.Features;
using MEC;
using RemoteAdmin;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;
using AdminToys;
using System.Reflection;
using ChatSystem;
using ChatSydstem;

namespace ChatSystem
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class ChatCommand : ICommand
    {
        public string Command => "chat";
        public string[] Aliases => new[] { "c", "say" };
        public string Description => "Send proximity chat message";

        private static Dictionary<Player, AdminToyBase> activeTexts = new Dictionary<Player, AdminToyBase>();
        private static Dictionary<Player, string> activeMessages = new Dictionary<Player, string>();
        private static Dictionary<Player, string> messageColors = new Dictionary<Player, string>();
        private static Dictionary<Player, CoroutineHandle> trackingCoroutines = new Dictionary<Player, CoroutineHandle>();

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            response = "An error occurred!";

            try
            {
                Player player = GetPlayerFromSender(sender);

                // Player Case
                switch (true)
                {
                    // When player == null
                    case var _ when player is null:
                        response = "Player not found!";
                        return false;
                    
                    // When player isn't alive
                    case var _ when !player.IsAlive:
                        response = "You can only use this command while alive!";
                        return false;

                    // When player is muted
                    case var _ when player.IsMuted:
                        response = "You can't use this command, you are muted!";
                        return false;
                }

                int maxLength = ChatSystem.Instance.Config.MaxMessageLength;
                string message = string.Join(" ", arguments).Trim();

                switch (true)
                {
                    // When arguments is 0
                    case var _ when arguments.Count == 0:
                        response = $"💬 Usage: chat <message> | Maximum {maxLength} characters allowed";
                        return true;

                    // When the text is empy
                    case var _ when string.IsNullOrWhiteSpace(message):
                        response = "Message cannot be empty!";
                        return false;

                    // When the text is so longer
                    case var _ when message.Length > maxLength:
                        response = $"❌ Your message is too long! (Max: {maxLength} characters, Yours: {message.Length} characters)";
                        return false;
                }


                CleanupPreviousMessages(player);
                int sentCount = ProcessProximityChat(player, message);

                response = $"💬 Your message '{message}' was sent to {sentCount} players! ({message.Length}/{maxLength} characters)";
                ChatLogger.LogMessage(player, message);
                return true;
            }

            catch (Exception ex)
            {
                Log.Error($"ChatCommand error: {ex}");
                response = "An error occurred!";
            }

            return false;
        }

        private Player GetPlayerFromSender(ICommandSender sender)
        {
            try
            {
                if (sender is PlayerCommandSender { ReferenceHub: not null } playerSender)
                    return Player.Get(playerSender.ReferenceHub);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to get player from sender: {ex}");
            }

            return null;
        }

        private void CleanupPreviousMessages(Player sender)
        {
            try
            {
                if (activeTexts.TryGetValue(sender, out var textDisplay))
                {
                    if (textDisplay != null)
                    {
                        NetworkServer.UnSpawn(textDisplay.gameObject);
                        NetworkServer.Destroy(textDisplay.gameObject);
                    }
                    activeTexts.Remove(sender);
                }

                activeMessages.Remove(sender);
                messageColors.Remove(sender);

                if (trackingCoroutines.TryGetValue(sender, out var coroutine))
                {
                    Timing.KillCoroutines(coroutine);
                    trackingCoroutines.Remove(sender);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[ChatSystem] CleanupPreviousMessages error: {ex}");
            }
        }

        private int ProcessProximityChat(Player sender, string message)
        {
            try
            {
                string formattedMessage = string.Format(ChatSystem.Instance.Config.MessageFormat, message);

                var players = Player.List.Where(p => p != null && p.IsAlive);

                var nearbyPlayers = players
                    .Where(p => Vector3.Distance(sender.Position, p.Position) <= ChatSystem.Instance.Config.ChatRange)
                    .ToList();

                var textViewers = players
                    .Where(p => Vector3.Distance(sender.Position, p.Position) <= ChatSystem.Instance.Config.TextVisibilityRange)
                    .ToList();


                Create3DTextDisplay(sender, formattedMessage, textViewers, ChatSystem.Instance.Config.MessageColor, ChatSystem.Instance.Config.MessageDuration);

                Log.Debug($"{sender.Nickname} proximity chat: '{message}' ({message.Length} characters, {nearbyPlayers.Count} players heard, {textViewers.Count} players see text)");

                return nearbyPlayers.Count;
            }
            catch (Exception ex)
            {
                Log.Error($"ProcessProximityChat error: {ex}");
                return 0;
            }
        }

        private void Create3DTextDisplay(Player sender, string message, List<Player> viewers, string messageColor, float duration)
        {
            try
            {
                string fullText = $"<color={ChatSystem.Instance.Config.ChatPrefixColor}><size=1>CHAT:</size></color><color={messageColor}><size=1>{message}</size></color>";

                activeMessages[sender] = fullText;
                messageColors[sender] = messageColor;

                Vector3 initialPosition = sender.Position + Vector3.up * ChatSystem.Instance.Config.HeightOffset;
                CreateTextAtPosition(sender, initialPosition);

                trackingCoroutines[sender] = Timing.RunCoroutine(TrackPlayerWithVisibilityControl(sender, duration));
            }
            catch (Exception ex)
            {
                Log.Error($"Create3DTextDisplay error: {ex}");
            }
        }

        private void CreateTextAtPosition(Player sender, Vector3 position)
        {
            try
            {
                if (activeTexts.ContainsKey(sender) && activeTexts[sender] != null)
                {
                    NetworkServer.UnSpawn(activeTexts[sender].gameObject);
                    NetworkServer.Destroy(activeTexts[sender].gameObject);
                }

                GameObject textPrefab = GetTextDisplayPrefab();
                if (textPrefab == null)
                {
                    Log.Error("TextDisplay prefab not found!");
                    return;
                }

                Quaternion textRotation = GetOwnerDirectionBillboard(position, sender);
                GameObject textObj = UnityEngine.Object.Instantiate(textPrefab, position, textRotation);

                var adminToyBase = textObj.GetComponent<AdminToyBase>();
                if (adminToyBase == null)
                {
                    Log.Error("AdminToyBase component not found!");
                    UnityEngine.Object.Destroy(textObj);
                    return;
                }

                if (activeMessages.ContainsKey(sender))
                {
                    SetTextToyProperties(adminToyBase, activeMessages[sender], activeMessages[sender], ChatSystem.Instance.Config.TextSize);
                }

                NetworkServer.Spawn(adminToyBase.gameObject);
                activeTexts[sender] = adminToyBase;

                UpdateTextVisibility(sender, adminToyBase);
            }
            catch (Exception ex)
            {
                Log.Error($"CreateTextAtPosition error: {ex}");
            }
        }

        private IEnumerator<float> TrackPlayerWithVisibilityControl(Player sender, float duration)
        {
            float elapsed = 0f;
            Vector3 lastPosition = sender.Position;
            Vector3 lastDirection = GetPlayerLookDirection(sender);

            while (elapsed < duration && sender != null && sender.IsAlive)
            {
                Vector3 currentPosition = sender.Position;
                Vector3 currentDirection = GetPlayerLookDirection(sender);

                bool positionChanged = Vector3.Distance(currentPosition, lastPosition) > 0.05f;
                bool directionChanged = Vector3.Angle(currentDirection, lastDirection) > 3f;

                if (positionChanged || directionChanged || elapsed % 0.1f < 0.03f)
                {
                    Vector3 basePosition = currentPosition + Vector3.up * ChatSystem.Instance.Config.HeightOffset;

                    float bobOffset = ChatSystem.Instance.Config.EnableBobbing ? Mathf.Sin(elapsed * 1.2f) * ChatSystem.Instance.Config.BobbingIntensity : 0f;
                    Vector3 finalTextPosition = basePosition + Vector3.up * bobOffset;

                    CreateTextAtPosition(sender, finalTextPosition);

                    if (positionChanged)
                    {
                        lastPosition = currentPosition;
                    }

                    if (directionChanged)
                    {
                        lastDirection = currentDirection;
                    }
                }

                elapsed += 0.01f;
                yield return Timing.WaitForSeconds(0.01f);
            }

            if (activeTexts.ContainsKey(sender) && activeTexts[sender] != null)
            {
                NetworkServer.UnSpawn(activeTexts[sender].gameObject);
                NetworkServer.Destroy(activeTexts[sender].gameObject);
                activeTexts.Remove(sender);
            }

            if (activeMessages.ContainsKey(sender))
                activeMessages.Remove(sender);

            if (messageColors.ContainsKey(sender))
                messageColors.Remove(sender);

            if (trackingCoroutines.ContainsKey(sender))
                trackingCoroutines.Remove(sender);
        }

        private void UpdateTextVisibility(Player textOwner, AdminToyBase textToy)
        {
            try
            {
                if (textToy == null || textOwner == null) return;


                foreach (var player in Player.List)
                {
                    if (player == null || !player.IsAlive) continue;

                    float distance = Vector3.Distance(player.Position, textOwner.Position);

                    if (distance <= ChatSystem.Instance.Config.TextVisibilityRange)
                    {
                        ShowTextToPlayer(textToy, player);
                    }
                    else
                    {
                        HideTextFromPlayer(textToy, player);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"UpdateTextVisibility error: {ex}");
            }
        }

        private void ShowTextToPlayer(AdminToyBase textToy, Player player)
        {
            try
            {
                if (textToy.gameObject.activeSelf == false)
                {
                    textToy.gameObject.SetActive(true);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"ShowTextToPlayer error: {ex}");
            }
        }

        private void HideTextFromPlayer(AdminToyBase textToy, Player player)
        {
            try
            {
            }
            catch (Exception ex)
            {
                Log.Error($"HideTextFromPlayer error: {ex}");
            }
        }

        private Vector3 GetPlayerLookDirection(Player player)
        {
            try
            {
                if (player.CameraTransform != null)
                {
                    return player.CameraTransform.forward;
                }
                return player.ReferenceHub.transform.forward;
            }
            catch (Exception ex)
            {
                Log.Error($"GetPlayerLookDirection error: {ex}");
                return Vector3.forward;
            }
        }

        private Quaternion GetOwnerDirectionBillboard(Vector3 textPosition, Player textOwner)
        {
            try
            {
                Vector3 ownerLookDirection = GetPlayerLookDirection(textOwner);

                Vector3 horizontalDirection = new Vector3(ownerLookDirection.x, 0, ownerLookDirection.z).normalized;

                if (horizontalDirection.magnitude > 0.1f)
                {
                    return Quaternion.LookRotation(-horizontalDirection);
                }

                return Quaternion.identity;
            }
            catch (Exception ex)
            {
                Log.Error($"GetOwnerDirectionBillboard error: {ex}");
                return Quaternion.identity;
            }
        }

        private void RecreateTextAtPosition(AdminToyBase existingText, Vector3 position, Quaternion rotation)
        {
            try
            {
                existingText.transform.position = position;
                existingText.transform.rotation = rotation;
            }
            catch (Exception ex)
            {
                Log.Error($"RecreateTextAtPosition error: {ex}");
            }
        }

        private GameObject GetTextDisplayPrefab()
        {
            try
            {
                foreach (var prefab in NetworkManager.singleton.spawnPrefabs)
                {
                    if (prefab.name == "TextToy")
                    {
                        var adminToy = prefab.GetComponent<AdminToyBase>();
                        if (adminToy != null)
                        {
                            return prefab;
                        }
                    }
                }

                Log.Error("TextToy prefab not found!");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"GetTextDisplayPrefab error: {ex}");
                return null;
            }
        }
        private void SetTextToyProperties(AdminToyBase adminToyBase, string formattedText, string rawMessage, float size)
        {
            try
            {
                var type = adminToyBase.GetType();

                // Set Arguments (Clear + Add)
                var argumentsField = type.GetField("Arguments", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var syncList = argumentsField?.GetValue(adminToyBase);
                syncList?.GetType().GetMethod("Clear")?.Invoke(syncList, null);
                syncList?.GetType().GetMethod("Add")?.Invoke(syncList, new object[] { formattedText });

                // Set text format (field + properties)
                type.GetField("_textFormat", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.SetValue(adminToyBase, formattedText);

                type.GetProperty("TextFormat", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.SetValue(adminToyBase, formattedText, null);

                type.GetProperty("Network_textFormat", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.SetValue(adminToyBase, formattedText, null);

                // Set display size
                type.GetField("_displaySize", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.SetValue(adminToyBase, new Vector2(70 * size, 15 * size));

                // Set scale
                type.GetField("Scale", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.SetValue(adminToyBase, Vector3.one * size);
            }
            catch (Exception ex)
            {
                Log.Error($"SetTextToyProperties error: {ex}");
            }
        }

    }
}
