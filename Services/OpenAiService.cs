using Azure;
using Azure.AI.OpenAI;
using Cosmos.Chat.GPT.Models;

namespace Cosmos.Chat.GPT.Services;

public class OpenAiService
{
    private readonly string _modelName = String.Empty;
    private readonly OpenAIClient _client;
    
    private readonly string _systemPrompt = @"
    You are an AI assistant that helps people find information.
    Provide concise answers that are polite and professional." + Environment.NewLine;
    
    private readonly string _summarizePrompt = @"
    Summarize this prompt in one or two words to use as a label in a button on a web page.
    Do not use any punctuation." + Environment.NewLine;
    
    public OpenAiService(string endpoint, string key, string modelName)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(modelName);
        ArgumentNullException.ThrowIfNullOrEmpty(endpoint);
        ArgumentNullException.ThrowIfNullOrEmpty(key);
        
        _modelName = modelName;
        Uri uri = new(endpoint);
        AzureKeyCredential credential = new(key);
        _client = new OpenAIClient(uri, credential);
    }

    public async Task<(string completionText, int completionTokens, int responseTokens)> GetChatCompletionAsync(string sessionId, string userPrompt)
    {
        ChatRequestUserMessage userMessage = new(userPrompt);
        ChatRequestSystemMessage systemMessage = new(_systemPrompt);
        ChatCompletionsOptions options = new()
        {
            Messages = {
                systemMessage,
                userMessage
            },
            User = sessionId,
            MaxTokens = 4000,
            Temperature = 0.3f,
            NucleusSamplingFactor = 0.5f,
            FrequencyPenalty = 0,
            PresencePenalty = 0,
            DeploymentName = _modelName
        };
        
        ChatCompletions completions = await _client.GetChatCompletionsAsync(options);
        return (
            response: completions.Choices[0].Message.Content,
            promptTokens: completions.Usage.PromptTokens,
            responseTokens: completions.Usage.CompletionTokens
        );
    }

    public async Task<string> SummarizeAsync(string sessionId, string conversationText)
    {
        ChatRequestSystemMessage systemMessage = new(_systemPrompt);
        ChatRequestUserMessage userMessage = new(conversationText);
        ChatCompletionsOptions options = new()
        {
            Messages = {
                systemMessage,
                userMessage
            },
            User = sessionId,
            MaxTokens = 200,
            Temperature = 0.0f,
            NucleusSamplingFactor = 1.0f,
            FrequencyPenalty = 0,
            PresencePenalty = 0,
            DeploymentName = _modelName
        };
        
        ChatCompletions completions = await _client.GetChatCompletionsAsync(options);
        return completions.Choices[0].Message.Content;
    }
}
