﻿namespace Penguin.Persistence.Database.Objects
{
    internal class AsyncSqlCommand
    {
        public int CommandNumber { get; set; }

        public double Progress { get; set; }

        public string Text { get; set; }

        public AsyncSqlCommand(string text, double progress, int commandNumber)
        {
            Text = text;
            Progress = progress;
            CommandNumber = commandNumber;
        }
    }
}