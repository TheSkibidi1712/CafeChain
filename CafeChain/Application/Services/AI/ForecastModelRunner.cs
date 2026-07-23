using CafeChain.Application.DTOs.AI;

namespace CafeChain.Application.Services.AI;

internal static class ForecastModelRunner
{
    internal sealed record ModelResult(string Name, decimal Mae, decimal Wape, List<decimal> Forecast, List<decimal> Residuals);

    public static ModelResult Select(IReadOnlyList<ForecastSeriesPointDto> series, int horizon)
    {
        var values = series.Select(x => x.Value).ToArray();
        var candidates = new List<Func<decimal[], int, decimal[]>>
        {
            SeasonalNaive,
            (x, h) => MovingAverage(x, h, 7),
            (x, h) => MovingAverage(x, h, 14),
            (x, h) => MovingAverage(x, h, 28)
        };
        var named = new[] { "SeasonalNaive", "MovingAverage7", "MovingAverage14", "MovingAverage28" };
        var results = candidates.Select((model, index) => Backtest(named[index], values, horizon, model)).ToList();
        foreach (var alpha in new[] { .1m, .3m, .5m, .7m, .9m })
            results.Add(Backtest($"ExponentialSmoothing-{alpha:0.0}", values, horizon, (x, h) => Exponential(x, h, alpha)));
        foreach (var parameters in new[] { (.2m, .05m, .2m), (.3m, .1m, .3m), (.5m, .1m, .3m) })
            results.Add(Backtest($"HoltWintersAdditive-{parameters.Item1:0.0}-{parameters.Item2:0.00}-{parameters.Item3:0.0}", values, horizon,
                (x, h) => HoltWintersAdditive(x, h, parameters.Item1, parameters.Item2, parameters.Item3)));
        return results.OrderBy(x => x.Wape).ThenBy(x => x.Mae).First();
    }

    private static ModelResult Backtest(string name, decimal[] values, int horizon, Func<decimal[], int, decimal[]> model)
    {
        var residuals = new List<decimal>();
        var absolute = 0m; var denominator = 0m; var count = 0;
        var foldSize = Math.Max(1, Math.Min(7, horizon));
        var first = Math.Max(28, values.Length - foldSize * 4);
        for (var cut = first; cut < values.Length; cut += foldSize)
        {
            var forecast = model(values[..cut], Math.Min(foldSize, values.Length - cut));
            for (var i = 0; i < forecast.Length; i++)
            {
                var error = values[cut + i] - forecast[i]; residuals.Add(error);
                absolute += Math.Abs(error); denominator += Math.Abs(values[cut + i]); count++;
            }
        }
        var future = model(values, horizon).Select(x => Math.Max(0, x)).ToList();
        return new ModelResult(name, count == 0 ? 0 : absolute / count,
            denominator == 0 ? 0 : absolute / denominator * 100, future, residuals);
    }

    private static decimal[] SeasonalNaive(decimal[] values, int horizon) =>
        Enumerable.Range(0, horizon).Select(i => values[Math.Max(0, values.Length - 7 + i % 7)]).ToArray();

    private static decimal[] MovingAverage(decimal[] values, int horizon, int window)
    {
        var average = values.TakeLast(Math.Min(window, values.Length)).DefaultIfEmpty().Average();
        return Enumerable.Repeat(average, horizon).ToArray();
    }

    private static decimal[] Exponential(decimal[] values, int horizon, decimal alpha)
    {
        var level = values[0];
        foreach (var value in values.Skip(1)) level = alpha * value + (1 - alpha) * level;
        return Enumerable.Repeat(level, horizon).ToArray();
    }

    private static decimal[] HoltWintersAdditive(decimal[] values, int horizon, decimal alpha, decimal beta, decimal gamma)
    {
        const int seasonLength = 7;
        if (values.Length < seasonLength * 2) return SeasonalNaive(values, horizon);
        var firstAverage = values.Take(seasonLength).Average();
        var secondAverage = values.Skip(seasonLength).Take(seasonLength).Average();
        var level = firstAverage; var trend = (secondAverage - firstAverage) / seasonLength;
        var seasonals = values.Take(seasonLength).Select(x => x - firstAverage).ToArray();
        for (var i = 0; i < values.Length; i++)
        {
            var index = i % seasonLength; var previousLevel = level;
            level = alpha * (values[i] - seasonals[index]) + (1 - alpha) * (level + trend);
            trend = beta * (level - previousLevel) + (1 - beta) * trend;
            seasonals[index] = gamma * (values[i] - level) + (1 - gamma) * seasonals[index];
        }
        return Enumerable.Range(1, horizon).Select(step => level + step * trend + seasonals[(values.Length + step - 1) % seasonLength]).ToArray();
    }
}
