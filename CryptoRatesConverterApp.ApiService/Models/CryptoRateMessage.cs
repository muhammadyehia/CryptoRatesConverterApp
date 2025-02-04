using System;
using System.Collections.Generic;

namespace KnabCryptoRatesConverterApp.ApiService.Models
{
    public class CryptoRateMessage
    {
        public string? CryptoSymbol { get; set; }
        public Dictionary<string, decimal> Rates { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }
} 