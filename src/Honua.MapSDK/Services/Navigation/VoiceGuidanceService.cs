// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Honua.MapSDK.Models.Navigation;
using Microsoft.JSInterop;

namespace Honua.MapSDK.Services.Navigation;

/// <summary>
/// Service for voice guidance using Web Speech API
/// </summary>
public class VoiceGuidanceService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _jsModule;
    private readonly Queue<VoiceInstruction> _instructionQueue;
    private bool _isSpeaking = false;
    private NavigationOptions? _options;

    public VoiceGuidanceService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        _instructionQueue = new Queue<VoiceInstruction>();
    }

    /// <summary>
    /// Initialize voice guidance
    /// </summary>
    public async Task InitializeAsync(NavigationOptions? options = null)
    {
        _options = options ?? new NavigationOptions();

        try
        {
            _jsModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                "./_content/Honua.MapSDK/js/voice-guidance.js"
            );

            await _jsModule.InvokeVoidAsync("initialize", new
            {
                language = _options.VoiceLanguage,
                volume = _options.VoiceVolume,
                rate = _options.VoiceSpeechRate,
                voice = _options.VoiceName
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to initialize voice guidance: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Speak a text instruction
    /// </summary>
    /// <param name="text">Text to speak</param>
    /// <param name="priority">Priority level (higher = more important)</param>
    public async Task SpeakAsync(string text, int priority = 0)
    {
        if (_jsModule == null)
        {
            throw new InvalidOperationException("Voice guidance not initialized");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            await _jsModule.InvokeVoidAsync("speak", text, new
            {
                volume = _options?.VoiceVolume ?? 1.0,
                rate = _options?.VoiceSpeechRate ?? 1.0,
                priority = priority
            });

            _isSpeaking = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to speak instruction: {ex.Message}");
        }
    }

    /// <summary>
    /// Speak a voice instruction
    /// </summary>
    public async Task SpeakInstructionAsync(VoiceInstruction instruction)
    {
        if (instruction == null)
        {
            return;
        }

        // Use SSML if available, otherwise use plain text
        var text = !string.IsNullOrEmpty(instruction.SsmlText)
            ? instruction.SsmlText
            : instruction.Text;

        await SpeakAsync(text, instruction.Priority);
    }

    /// <summary>
    /// Queue multiple instructions
    /// </summary>
    public async Task QueueInstructionsAsync(IEnumerable<VoiceInstruction> instructions)
    {
        if (instructions == null)
        {
            return;
        }

        foreach (var instruction in instructions.OrderByDescending(i => i.Priority))
        {
            _instructionQueue.Enqueue(instruction);
        }

        await ProcessQueueAsync();
    }

    /// <summary>
    /// Stop current speech
    /// </summary>
    public async Task StopAsync()
    {
        if (_jsModule != null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("stop");
                _isSpeaking = false;
                _instructionQueue.Clear();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to stop voice guidance: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Pause voice guidance
    /// </summary>
    public async Task PauseAsync()
    {
        if (_jsModule != null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("pause");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to pause voice guidance: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Resume voice guidance
    /// </summary>
    public async Task ResumeAsync()
    {
        if (_jsModule != null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("resume");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to resume voice guidance: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Set voice volume
    /// </summary>
    public async Task SetVolumeAsync(double volume)
    {
        if (_jsModule != null && _options != null)
        {
            _options.VoiceVolume = Math.Clamp(volume, 0.0, 1.0);
            try
            {
                await _jsModule.InvokeVoidAsync("setVolume", _options.VoiceVolume);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to set volume: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Set speech rate
    /// </summary>
    public async Task SetSpeechRateAsync(double rate)
    {
        if (_jsModule != null && _options != null)
        {
            _options.VoiceSpeechRate = Math.Clamp(rate, 0.5, 2.0);
            try
            {
                await _jsModule.InvokeVoidAsync("setRate", _options.VoiceSpeechRate);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to set speech rate: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Set voice language
    /// </summary>
    public async Task SetLanguageAsync(string language)
    {
        if (_jsModule != null && _options != null)
        {
            _options.VoiceLanguage = language;
            try
            {
                await _jsModule.InvokeVoidAsync("setLanguage", language);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to set language: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Get available voices
    /// </summary>
    public async Task<List<VoiceInfo>> GetAvailableVoicesAsync()
    {
        if (_jsModule == null)
        {
            return new List<VoiceInfo>();
        }

        try
        {
            var voices = await _jsModule.InvokeAsync<List<VoiceInfo>>("getVoices");
            return voices ?? new List<VoiceInfo>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to get voices: {ex.Message}");
            return new List<VoiceInfo>();
        }
    }

    /// <summary>
    /// Set the voice to use
    /// </summary>
    public async Task SetVoiceAsync(string voiceName)
    {
        if (_jsModule != null && _options != null)
        {
            _options.VoiceName = voiceName;
            try
            {
                await _jsModule.InvokeVoidAsync("setVoice", voiceName);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to set voice: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Check if speech synthesis is supported
    /// </summary>
    public async Task<bool> IsSupportedAsync()
    {
        if (_jsModule == null)
        {
            return false;
        }

        try
        {
            return await _jsModule.InvokeAsync<bool>("isSupported");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if currently speaking
    /// </summary>
    public bool IsSpeaking => _isSpeaking;

    /// <summary>
    /// Process instruction queue
    /// </summary>
    private async Task ProcessQueueAsync()
    {
        if (_isSpeaking || _instructionQueue.Count == 0)
        {
            return;
        }

        var instruction = _instructionQueue.Dequeue();
        await SpeakInstructionAsync(instruction);

        // Continue processing queue after speech completes
        // Note: In a real implementation, you'd want to listen for speech end events
        _isSpeaking = false;
        if (_instructionQueue.Count > 0)
        {
            await ProcessQueueAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_jsModule != null)
        {
            try
            {
                await StopAsync();
                await _jsModule.DisposeAsync();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Information about an available voice
/// </summary>
public class VoiceInfo
{
    /// <summary>
    /// Voice name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Voice language code
    /// </summary>
    public string Lang { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a local voice (vs cloud)
    /// </summary>
    public bool LocalService { get; set; }

    /// <summary>
    /// Whether this is the default voice
    /// </summary>
    public bool Default { get; set; }

    /// <summary>
    /// Voice URI
    /// </summary>
    public string? VoiceURI { get; set; }
}
