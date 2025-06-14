using System;
using BubbleChat;
using Exiled.API.Features;
using Exiled.API.Interfaces;

namespace ProximityChat
{
    public class ProximityChat : Plugin<Config>
    {
        public override string Author => "ByLeTalhaWw";
        public override string Name => "ProximityChat";
        public override Version Version => new Version(1, 0, 0);
        public override string Prefix => "ProximityChat";

        public static ProximityChat Instance { get; private set; }

        private EventHandlers eventHandlers;

        public override void OnEnabled()
        {
            Instance = this;
            eventHandlers = new EventHandlers();

            // Player join eventi
            Exiled.Events.Handlers.Player.Verified += eventHandlers.OnPlayerVerified;

            Log.Debug("ProximityChat plugin has been enabled!");
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            if (eventHandlers != null)
            {
                Exiled.Events.Handlers.Player.Verified -= eventHandlers.OnPlayerVerified;
                eventHandlers = null;
            }

            Instance = null;
            Log.Debug("ProximityChat plugin has been disabled!");
            base.OnDisabled();
        }
    }
}