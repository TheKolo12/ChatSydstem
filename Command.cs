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

namespace BubbleChat
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

                if (player == null)
                {
                    response = "Player not found!";
                    return false;
                }

                if (!player.IsAlive)
                {
                    response = "You can only use this command while alive!";
                    return false;
                }

                var config = GetBubbleChatConfig();
                int maxLength = config?.MaxMessageLength ?? 50;

                if (arguments.Count == 0)
                {
                    response = $"💬 Usage: chat <message> | Maximum {maxLength} characters allowed";
                    return true;
                }

                string message = string.Join(" ", arguments);

                if (string.IsNullOrEmpty(message.Trim()))
                {
                    response = "Message cannot be empty!";
                    return false;
                }

                if (message.Length > maxLength)
                {
                    response = $"❌ Your message is too long! (Max: {maxLength} characters, Yours: {message.Length} characters)";
                    return false;
                }

                CleanupPreviousMessages(player);
                int sentCount = ProcessProximityChat(player, message);

                response = $"💬 Your message '{message}' was sent to {sentCount} players! ({message.Length}/{maxLength} characters)";
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
                if (sender is PlayerCommandSender playerSender && playerSender.ReferenceHub != null)
                {
                    return Player.Get(playerSender.ReferenceHub);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"GetPlayerFromSender error: {ex}");
            }

            return null;
        }

        private void CleanupPreviousMessages(Player sender)
        {
            try
            {
                if (activeTexts.ContainsKey(sender))
                {
                    if (activeTexts[sender] != null)
                    {
                        NetworkServer.UnSpawn(activeTexts[sender].gameObject);
                        NetworkServer.Destroy(activeTexts[sender].gameObject);
                    }
                    activeTexts.Remove(sender);
                }

                if (activeMessages.ContainsKey(sender))
                    activeMessages.Remove(sender);

                if (messageColors.ContainsKey(sender))
                    messageColors.Remove(sender);

                if (trackingCoroutines.ContainsKey(sender))
                {
                    Timing.KillCoroutines(trackingCoroutines[sender]);
                    trackingCoroutines.Remove(sender);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"CleanupPreviousMessages error: {ex}");
            }
        }

        private int ProcessProximityChat(Player sender, string message)
        {
            try
            {
                var config = GetBubbleChatConfig();

                float chatRange = config?.ChatRange ?? 15f;
                float textVisibilityRange = config?.TextVisibilityRange ?? 8f;
                string messageFormat = config?.MessageFormat ?? "{0}";
                string messageColor = config?.MessageColor ?? "#FFFFFF";
                float messageDuration = config?.MessageDuration ?? 5f;
                bool debug = config?.Debug ?? true;

                string formattedMessage = string.Format(messageFormat, message);

                // Players within chat range
                var nearbyPlayers = Player.List.Where(p =>
                    p != null &&
                    p.IsAlive &&
                    Vector3.Distance(sender.Position, p.Position) <= chatRange
                ).ToList();

                var textViewers = Player.List.Where(p =>
                    p != null &&
                    p.IsAlive &&
                    Vector3.Distance(sender.Position, p.Position) <= textVisibilityRange
                ).ToList();

                Create3DTextDisplay(sender, formattedMessage, textViewers, messageColor, messageDuration);

                if (debug)
                {
                    Log.Debug($"{sender.Nickname} proximity chat: '{message}' ({message.Length} characters, {nearbyPlayers.Count} players heard, {textViewers.Count} players see text)");
                }

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
                var config = GetBubbleChatConfig();
                string chatPrefixColor = config?.ChatPrefixColor ?? "#00FF00";
                float heightOffset = config?.HeightOffset ?? 0.9f;

                string fullText = $"<color={chatPrefixColor}><size=1>CHAT:</size></color><color={messageColor}><size=1>{message}</size></color>";

                activeMessages[sender] = fullText;
                messageColors[sender] = messageColor;

                Vector3 initialPosition = sender.Position + Vector3.up * heightOffset;
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
                    var config = GetBubbleChatConfig();
                    float textSize = config?.TextSize ?? 1.0f;
                    SetTextToyProperties(adminToyBase, activeMessages[sender], activeMessages[sender], textSize);
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
                var config = GetBubbleChatConfig();
                float heightOffset = config?.HeightOffset ?? 0.9f;
                bool enableBobbing = config?.EnableBobbing ?? true;
                float bobbingIntensity = config?.BobbingIntensity ?? 0.005f;

                Vector3 currentPosition = sender.Position;
                Vector3 currentDirection = GetPlayerLookDirection(sender);

                bool positionChanged = Vector3.Distance(currentPosition, lastPosition) > 0.05f;
                bool directionChanged = Vector3.Angle(currentDirection, lastDirection) > 3f;

                if (positionChanged || directionChanged || elapsed % 0.1f < 0.03f)
                {
                    Vector3 basePosition = currentPosition + Vector3.up * heightOffset;

                    float bobOffset = enableBobbing ? Mathf.Sin(elapsed * 1.2f) * bobbingIntensity : 0f;
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

                var config = GetBubbleChatConfig();
                float textVisibilityRange = config?.TextVisibilityRange ?? 8f;

                foreach (var player in Player.List)
                {
                    if (player == null || !player.IsAlive) continue;

                    float distance = Vector3.Distance(player.Position, textOwner.Position);

                    if (distance <= textVisibilityRange)
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

                var argumentsField = type.GetField("Arguments", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (argumentsField != null)
                {
                    var syncList = argumentsField.GetValue(adminToyBase);
                    if (syncList != null)
                    {
                        var clearMethod = syncList.GetType().GetMethod("Clear");
                        var addMethod = syncList.GetType().GetMethod("Add");

                        if (clearMethod != null && addMethod != null)
                        {
                            clearMethod.Invoke(syncList, null);
                            addMethod.Invoke(syncList, new object[] { formattedText });
                        }
                    }
                }

                var textFormatField = type.GetField("_textFormat", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (textFormatField != null)
                {
                    textFormatField.SetValue(adminToyBase, formattedText);
                }

                var textFormatProperty = type.GetProperty("TextFormat", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (textFormatProperty != null && textFormatProperty.CanWrite)
                {
                    textFormatProperty.SetValue(adminToyBase, formattedText);
                }

                var networkTextFormatProperty = type.GetProperty("Network_textFormat", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (networkTextFormatProperty != null && networkTextFormatProperty.CanWrite)
                {
                    networkTextFormatProperty.SetValue(adminToyBase, formattedText);
                }

                var displaySizeField = type.GetField("_displaySize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (displaySizeField != null)
                {
                    Vector2 newSize = new Vector2(70 * size, 15 * size);
                    displaySizeField.SetValue(adminToyBase, newSize);
                }

                var scaleField = type.GetField("Scale", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (scaleField != null)
                {
                    Vector3 newScale = Vector3.one * size;
                    scaleField.SetValue(adminToyBase, newScale);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"SetTextToyProperties error: {ex}");
            }
        }

        private BubbleChat.Config GetBubbleChatConfig()
        {
            try
            {
                var plugins = Exiled.Loader.Loader.Plugins;

                foreach (var plugin in plugins)
                {
                    if (plugin.GetType().Namespace == "BubbleChat")
                    {
                        var configProperty = plugin.GetType().GetProperty("Config");
                        if (configProperty != null)
                        {
                            var config = configProperty.GetValue(plugin);
                            if (config is BubbleChat.Config bubbleChatConfig)
                            {
                                return bubbleChatConfig;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"GetBubbleChatConfig error: {ex}");
            }

            return null;
        }
    }
}