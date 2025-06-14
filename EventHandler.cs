using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;

namespace ProximityChat
{
    public class EventHandlers
    {
        public void OnPlayerVerified(VerifiedEventArgs ev)
        {
            try
            {
                if (ProximityChat.Instance?.Config != null)
                {
                    ev.Player.ShowHint("<color=yellow>💬 Proximity Chat On! Use Command: chat <message></color>", 5);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"OnPlayerVerified hatası: {ex.Message}");
            }
        }
    }
}