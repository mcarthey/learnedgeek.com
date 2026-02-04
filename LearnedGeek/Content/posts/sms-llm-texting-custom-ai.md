# Texting My Own AI: Building an SMS Interface to a Custom LLM in an Hour

It started with a simple question: "Can I text an AI instead of opening yet another app?"

Turns out, not only is it possible—it's surprisingly straightforward. And more importantly, it validates a genuinely compelling business model for consulting work.

Here's how I built a working SMS-to-LLM integration in about an hour, what I learned, and why this matters for anyone doing technical consulting.

## The Idea

I've been experimenting with [local LLMs](/blog/running-ai-on-your-own-computer) lately, specifically fine-tuning models and running them through Ollama. But constantly switching between apps felt cumbersome. I wanted something more natural. Something I already do dozens of times a day: texting.

Then it clicked: What if clients could text questions about their projects and get answers from an AI trained on their documentation, codebase, and requirements? No portal to log into, no app to download. Just text a number.

The concept had three compelling advantages:

1. **Familiar interface** - Everyone texts
2. **Privacy** - Run locally, client data never leaves your infrastructure
3. **Customization** - Fine-tune on client-specific knowledge

Time to prove it could work.

## The Stack

I kept it simple for the POC:

- **Twilio** for SMS handling (free trial)
- **ngrok** to expose my local dev machine
- **.NET 10 minimal API** as the webhook endpoint
- **Ollama** running my custom model locally
- A vanity number that spells out something memorable

The architecture is dead simple:

```
Phone → Twilio → ngrok → .NET API → Ollama (local LLM) → Response
```

## Building It

### Step 1: Get a Number

I wanted something memorable for business use, so I searched Twilio's inventory for vanity numbers. Found one that worked perfectly.

Cost: $1.15/month.

### Step 2: Set Up Ollama

Ollama was already running as a background service on my machine. Verified it worked:

```bash
curl http://localhost:11434/api/generate \
  -d '{"model":"my-custom-model","prompt":"test","stream":false}'
```

Got a response. Good to go.

### Step 3: Build the Webhook

Created a minimal .NET API with a single POST endpoint that:

1. Receives SMS from Twilio
2. Extracts the message body
3. Calls Ollama's API
4. Wraps response in TwiML XML
5. Returns to Twilio

The entire `Program.cs`:

```csharp
using Twilio.TwiML;
using Twilio.TwiML.Messaging;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var ollamaUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
var ollamaModel = builder.Configuration["Ollama:Model"] ?? "llama3";

app.MapPost("/sms", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var incomingMessage = form["Body"].ToString();
    var fromNumber = form["From"].ToString();

    app.Logger.LogInformation("Received from {Number}: {Message}",
        fromNumber, incomingMessage);

    var ollamaResponse = await CallOllama(incomingMessage, ollamaUrl, ollamaModel);

    var response = new MessagingResponse();
    response.Message(ollamaResponse);

    return Results.Content(response.ToString(), "application/xml");
});

async Task<string> CallOllama(string userMessage, string baseUrl, string model)
{
    try
    {
        using var client = new HttpClient();
        var payload = new { model, prompt = userMessage, stream = false };

        var response = await client.PostAsJsonAsync(
            $"{baseUrl}/api/generate", payload);

        response.EnsureSuccessStatusCode();
        var result = await response.Content
            .ReadFromJsonAsync<OllamaResponse>();
        return result?.response ?? "Sorry, no response from LLM";
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error calling Ollama");
        return "Sorry, I'm having trouble processing that right now.";
    }
}

record OllamaResponse(string response);

app.Run();
```

That's it. ~40 lines of code.

### Step 4: Expose It

Started ngrok to tunnel to my local machine:

```bash
ngrok http 5036
```

Got a public URL with one of ngrok's delightfully random subdomains.

### Step 5: Wire It Up

Configured Twilio's webhook:
- Messaging → "A message comes in"
- URL: `https://[ngrok-subdomain].ngrok-free.dev/sms`
- Method: HTTP POST

### Step 6: Test It

Sent a text to my new number: "Hello!"

Watched my console light up with the incoming message. Checked ngrok's web inspector at `http://127.0.0.1:4040`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Response>
  <Message>Hello! How can I help you today?</Message>
</Response>
```

**IT WORKED.**

## The Catch (And It's Minor)

The SMS didn't actually deliver to my phone—yet. Twilio's message log showed "Undelivered" with an error about A2P 10DLC registration.

Turns out all US long-code SMS now requires registration (anti-spam regulation). Takes 1-3 days to complete. But the important part: **the entire technical stack works perfectly.**

The webhook integration, Ollama processing, TwiML generation—all flawless. I verified the response in ngrok's inspector and Twilio's logs. Once registration clears, the SMS will deliver.

## What This Proves

This one-hour POC validates several things:

### 1. The Tech Works

- Webhooks + ngrok = instant deployment-free testing
- Ollama's API is dead simple to integrate
- TwiML responses are easier than Twilio's SDK for basic replies
- Custom fine-tuned models work great over SMS

### 2. The Business Model Works

Imagine offering this to clients:

> "Text this number with questions about your project. You'll get instant answers from an AI trained on your documentation, codebase, and requirements. No app to install, no portal to log into."

**Why clients would pay for this:**

- **Convenience** - Text is universal and familiar
- **Privacy** - Their data never leaves your infrastructure
- **Accuracy** - Fine-tuned on their specific content
- **Availability** - 24/7 access without human bottlenecks

**Why it's profitable for consultants:**

- No per-token API costs eating margins
- One-time fine-tuning, infinite usage
- Can charge recurring monthly fee per client
- Positions you as cutting-edge

### 3. Custom Models Are the Secret Sauce

Using a generic LLM wouldn't work. The value comes from training on client-specific knowledge:

- Their architectural decisions and why they were made
- Their coding patterns and conventions
- Their business requirements and constraints
- Their internal documentation

You're not selling "AI access"—you're selling institutional knowledge preservation and retrieval.

## The Path to Production

For this to be client-ready:

**Immediate:**
- Complete A2P 10DLC registration
- Add conversation memory (track context per phone number)
- Implement rate limiting
- Proper error handling

**Business Features:**
- RAG integration (vector database + document embeddings)
- Fine-tuning pipeline for new clients
- Multi-tenant architecture
- Usage tracking and billing

**Production:**
- Deploy to cloud VM with GPU
- Proper logging and monitoring
- Secrets management (environment variables, not config files)
- Backup and disaster recovery

None of this is complicated. It's standard production hardening.

## What I Learned

### Technical
- Webhooks are perfect for POCs—no deployment needed
- ngrok's paid plan offers persistent URLs (worth it for business)
- A2P 10DLC is now mandatory for all US SMS
- Custom model + RAG is way more valuable than generic AI

### Business
- SMS interfaces lower adoption friction dramatically
- Local LLMs = competitive advantage (privacy + cost)
- Fine-tuning is the moat, not the infrastructure
- This is a legit consulting offering, not just a toy

### Meta
- Best POCs are ones you're excited about
- An hour of focused work beats weeks of planning
- Build the simple version first, add complexity only when needed

## Next Steps

I'm keeping this number long-term—the vanity number is too perfect for branding. Once A2P registration completes, I'll add conversation memory and start testing with real use cases.

The bigger question: Is this a service worth offering to clients? The answer is increasingly looking like "yes."

If you're a consultant with technical clients, this is worth exploring. The tech is accessible, the value proposition is clear, and the competitive moat (fine-tuned models) is real.

---

*Related: [Running AI On Your Own Computer](/blog/running-ai-on-your-own-computer) covers getting started with Ollama and local LLMs.*
