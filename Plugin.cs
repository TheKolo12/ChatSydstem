using System;
using ChatSystem;
using Exiled.API.Features;
using Exiled.API.Interfaces;

namespace ChatSystem
{
    public class ChatSystem : Plugin<Config>
    {
        public override string Author => "ByLeTalhaWw & TheKolo12";
        public override string Name => "ChatSystem";
        public override Version Version => new Version(1, 0, 1);
        public override string Prefix => "ChatSystem";

        public static ChatSystem Instance { get; private set; }

        private EventHandlers eventHandlers;

        public override void OnEnabled()
        {
            Instance = this;
            eventHandlers = new EventHandlers();

            Exiled.Events.Handlers.Player.Verified += eventHandlers.OnPlayerVerified;

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
            base.OnDisabled();
        }
    }
}