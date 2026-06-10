using System;
using System.ComponentModel;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using OpenAI.Chat;
using DotNetEnv;
using Progress.Observability.Extensions.AI;

// Load environment variables from .env file
Env.Load("../../../../.env");

// Get Azure OpenAI configuration from environment variables
var azure_endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is required");
var azure_key = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY")
    ?? throw new InvalidOperationException("AZURE_OPENAI_KEY is required");
var model_id = Environment.GetEnvironmentVariable("AZURE_OPENAI_MODEL_ID") ?? "gpt-4o-mini";

try
{
    // INITIALIZE PROGRESS OBSERVABILITY
    ObservabilityTracer.Initialize(new ObservabilityOptions()
    {
        AppName = Environment.GetEnvironmentVariable("OBSERVABILITY_APP_NAME")
            ?? throw new InvalidOperationException("OBSERVABILITY_APP_NAME is required"),
        ApiKey = Environment.GetEnvironmentVariable("OBSERVABILITY_API_KEY")
            ?? throw new InvalidOperationException("OBSERVABILITY_API_KEY is required")
    });

    List<AITool> tools = [AIFunctionFactory.Create((Func<string>)GetRandomDestination)];

    // Create AI Agent with custom tool
    AIAgent agent = new AzureOpenAIClient(
            new Uri(azure_endpoint),
            new AzureKeyCredential(azure_key))
        .GetChatClient(model_id)
        .AsAIAgent(
            name: "TravelPlanAgent",
            instructions: "You are a helpful AI Agent that can help plan vacations for customers at random destinations",
            // ADD TOOLS OBSERVABILITY
            tools: tools.AddToolObservability()
        )
        .AsBuilder()
        // ADD AGENT OBSERVABILITY by using the default source name
        .UseOpenTelemetry(sourceName: "Progress.Observability.AgentMonitoring", configure: agent =>
        {
            // To send input/output/tools data to Progress Observability.
            // Set to false if you don't want to send potentially sensitive data.
            agent.EnableSensitiveData = true;
        })
        .Build();

    // Run agent with standard response
    Console.WriteLine("=== Travel Plan ===");
    Console.WriteLine(await agent.RunAsync("Plan me a day trip"));

    // Run agent with streaming response
    Console.WriteLine("\n=== Streaming Travel Plan ===");
    await foreach (var update in agent.RunStreamingAsync("Plan me a day trip"))
    {
        Console.Write(update);
    }
    Console.WriteLine();
}
finally
{
    ObservabilityTracer.Shutdown();
}

// Agent Tool: Random Destination Generator
[Description("Provides a random vacation destination for travel planning.")]
static string GetRandomDestination()
{
    var destinations = new List<string>
    {
        "Paris, France",
        "Tokyo, Japan",
        "New York City, USA",
        "Sydney, Australia",
        "Rome, Italy",
        "Barcelona, Spain",
        "Cape Town, South Africa",
        "Rio de Janeiro, Brazil",
        "Bangkok, Thailand",
        "Vancouver, Canada"
    };

    var random = new Random();
    return destinations[random.Next(destinations.Count)];
}