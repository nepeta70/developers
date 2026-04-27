using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace ExchangeRateUpdater
{
    public class ExchangeRateProvider
    {
        private const string SourceUrl = "https://www.cnb.cz/en/financial-markets/foreign-exchange-market/central-bank-exchange-rate-fixing/central-bank-exchange-rate-fixing/daily.txt";
        private readonly Currency _targetCurrency = new("CZK");
        private readonly HttpClient _client = new();

        /// <summary>
        /// Should return exchange rates among the specified currencies that are defined by the source. But only those defined
        /// by the source, do not return calculated exchange rates. E.g. if the source contains "CZK/USD" but not "USD/CZK",
        /// do not return exchange rate "USD/CZK" with value calculated as 1 / "CZK/USD". If the source does not provide
        /// some of the currencies, ignore them.
        /// </summary>
        /// 
        public IEnumerable<ExchangeRate> GetExchangeRates(IEnumerable<Currency> currencies)
        {
            var requestedCurrencies = currencies.ToDictionary(c => c.Code, c => c, StringComparer.OrdinalIgnoreCase);

            if (requestedCurrencies.Count == 0)
            {
                return [];
            }

            string rawData;

            try
            {
                rawData = ReadRawData();
            }
            catch (HttpRequestException)
            {
                return [];
            }
            catch (IOException)
            {
                return [];
            }

            var lines = rawData.Split(["\n", "\r\n"], StringSplitOptions.RemoveEmptyEntries);

            var rates = new List<ExchangeRate>();

            // Skip header rows 0 and 1
            foreach (var line in lines.Skip(2))
            {
                try
                {
                    var cols = line.Split('|');
                    if (cols.Length < 5) continue;

                    var code = cols[3];
                    if (requestedCurrencies.ContainsKey(code))
                    {
                        if (int.TryParse(cols[2], out int amount) && amount > 0 &&
                            decimal.TryParse(cols[4], CultureInfo.InvariantCulture, out decimal rateValue))
                        {
                            decimal unitValue = rateValue / amount;

                            rates.Add(new ExchangeRate(
                                requestedCurrencies[code],
                                _targetCurrency,
                                unitValue
                            ));
                        }
                    }
                }
                catch (Exception) // Catch any parsing exceptions and skip the line
                {
                    continue;
                }
            }

            return rates;
        }

        private string ReadRawData()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, SourceUrl);
            using var response = _client.Send(request);

            response.EnsureSuccessStatusCode();

            using var reader = new StreamReader(response.Content.ReadAsStream());
            return reader.ReadToEnd();
        }
    }
}
