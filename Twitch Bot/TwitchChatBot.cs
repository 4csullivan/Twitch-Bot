using System;
using System.Collections;
using System.Collections.Generic;
using System.Resources;
using System.Security.Authentication.ExtendedProtection.Configuration;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace Twitch_Bot
{
    internal class TwitchChatBot
    {
        private const string CMD_RECORD_SET = "setrecord";
        private const string CMD_RECORD_LIST = "recordlist";
        private const string CMD_RECORD_REMOVE = "removerecord";
        private const string CMD_RECORD = "record";
        private const string CMD_VOTE_INIT = "initvote";
        private const string CMD_VOTE_CANCEL = "cancel";
        private const string CMD_VOTE = "vote";
        private const string CMD_VOTE_RETRACT = "retract";
        private const string CMD_VOTE_TALLY = "tally";
        private const string CMD_VOTE_HELP = "votehelp";
        private const string RESX_LOCATION = @".\RecordValues.resx";
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
            CheckVotes(e);
            CheckRecords(e);
            CheckHelp(e);
        }

        private void CheckHelp(OnChatCommandReceivedArgs e)
        {
            switch (e.Command.CommandText)
            {
                case "help":
                    if (e.Command.ArgumentsAsList.Count <= 0)
                    {
                        client.SendMessage(e.Command.ChatMessage.Channel, $"What would you like help with? !help <category> | Current categories: Records, Votes");
                    }
                    else
                    {
                        string helpArg = e.Command.ArgumentsAsList[0].ToString().ToLower();
                        switch (helpArg)
                        {
                            case "records":
                                client.SendMessage(e.Command.ChatMessage.Channel, $"!{CMD_RECORD_SET} <name> <value> [Mod Only] // !{CMD_RECORD_REMOVE} <name> [Mod Only] // !{CMD_RECORD} <name> // !{CMD_RECORD_LIST}");
                                break;
                            case "votes":
                                client.SendMessage(e.Command.ChatMessage.Channel, $"!{CMD_VOTE_INIT} <name> <#votes> [Mod Only] // !{CMD_VOTE_CANCEL} [Mod Only] // !{CMD_VOTE} // !{CMD_VOTE_RETRACT} // !{CMD_VOTE_TALLY}");
                                break;
                        }
                    }

                    break;
            }
        }

        private void CheckRecords(OnChatCommandReceivedArgs e)
        {
            ResXResourceReader recordReader = new ResXResourceReader(RESX_LOCATION);
            Dictionary<string, float> recordDict = new Dictionary<string, float>();

            switch (e.Command.CommandText)
            {
                case CMD_RECORD_SET:
                    string name = "";
                    if (CheckModStatus(e))
                    {
                        if (e.Command.ArgumentsAsList.Count <= 0)
                        {
                            client.SendMessage(e.Command.ChatMessage.Channel, ErrorMessage("Missing arguments: <name> <value>"));
                            break;
                        }
                        if (e.Command.ArgumentsAsList.Count < 2)
                        {
                            client.SendMessage(e.Command.ChatMessage.Channel, ErrorMessage("Requires 2 arguments: <name> <value>"));
                            break;
                        }
                        name = e.Command.ArgumentsAsList[0].ToString();
                        float value;
                        bool validInput = float.TryParse(e.Command.ArgumentsAsList[1], out value);
                        if (!validInput)
                        {
                            client.SendMessage(e.Command.ChatMessage.Channel, ErrorMessage("Please enter a valid number"));
                            break;
                        }


                        foreach (DictionaryEntry entry in recordReader)
                            recordDict.Add(entry.Key.ToString().ToLower(), (float)entry.Value);

                        if (recordDict.ContainsKey(name.ToLower()))
                        {
                            if (recordDict[name.ToLower()] < value)
                            {
                                recordDict[name.ToLower()] = value;
                                client.SendMessage(e.Command.ChatMessage.Channel, $"Record updated! {name.ToLower()} updated from {recordDict[name.ToLower()]} to {value}");
                            }
                            else
                            {
                                client.SendMessage(e.Command.ChatMessage.Channel, ErrorMessage($"The value for record {name.ToLower()} is higher! Current value: {recordDict[name.ToLower()]}"));
                                break;
                            }
                        }
                        else
                        {
                            client.SendMessage(e.Command.ChatMessage.Channel, $"New Record set! {name.ToLower()} set to: {value}");
                            recordDict.Add(name.ToLower(), value);
                        }

                        using (ResXResourceWriter recordWriter = new ResXResourceWriter(RESX_LOCATION))
                        {
                            foreach (KeyValuePair<string, float> entry in recordDict)
                            {
                                recordWriter.AddResource(entry.Key.ToString(), entry.Value);
                            }
                        }
                    }
                    break;
                case CMD_RECORD_LIST:
                    List<string> records = new List<string>();
                    foreach (DictionaryEntry entry in recordReader)
                    {
                        records.Add(entry.Key.ToString());
                    }
                    string recordString = string.Join(", ", records.ToArray());
                    client.SendMessage(e.Command.ChatMessage.Channel, $"The current records are: {recordString}");
                    break;
                case CMD_RECORD_REMOVE:
                    foreach (DictionaryEntry entry in recordReader)
                        recordDict.Add(entry.Key.ToString().ToLower(), (float)entry.Value);
                    string record = e.Command.ArgumentsAsList[0].ToString().ToLower();
                    if (recordDict.ContainsKey(record))
                    {
                        recordDict.Remove(record);
                        client.SendMessage(e.Command.ChatMessage.Channel, $"The record {record} has been removed.");
                    }
                    else
                    {
                        client.SendMessage(e.Command.ChatMessage.Channel, ErrorMessage($"{record} does not currently exist"));
                        break;
                    }
                    using (ResXResourceWriter recordWriter = new ResXResourceWriter(RESX_LOCATION))
                    {
                        foreach (KeyValuePair<string, float> entry in recordDict)
                        {
                            recordWriter.AddResource(entry.Key.ToString(), entry.Value);
                        }
                    }
                    break;
                case CMD_RECORD:
                    if (e.Command.ArgumentsAsList.Count <= 0)
                    {
                        break;
                    }
                    string _record = e.Command.ArgumentsAsList[0];
                    Object result;
                    using (ResXResourceSet recordSet = new ResXResourceSet(RESX_LOCATION))
                    {
                        result = recordSet.GetObject(_record);
                    }
                    if (result == null)
                    {
                        client.SendMessage(e.Command.ChatMessage.Channel, ErrorMessage($"No record for {_record} exists"));
                    }
                    else
                    {
                        client.SendMessage(e.Command.ChatMessage.Channel, $"The record for {_record} is: {result}");
                    }
                    break;
            }

            recordReader.Close();
        }


        private bool CheckModStatus(OnChatCommandReceivedArgs e)
        {
            return e.Command.ChatMessage.IsModerator || e.Command.ChatMessage.IsBroadcaster;
        }

        private void CheckVotes(OnChatCommandReceivedArgs e)
        {
            switch (e.Command.CommandText)
            {
                case CMD_VOTE_INIT:
                    //client.SendMessage(e.Command.ChatMessage.Channel, "yup");
                    if ((e.Command.ChatMessage.IsModerator || e.Command.ChatMessage.IsBroadcaster))
                    {
                        if (!CheckNull())
                        {
                            if (timerVote.GetActive())
                            {
                                client.SendMessage(e.Command.ChatMessage.Channel, "There is already a vote active!");
                                break;
                            }
                        }
                        if (e.Command.ArgumentsAsList.Count <= 0)
                        {
                            client.SendMessage(e.Command.ChatMessage.Channel, "Error: no minimum votes inputted to succeed :/");
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
                            client.SendMessage(e.Command.ChatMessage.Channel, $"Error: unknown number :/");
                        }
                    }
                    break;
                case CMD_VOTE_CANCEL:
                    if ((e.Command.ChatMessage.IsModerator || e.Command.ChatMessage.IsBroadcaster))
                    {
                        timerVote.SetActive(false);

                        string voteTitle = timerVote.GetTitle() != "" ? $" for '{timerVote.GetTitle()}'" : "";
                        client.SendMessage(e.Command.ChatMessage.Channel, $"The vote{voteTitle} has been cancelled.");
                    }
                    break;
                case CMD_VOTE:
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
                case CMD_VOTE_RETRACT:
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
                case CMD_VOTE_TALLY:
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
            }
        }

        private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            //if (e.ChatMessage.Message.StartsWith("hi", StringComparison.InvariantCultureIgnoreCase))
            //{
            //    client.SendMessage(e.ChatMessage.Channel, $"Hey there {e.ChatMessage.DisplayName}");
            //}
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
        private string ErrorMessage(string body)
        {
            return $"Error! {body} :/";
        }
    }

}