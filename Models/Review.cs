using System.ComponentModel.DataAnnotations;

public class Review
{
    [Key]
    public int ReviewId { get; set; }
    public int ProductId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Sentiment { get; set; }
    public double? Score { get; set; }
    public string? Summary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class ReviewDto
{
    public int ProductId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Rating { get; set; }
}

public class ChatDto
{
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ReviewAnalysisResult
{
    public string Sentiment { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Summary { get; set; } = string.Empty;
}

