using System;
using System.Text.Json.Serialization;
using fhir.candle.Pages;
using Hl7.Fhir.Model;
using Microsoft.VisualBasic;

namespace fhircandle.Tests.Models;

public class MinimalStatus
{
    public class MinimalEvent
    {
        [JsonPropertyName("eventNumber")]
        public string EventNumber { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("focus")]
        public object? Focus { get; set; } = null;

        [JsonPropertyName("additionalContext")]
        public IEnumerable<object>? AdditionalContext { get; set; } = null;
        }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string NotificationType { get; set; } = string.Empty;

    [JsonPropertyName("eventsSinceSubscriptionStart")]
    public string EventsSinceSubscriptionStart { get; set; } = string.Empty;


}

