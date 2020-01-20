﻿namespace Penguin.Persistence.Database.Objects
{
    internal class AsyncSqlCommand
    {
        public int CommandNumber { get; set; }

        public decimal Progress { get; set; }

        public string Text { get; set; }

        public AsyncSqlCommand(string text, decimal progress, int commandNumber)
        {
            this.Text = text;
            this.Progress = progress;
            this.CommandNumber = commandNumber;
        }
    }
}