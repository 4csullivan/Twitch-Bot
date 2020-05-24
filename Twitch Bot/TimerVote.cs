using System;
using System.Collections.Generic;
using TwitchLib.Api.Helix.Models.Tags;

namespace Twitch_Bot
{
    internal class TimerVote
    {
        private int votes;
        private int toSucceed;
        private bool isActive;
        private List<string> entries;
        private string title;
        public TimerVote()
        {
            this.votes = 0;
            this.toSucceed = 0;
            this.isActive = true;
            this.entries = new List<string>();
            this.title = "";
        }

        public TimerVote(int succeed, string title)
        {
            this.isActive = true;
            this.toSucceed = succeed;
            this.title = title;
            entries = new List<string>();
        }
        public TimerVote(int succeed)
        {
            this.isActive = true;
            this.toSucceed = succeed;
            this.title = "";
            entries = new List<string>();
        }

        public bool AddVote(string entry)
        {
            if (entries.Contains(entry)) return false;
            entries.Add(entry);
            this.votes++;
            if (this.CheckVotes())
                this.isActive = false;
            return true;
        }

        public bool SubtractVote(string entry)
        {
            if (!entries.Contains(entry)) return false;
            this.votes--;
            entries.Remove(entry);
            return true;
        }

        public bool CheckVotes()
        {
            return (this.votes >= this.toSucceed);
        }

        public int GetVotes()
        {
            return this.votes;
        }

        public int GetSucceed()
        {
            return this.toSucceed;
        }

        public void SetSucceed(int succeed)
        {
            this.toSucceed = succeed;
        }

        public bool GetActive()
        {
            return this.isActive;
        }

        public string GetTitle()
        {
            return this.title;
        }

        public void SetActive(bool active)
        {
            this.isActive = active;
        }
    }
}