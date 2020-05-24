using System;
using System.Net.WebSockets;
using System.Threading;
using TwitchLib;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Client.Exceptions;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace Twitch_Bot
{
    internal class TwitchChatBot
    {

        readonly ConnectionCredentials credentials = new ConnectionCredentials(TwitchInfo.BotUsername, TwitchInfo.BotToken);
        TwitchClient client;
        TimerVote timerVote;

        internal void Connect()
        {
            Console.WriteLine("Connecting");

            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 20 / 2,
                WhispersAllowedInPeriod = 20 / 2,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };

            WebSocketClient customClient = new WebSocketClient(clientOptions);
            client = new TwitchClient(customClient);
            client.Initialize(credentials, TwitchInfo.ChannelName);

            client.OnLog += Client_OnLog;
            client.OnConnectionError += Client_OnConnectionError;
            client.OnMessageReceived += Client_OnMessageReceived;
            client.OnChatCommandReceived += Client_OnChatCommandReceived;

            client.Connect();

            //timerVote = new TimerVote();
        }

        private void Client_OnChatCommandReceived(object sender, OnChatCommandReceivedArgs e)
        {
            switch (e.Command.CommandText)
            {
                case "initvote":
                    //client.SendMessage(e.Command.ChatMessage.Channel, "yup");
                    if ((e.Command.ChatMessage.IsModerator || e.Command.ChatMessage.IsBroadcaster))
                    {
                        if (e.Command.ArgumentsAsList.Count <= 0)
                        {
                            client.SendMessage(e.Command.ChatMessage.Channel, "Error: no minimum votes inputted to succeed.");
                            break;
                        }
                        int succeed;
                        string title = "";
                        if (e.Command.ArgumentsAsList.Count > 1)
                        {
                            for (int i = 1; i < e.Command.ArgumentsAsList.Count; i++)
                            {
                                string space = i == 1 ? "" : " ";
                                title += space + e.Command.ArgumentsAsList[i];
                            }
                        }
                        bool parsed = int.TryParse(e.Command.ArgumentsAsList[0], out succeed);
                        if (parsed)
                        {
                            if (title != "")
                            {
                                timerVote = new TimerVote(succeed, title);
                                client.SendMessage(e.Command.ChatMessage.Channel, $"Started a vote: '{title}'! Requires {succeed} votes to succeed.");
                            }
                            else
                            {
                                timerVote = new TimerVote(succeed);
                                client.SendMessage(e.Command.ChatMessage.Channel, $"Started a vote! Requires {succeed} votes to succeed.");
                            }
                        }
                        else
                        {
                            client.SendMessage(e.Command.ChatMessage.Channel, $"Error: unknown number.");
                        }
                    }
                    break;
                case "cancel":
                    if ((e.Command.ChatMessage.IsModerator || e.Command.ChatMessage.IsBroadcaster))
                    {
                        timerVote.SetActive(false);

                        string voteTitle = timerVote.GetTitle() != "" ? $" for '{timerVote.GetTitle()}'" : "";
                        client.SendMessage(e.Command.ChatMessage.Channel, $"The vote{voteTitle} has been cancelled.");
                    }
                    break;
                case "vote":
                    if (CheckNull())
                    {
                        client.SendMessage(e.Command.ChatMessage.Channel, "No vote has been initialized :/");
                        break;
                    }
                    if (!CheckActive())
                    {
                        client.SendMessage(e.Command.ChatMessage.Channel, "No vote is currently active :/");
                        break;
                    }
                    if (!timerVote.AddVote(e.Command.ChatMessage.DisplayName))
                    {
                        client.SendMessage(e.Command.ChatMessage.Channel, $"Sorry, {e.Command.ChatMessage.DisplayName}, you have already submitted a vote.");
                        break;
                    }
                    if (timerVote.CheckVotes())
                    {
                        string voteTitle = timerVote.GetTitle() != "" ? $" for '{timerVote.GetTitle()}'" : "";
                        client.SendMessage(e.Command.ChatMessage.Channel, $" Thank you, {e.Command.ChatMessage.DisplayName}, the vote{voteTitle} has been passed! Total: {timerVote.GetVotes()}/{timerVote.GetSucceed()}");
                        break;
                    }
                    client.SendMessage(e.Command.ChatMessage.Channel, $"{e.Command.ChatMessage.DisplayName} has added a vote! Current total: {timerVote.GetVotes()}/{timerVote.GetSucceed()}");
                    break;
                case "retract":
                    if (CheckNull())
                    {
                        client.SendMessage(e.Command.ChatMessage.Channel, "No vote has been initialized :/");
                        break;
                    }
                    if (!CheckActive())
                    {
                        client.SendMessage(e.Command.ChatMessage.Channel, "No vote is currently active :/");
                        break;
                    }
                    if (timerVote.GetVotes() > 0)
                    {
                        if (!timerVote.SubtractVote(e.Command.ChatMessage.DisplayName))
                        {
                            client.SendMessage(e.Command.ChatMessage.Channel, $"You have not submitted a vote yet, {e.Command.ChatMessage.DisplayName}.");
                        }
                        client.SendMessage(e.Command.ChatMessage.Channel, $"Your vote has been removed, {e.Command.ChatMessage.DisplayName}. Current total: {timerVote.GetVotes()}/{timerVote.GetSucceed()}");
                    }
                    else
                    {
                        client.SendMessage(e.Command.ChatMessage.Channel, $"Error: something fishy happpened :/");
                    }
                    break;
                case "tally":
                    if (CheckNull())
                    {
                        client.SendMessage(e.Command.ChatMessage.Channel, "No vote has been initialized :/");
                        break;
                    }
                    if (!CheckActive())
                    {
                        client.SendMessage(e.Command.ChatMessage.Channel, "No vote is currently active :/");
                        break;
                    }
                    if (timerVote.GetActive())
                    {
                        if (timerVote.GetTitle() != "")
                            client.SendMessage(e.Command.ChatMessage.Channel, $"Current total for '{timerVote.GetTitle()}' vote: {timerVote.GetVotes()}/{timerVote.GetSucceed()}.");
                        else
                            client.SendMessage(e.Command.ChatMessage.Channel, $"Current total: {timerVote.GetVotes()}/{timerVote.GetSucceed()}.");
                    }
                    else
                    {
                        client.SendMessage(e.Command.ChatMessage.Channel, $"There are no votes currently ongoing.");
                    }
                    break;
                /*case "help":
                    string help = "!initvote <#votes> <title> - Start a new vote [Mod+ only].     "+
                                  "!vote - add to a current vote.     " +
                                  "!retract - remove your vote.     " +
                                  "!tally - get info of current vote.     ";
                    client.SendMessage(e.Command.ChatMessage.Channel, help);
                    break;*/
            }
        }

        private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            if (e.ChatMessage.Message.StartsWith("hi", StringComparison.InvariantCultureIgnoreCase))
            {
                client.SendMessage(e.ChatMessage.Channel, $"Hey there {e.ChatMessage.DisplayName}");
            }
        }

        private void Client_OnLog(object sender, OnLogArgs e)
        {
            //Console.WriteLine(e.Data);
        }
        private void Client_OnConnectionError(object sender, OnConnectionErrorArgs e)
        {
            Console.WriteLine($"Error! {e.Error}");
        }

        internal void Disconnect()
        {
            Console.WriteLine("Disconnecting");
        }

        private bool CheckNull()
        {
            return (timerVote == null);
        }

        private bool CheckActive()
        {
            return timerVote.GetActive();
        }
    }

}