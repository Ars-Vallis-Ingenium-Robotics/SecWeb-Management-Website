using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;

namespace SecWeb.Bot
{
    public sealed class ChipyReactionService
    {
        // ---------------------------------------------------------
        // ACTIVE REACTION MENUS
        // ---------------------------------------------------------
        //
        // Key:
        //     Discord message ID
        //
        // Value:
        //     The selection currently waiting for a reaction.
        //

        private readonly ConcurrentDictionary<
            ulong,
            PendingSelection> _pendingSelections =
                new();


        // =========================================================
        // WAIT FOR A USER TO SELECT AN EMOJI
        // =========================================================

        public async Task<string?> WaitForSelectionAsync(
            ulong messageId,
            ulong userId,
            IReadOnlyCollection<string> validEmojiNames,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            PendingSelection selection =
                new(
                    userId,
                    validEmojiNames);


            if (!_pendingSelections.TryAdd(
                messageId,
                selection))
            {
                throw new InvalidOperationException(
                    "A Chipy reaction selection is already active for this message.");
            }


            using CancellationTokenSource timeoutSource =
                CancellationTokenSource
                    .CreateLinkedTokenSource(
                        cancellationToken);


            timeoutSource.CancelAfter(
                timeout);


            using CancellationTokenRegistration registration =
                timeoutSource.Token.Register(
                    () =>
                    {
                        selection.Completion
                            .TrySetCanceled(
                                timeoutSource.Token);
                    });


            try
            {
                return await selection
                    .Completion
                    .Task;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            finally
            {
                _pendingSelections.TryRemove(
                    messageId,
                    out _);
            }
        }


        // =========================================================
        // RECEIVE DISCORD REACTIONS
        // =========================================================

        public Task HandleReactionAddedAsync(
            Cacheable<IUserMessage, ulong> cachedMessage,
            Cacheable<IMessageChannel, ulong> cachedChannel,
            SocketReaction reaction)
        {
            // -----------------------------------------------------
            // FIND THE REACTION MENU
            // -----------------------------------------------------

            if (!_pendingSelections.TryGetValue(
                reaction.MessageId,
                out PendingSelection? selection))
            {
                return Task.CompletedTask;
            }


            // -----------------------------------------------------
            // ONLY THE USER WHO STARTED THE COMMAND MAY CHOOSE
            // -----------------------------------------------------
            //
            // If somebody else clicks one of Chipy's emoji,
            // their reaction is ignored.
            //

            if (reaction.UserId !=
                selection.UserId)
            {
                return Task.CompletedTask;
            }


            string emojiName =
                reaction.Emote.Name;


            // -----------------------------------------------------
            // IGNORE REACTIONS THAT ARE NOT PART OF THE MENU
            // -----------------------------------------------------

            if (!selection.ValidEmojiNames.Contains(
                emojiName))
            {
                return Task.CompletedTask;
            }


            // -----------------------------------------------------
            // COMPLETE THE SELECTION
            // -----------------------------------------------------

            selection.Completion
                .TrySetResult(
                    emojiName);


            return Task.CompletedTask;
        }


        // =========================================================
        // INTERNAL PENDING SELECTION
        // =========================================================

        private sealed class PendingSelection
        {
            public ulong UserId { get; }


            public HashSet<string> ValidEmojiNames { get; }


            public TaskCompletionSource<string> Completion { get; }


            public PendingSelection(
                ulong userId,
                IReadOnlyCollection<string> validEmojiNames)
            {
                UserId =
                    userId;


                ValidEmojiNames =
                    new HashSet<string>(
                        validEmojiNames,
                        StringComparer.Ordinal);


                Completion =
                    new TaskCompletionSource<string>(
                        TaskCreationOptions
                            .RunContinuationsAsynchronously);
            }
        }
    }
}