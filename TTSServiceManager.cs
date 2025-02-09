using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using KokoroSharp;
using KokoroSharp.Core;
using Microsoft.ML.OnnxRuntime;
using KokoroSharp.Processing;
using NAudio.Wave;
using System.Net.Http;

namespace KokoroTray
{
    public class TTSServiceManager : IDisposable
    {
        internal KokoroTTS tts;
        private bool isInitialized = false;
        private bool isPlaying = false;
        private bool isPaused = false;
        private const int MaxRetries = 3;
        private const string MODEL_FILENAME = "kokoro.onnx";
        private const float DefaultTTSSpeed = 1.0f;
        private float currentSpeed = 1.0f;

        public bool IsPlaying => isPlaying;
        public bool IsPaused => isPaused;

        public TTSServiceManager(string modelPath, string voicesPath)
        {
            Logger.Info($"Initializing TTSServiceManager with modelPath: {modelPath}, voicesPath: {voicesPath}");
            var modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
            Directory.CreateDirectory(modelDir);
            Directory.CreateDirectory(voicesPath);

            // Set up the KokoroSharp logger to use our logger
            KokoroLogger.SetLogger(new KokoroTrayLogger());

            var modelFilePath = Path.Combine(modelDir, MODEL_FILENAME);
            if (!File.Exists(modelFilePath))
            {
                Logger.Info($"Model file not found at {modelFilePath}, downloading...");
                DownloadModel(modelPath, modelFilePath);
            }

            tts = new KokoroTTS(modelFilePath);
            Logger.Info($"Loading voices from path: {voicesPath}");
            KokoroVoiceManager.LoadVoicesFromPath(voicesPath);
            Logger.Info($"Loaded {KokoroVoiceManager.Voices.Count} voices");
            isInitialized = true;
        }

        private async Task EnsureTTSServiceInitialized()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("TTSServiceManager is not initialized");
            }
        }

        private void UpdateTrayMenuState()
        {
            // This method will be called by the TrayApplication
        }

        public void SetSpeed(float speed)
        {
            Logger.Info($"Setting TTS speed - Previous: {currentSpeed}x, New: {speed}x");
            currentSpeed = speed;
            Logger.Info($"TTS speed has been set to: {currentSpeed}x");
        }

        private void DownloadModel(string modelPath, string modelFilePath)
        {
            using var client = new HttpClient();
            var retryCount = 0;
            while (retryCount < MaxRetries)
            {
                try
                {
                    var bytes = client.GetByteArrayAsync(modelPath).Result;
                    File.WriteAllBytes(modelFilePath, bytes);
                    break;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    if (retryCount == MaxRetries)
                    {
                        throw new Exception($"Failed to download model after {MaxRetries} attempts: {ex.Message}");
                    }
                    Thread.Sleep(1000 * retryCount); // Exponential backoff
                }
            }
        }

        public async Task<MemoryStream> GenerateAudioAsync(string text, string voiceId)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("TTSServiceManager is not initialized");
            }

            try
            {
                Logger.Info($"Starting audio generation:");
                Logger.Info($"  - Text length: {text?.Length ?? 0} characters");
                Logger.Info($"  - Voice ID: {voiceId}");
                Logger.Info($"  - Current speed setting: {currentSpeed}x");

                var voice = KokoroVoiceManager.GetVoice(voiceId);
                Logger.Info($"Found voice: {voice.Name} (Language: {voice.Language}, Gender: {voice.Gender})");
                
                var memoryStream = new MemoryStream();
                var completionSource = new TaskCompletionSource<bool>();

                // Create a custom playback handler to capture the audio
                Action<float[]> playbackHandler = (samples) =>
                {
                    Logger.Debug($"Processing audio samples:");
                    Logger.Debug($"  - Sample count: {samples.Length}");
                    Logger.Debug($"  - Current speed setting: {currentSpeed}x");
                    
                    try
                    {
                        // Convert float samples to 16-bit PCM
                        var byteArray = new byte[samples.Length * 2];
                        for (int i = 0; i < samples.Length; i++)
                        {
                            var value = (short)(samples[i] * short.MaxValue);
                            byteArray[i * 2] = (byte)(value & 0xFF);
                            byteArray[i * 2 + 1] = (byte)(value >> 8);
                        }

                        // Write to memory stream
                        memoryStream.Write(byteArray, 0, byteArray.Length);
                        Logger.Debug($"  - Wrote {byteArray.Length} bytes to stream");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error processing audio samples", ex);
                        completionSource.TrySetException(ex);
                    }
                };

                // Tokenize and create job
                var tokens = Tokenizer.Tokenize(text.Trim(), voice.GetLangCode(), true);
                Logger.Info($"  - Tokenized text into {tokens.Length} tokens");

                // Create pipeline config with speed
                var config = new DefaultSegmentationConfig();
                var pipelineConfig = new KokoroTTSPipelineConfig(config);
                pipelineConfig.Speed = currentSpeed;
                
                // Create segments and job
                var segments = pipelineConfig.SegmentationFunc(tokens);
                var steps = segments.Select(segment => (Tokens: segment, VoiceStyle: voice.Features, Speed: currentSpeed)).ToList();
                var job = KokoroJob.Create(steps, playbackHandler);
                var handle = tts.EnqueueJob(job);

                // Set up event handlers
                tts.OnSpeechCompleted += (packet) => completionSource.TrySetResult(true);
                tts.OnSpeechCanceled += (packet) => completionSource.TrySetResult(false);

                await completionSource.Task;
                
                // Reset stream position to beginning
                memoryStream.Position = 0;
                
                Logger.Info($"Audio generation completed:");
                Logger.Info($"  - Stream length: {memoryStream.Length} bytes");
                Logger.Info($"  - Stream position: {memoryStream.Position}");
                Logger.Info($"  - Approximate duration: {memoryStream.Length / 44100.0:F2} seconds");
                Logger.Info($"  - Final speed setting used: {currentSpeed}x");
                
                return memoryStream;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error generating audio (speed: {currentSpeed}x)", ex);
                throw;
            }
        }

        public void SetPlaybackState(bool playing, bool paused)
        {
            Logger.Info($"Setting playback state - Playing: {playing}, Paused: {paused}");
            isPlaying = playing;
            isPaused = paused;
            Logger.Info($"New playback state - isPlaying: {isPlaying}, isPaused: {isPaused}");
        }

        public void PausePlayback()
        {
            if (isPlaying && !isPaused)
            {
                Logger.Info($"Attempting to pause TTS playback - Current state: isPlaying={isPlaying}, isPaused={isPaused}");
                tts?.PausePlayback();
                SetPlaybackState(true, true);  // Update state atomically
                Logger.Info($"TTS playback paused - New state: isPlaying={isPlaying}, isPaused={isPaused}");
            }
            else
            {
                Logger.Info($"Cannot pause TTS playback - Current state: isPlaying={isPlaying}, isPaused={isPaused}");
            }
        }

        public void ResumePlayback()
        {
            if (isPlaying && isPaused)
            {
                Logger.Info($"Attempting to resume TTS playback - Current state: isPlaying={isPlaying}, isPaused={isPaused}");
                tts?.ResumePlayback();
                SetPlaybackState(true, false);  // Update state atomically
                Logger.Info($"TTS playback resumed - New state: isPlaying={isPlaying}, isPaused={isPaused}");
            }
            else
            {
                Logger.Info($"Cannot resume TTS playback - Current state: isPlaying={isPlaying}, isPaused={isPaused}");
            }
        }

        public void TogglePauseResume()
        {
            Logger.Info($"Toggle pause/resume requested - Current state: isPlaying={isPlaying}, isPaused={isPaused}");
            if (isPlaying)  // Only toggle if we're actually playing
            {
                if (isPaused)
                {
                    ResumePlayback();
                }
                else
                {
                    PausePlayback();
                }
            }
            else
            {
                Logger.Info("Cannot toggle pause/resume - No active playback");
            }
            Logger.Info($"Toggle pause/resume completed - New state: isPlaying={isPlaying}, isPaused={isPaused}");
        }

        public async Task PlayTTSAsync(string text, string voice, float speed = DefaultTTSSpeed)
        {
            try
            {
                await EnsureTTSServiceInitialized();
                Logger.Info($"Starting audio generation for text with {text.Length} chars and approximately {text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).Length} sentences");
                Logger.Info($"Text content: {text}");
                Logger.Info($"Using speed: {speed}x");
                
                var voiceObj = KokoroVoiceManager.GetVoice(voice);
                
                // Create pipeline config with speed
                var config = new DefaultSegmentationConfig();
                var pipelineConfig = new KokoroTTSPipelineConfig(config);
                pipelineConfig.Speed = speed;  // Set the speed before calling SpeakFast
                currentSpeed = speed;  // Update the current speed
                
                // Use SpeakFast directly with the configured pipeline
                var handle = tts.SpeakFast(text, voiceObj, pipelineConfig);
                isPlaying = true;
                isPaused = false;  // Reset pause state when starting new playback
                UpdateTrayMenuState();
                
                // Wait for completion
                var completionSource = new TaskCompletionSource<bool>();
                handle.OnSpeechCompleted += (packet) => {
                    Logger.Info("Speech completed successfully");
                    completionSource.TrySetResult(true);
                };
                handle.OnSpeechCanceled += (packet) => {
                    Logger.Info("Speech was cancelled");
                    completionSource.TrySetResult(false);
                };
                
                await completionSource.Task;
                isPlaying = false;
                isPaused = false;
                UpdateTrayMenuState();
                Logger.Info("Playback completed");
            }
            catch (Exception ex)
            {
                Logger.Error("Error during TTS playback", ex);
                isPlaying = false;
                isPaused = false;
                UpdateTrayMenuState();
                throw;
            }
        }

        public void Dispose()
        {
            tts?.Dispose();
        }
    }
} 