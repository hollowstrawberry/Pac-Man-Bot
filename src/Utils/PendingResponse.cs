using System;
using System.Threading;
using DSharpPlus.Entities;

namespace PacManBot.Utils
{
    /// <summary>
    /// Represents an intent to get a specific response back from a user.
    /// </summary>
    public class PendingResponse
    {
        private readonly CancellationTokenSource _cancelSource = new CancellationTokenSource();
        private DiscordMessage _response = null;

        /// <summary>The condition that a message has to fulfill in order to be accepted as a response.</summary>
        public Func<DiscordMessage, bool> Condition { get; }

        /// <summary>Is automatically cancelled when a message response is accepted.</summary>
        public CancellationToken Token => _cancelSource.Token;

        /// <summary>The message response that gets accepted, if any.</summary>
        public DiscordMessage Response
        {
            get => _response;
            set
            {
                _response = value;
                if (value is not null) _cancelSource.Cancel();
            }
        }


        public PendingResponse(Func<DiscordMessage, bool> condition)
        {
            Condition = condition;
        }
    }
}
