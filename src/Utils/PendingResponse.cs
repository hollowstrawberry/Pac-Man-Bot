using System;
using System.Threading;
using Discord.WebSocket;

namespace PacManBot.Utils
{
    /// <summary>
    /// Represents an intent to get a specific response back from a user.
    /// </summary>
    public class PendingResponse
    {
        private CancellationTokenSource cancelSource = new CancellationTokenSource();
        private SocketUserMessage internalResponse = null;

        /// <summary>The condition that a message has to fulfill in order to be accepted as a response.</summary>
        public Func<SocketUserMessage, bool> Condition { get; }

        /// <summary>Is automatically cancelled when a message response is accepted.</summary>
        public CancellationToken Token => cancelSource.Token;

        /// <summary>The message response that gets accepted, if any.</summary>
        public SocketUserMessage Response
        {
            get => internalResponse;
            set
            {
                internalResponse = value;
                if (value != null) cancelSource.Cancel();
            }
        }


        public PendingResponse(Func<SocketUserMessage, bool> condition)
        {
            Condition = condition;
        }
    }
}
