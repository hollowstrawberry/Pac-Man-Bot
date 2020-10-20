using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PacManBot.Constants;

namespace PacManBot.Services
{
    public class WordService
    {
        private readonly LoggingService _log;

        public IReadOnlyList<string> Words { get; }

        public WordService(LoggingService log)
        {
            _log = log;

            try
            {
                Words = File.ReadAllLines(Files.Words);
                _log.Info($"Loaded {Words.Count} words");
            }
            catch (Exception e)
            {
                _log.Fatal($"Could not load words file - {e}");
            }
        }
    }
}
