using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;

namespace ChatSystem
{
    public class EventHandlers
    {
        public void OnPlayerVerified(VerifiedEventArgs ev)
        {
            try
            {
                if (ChatSystem.Instance?.Config != null)
                {
                    ev.Player.ShowHint("<color=yellow>💬 Proximity Chat On! Use Command: chat <message></color>", 5);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"OnPlayerVerified hatası: {ex.Message}");
            }
        }

        public void CheckCount()
        {
            if (ChatSystem.Instance.Config.MaxMessageLength > 50)
            {
                Log.Warn("It is not possible to set a message longer than 50 words.");
            }
        }
    }
}