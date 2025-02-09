using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KokoroTray
{
    public class DictionaryManager
    {
        private HashSet<string> ignoreWords;
        private HashSet<string> bannedPhrases;
        private Dictionary<string, string> replacements;
        private readonly string dictionaryPath;
        private static readonly string[] DictionaryFiles = { "ignore.dict", "banned.dict", "replace.dict" };

        public DictionaryManager(string dictionaryPath)
        {
            this.dictionaryPath = dictionaryPath;
            ignoreWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bannedPhrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            LoadDictionaries();
        }

        private void LoadDictionaries()
        {
            // Create dictionary directory if it doesn't exist
            Directory.CreateDirectory(dictionaryPath);

            // Create dictionary files if they don't exist
            foreach (var file in DictionaryFiles)
            {
                var filePath = Path.Combine(dictionaryPath, file);
                if (!File.Exists(filePath))
                {
                    File.Create(filePath).Dispose();
                    Logger.Info($"Created dictionary file: {filePath}");
                }
            }

            // Load ignore dictionary
            var ignorePath = Path.Combine(dictionaryPath, "ignore.dict");
            if (File.Exists(ignorePath))
            {
                ignoreWords = new HashSet<string>(
                    File.ReadAllLines(ignorePath)
                        .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                        .Select(line => line.Trim()),
                    StringComparer.OrdinalIgnoreCase
                );
                Logger.Info($"Loaded {ignoreWords.Count} ignore words");
            }

            // Load banned dictionary
            var bannedPath = Path.Combine(dictionaryPath, "banned.dict");
            if (File.Exists(bannedPath))
            {
                bannedPhrases = new HashSet<string>(
                    File.ReadAllLines(bannedPath)
                        .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                        .Select(line => line.Trim()),
                    StringComparer.OrdinalIgnoreCase
                );
                Logger.Info($"Loaded {bannedPhrases.Count} banned phrases");
            }

            // Load replacements dictionary
            var replacePath = Path.Combine(dictionaryPath, "replace.dict");
            if (File.Exists(replacePath))
            {
                replacements = File.ReadAllLines(replacePath)
                    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                    .Select(line => line.Trim())
                    .Where(line => line.Contains('='))
                    .ToDictionary(
                        line => line.Split('=')[0].Trim(),
                        line => line.Split('=')[1].Trim(),
                        StringComparer.OrdinalIgnoreCase
                    );
                Logger.Info($"Loaded {replacements.Count} replacements");
            }
        }

        public string ProcessText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            Logger.Info($"Processing text with dictionaries - Length: {text.Length} chars");
            Logger.Info($"Original text: {text}");
            Logger.Info($"Active replacements: {string.Join(", ", replacements.Select(r => $"{r.Key}={r.Value}"))}");

            // Split text into lines and process each line
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var processedLines = new List<string>();

            foreach (var line in lines)
            {
                Logger.Info($"Processing line: {line}");
                
                // Skip lines containing banned phrases
                if (bannedPhrases.Any(banned => line.Contains(banned, StringComparison.OrdinalIgnoreCase)))
                {
                    Logger.Info($"Skipping line due to banned phrase: {line}");
                    continue;
                }

                var processedLine = line;

                // Apply replacements
                foreach (var replacement in replacements)
                {
                    if (processedLine.Contains(replacement.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Info($"Applying replacement: {replacement.Key} -> {replacement.Value}");
                        processedLine = processedLine.Replace(replacement.Key, replacement.Value, StringComparison.OrdinalIgnoreCase);
                    }
                }

                // Remove ignored words
                foreach (var ignored in ignoreWords)
                {
                    if (processedLine.Contains(ignored, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Info($"Removing ignored word: {ignored}");
                        processedLine = processedLine.Replace(ignored, "", StringComparison.OrdinalIgnoreCase);
                    }
                }

                // Only add non-empty lines after processing
                if (!string.IsNullOrWhiteSpace(processedLine))
                {
                    Logger.Info($"Processed line result: {processedLine}");
                    processedLines.Add(processedLine);
                }
            }

            var result = string.Join(Environment.NewLine, processedLines);
            Logger.Info($"Text processing complete - Final length: {result.Length} chars");
            Logger.Info($"Final processed text: {result}");
            return result;
        }

        public void ReloadDictionaries()
        {
            Logger.Info("Reloading dictionaries");
            LoadDictionaries();
        }
    }
} 