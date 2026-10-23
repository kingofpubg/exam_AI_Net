using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text;

[Route("api")]
[ApiController]
public class ShopController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public ShopController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
        _httpClient = new HttpClient();
        _apiKey = _configuration["ApiKey"] ?? _configuration["GeminiApiKey"] ?? "";
    }

    private async Task<string> CallGeminiAsync(string systemInstruction, string userPrompt)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
        
        var requestBody = new {
            system_instruction = new {
                parts = new[] { new { text = systemInstruction } }
            },
            contents = new[] {
                new { parts = new[] { new { text = userPrompt } } }
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content);
        var jsonResponse = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(jsonResponse);
        try {
            return doc.RootElement.GetProperty("candidates")[0]
                                  .GetProperty("content")
                                  .GetProperty("parts")[0]
                                  .GetProperty("text").GetString() ?? "";
        } catch {
            return jsonResponse;
        }
    }

    // 1. POST /api/products/{id}/generatedescription
    [HttpPost("products/{id}/generatedescription")]
    public async Task<IActionResult> GenerateDescription(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound(new { message = "Product not found" });

        var prompt = $"Product: {product.ProductName}, Category: {product.Category}, Features: {product.Features}";
        var systemInstruction = "You are a professional copywriter, writes a description of at most 80 words based on the product name, category and features.";

        var textResponse = await CallGeminiAsync(systemInstruction, prompt);

        product.Description = textResponse;
        await _context.SaveChangesAsync();

        return Ok(new { product.ProductId, product.ProductName, product.Description });
    }

    // 2. POST /api/reviews/analyze
    [HttpPost("reviews/analyze")]
    public async Task<IActionResult> AnalyzeReview([FromBody] ReviewDto dto)
    {
        var prompt = $"Analyze this review: '{dto.Content}' with rating {dto.Rating}. Return ONLY JSON with fields: \"sentiment\" (Positive | Negative | Neutral), \"score\" (float 0.0-1.0), \"summary\" (short summary under 256 chars).";
        var systemInstruction = "You are an AI that analyzes sentiment and returns strict JSON only.";

        var textResponse = await CallGeminiAsync(systemInstruction, prompt);
        var jsonResult = textResponse.Trim().Replace("```json", "").Replace("```", "").Trim();

        var analysis = JsonSerializer.Deserialize<ReviewAnalysisResult>(jsonResult);

        var review = new Review {
            ProductId = dto.ProductId,
            CustomerName = dto.CustomerName,
            Content = dto.Content,
            Rating = dto.Rating,
            Sentiment = analysis?.Sentiment,
            Score = analysis?.Score,
            Summary = analysis?.Summary
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        return Ok(review);
    }

    // 3. POST /api/assistant/chat
    [HttpPost("assistant/chat")]
    public async Task<IActionResult> ShoppingAssistant([FromBody] ChatDto dto)
    {
        var products = await _context.Products.ToListAsync();
        var productJson = JsonSerializer.Serialize(products);

        var systemInstruction = $"You are a smart shopping assistant. Here is the product catalog database: {productJson}. Only answer questions about products in the Product table. Refuse unrelated questions.";

        var textResponse = await CallGeminiAsync(systemInstruction, dto.Message);

        return Ok(new { sessionId = dto.SessionId, reply = textResponse });
    }

    // 4. GET /api/reports/reviews
    [HttpGet("reports/reviews")]
    public async Task<IActionResult> GetReviewReport()
    {
        var report = await _context.Products
            .GroupJoin(_context.Reviews, p => p.ProductId, r => r.ProductId, (p, r) => new {
                ProductName = p.ProductName,
                TotalReviews = r.Count(),
                Positive = r.Count(x => x.Sentiment == "Positive"),
                Negative = r.Count(x => x.Sentiment == "Negative"),
                Neutral = r.Count(x => x.Sentiment == "Neutral"),
                AverageRating = r.Any() ? r.Average(x => (double?)x.Rating) ?? 0 : 0
            })
            .ToListAsync();

        int no = 1;
        var result = report.Select(x => new {
            No = no++,
            x.ProductName,
            x.TotalReviews,
            x.Positive,
            x.Negative,
            x.Neutral,
            AverageRating = Math.Round(x.AverageRating, 1)
        });

        return Ok(result);
    }
}
