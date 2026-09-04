using System.Text.Json;

namespace Runiq.AI.Rag.CorporateDocumentAssistant.Evaluation;

internal sealed record CorporateRagEvaluationSet(int SchemaVersion, int TopK, IReadOnlyList<CorporateRagEvaluationCase> Cases);

internal sealed record CorporateRagEvaluationCase(
    string Id,
    string Category,
    string Query,
    bool ExpectedAnswerable,
    IReadOnlyList<string> RelevantDocuments,
    IReadOnlyList<string> DistractorDocuments,
    IReadOnlyList<string>? ExpectedTieDocuments = null);

internal sealed record CorporateRagEvaluationObservation(
    string CaseId,
    IReadOnlyList<string> RankedDocuments,
    bool PredictedAnswerable,
    bool ModelInvoked,
    TimeSpan RerankingDuration);

internal sealed record CorporateRagEvaluationReport(
    double ContextPrecision,
    double RecallAtK,
    double MeanReciprocalRank,
    double NormalizedDiscountedCumulativeGain,
    double AnswerabilityPrecision,
    double AnswerabilityRecall,
    double WrongAnswerPreventionRate,
    TimeSpan AverageRerankingLatency);

internal static class CorporateRagEvaluation
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static CorporateRagEvaluationSet Load(string path) =>
        JsonSerializer.Deserialize<CorporateRagEvaluationSet>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidOperationException("The corporate RAG evaluation set is empty.");

    public static CorporateRagEvaluationReport Calculate(
        CorporateRagEvaluationSet evaluationSet,
        IReadOnlyList<CorporateRagEvaluationObservation> observations)
    {
        var byId = observations.ToDictionary(item => item.CaseId, StringComparer.Ordinal);
        if (byId.Count != evaluationSet.Cases.Count || evaluationSet.Cases.Any(item => !byId.ContainsKey(item.Id)))
            throw new ArgumentException("Every evaluation case must have exactly one observation.", nameof(observations));

        var precision = new List<double>();
        var recall = new List<double>();
        var reciprocalRanks = new List<double>();
        var ndcg = new List<double>();
        var truePositive = 0;
        var falsePositive = 0;
        var falseNegative = 0;
        var prevented = 0;
        var notAnswerableCount = 0;

        foreach (var testCase in evaluationSet.Cases)
        {
            var observation = byId[testCase.Id];
            var ranked = observation.RankedDocuments.Take(evaluationSet.TopK).ToArray();
            var relevant = testCase.RelevantDocuments.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var relevantRanks = ranked.Select((document, index) => (document, rank: index + 1))
                .Where(item => relevant.Contains(item.document)).Select(item => item.rank).ToArray();

            precision.Add(ranked.Length == 0 ? (relevant.Count == 0 ? 1 : 0) : (double)relevantRanks.Length / ranked.Length);
            recall.Add(relevant.Count == 0 ? 1 : (double)relevantRanks.Length / relevant.Count);
            reciprocalRanks.Add(relevantRanks.Length == 0 ? 0 : 1d / relevantRanks[0]);
            var dcg = relevantRanks.Sum(rank => 1d / Math.Log2(rank + 1));
            var idealCount = Math.Min(relevant.Count, evaluationSet.TopK);
            var idealDcg = Enumerable.Range(1, idealCount).Sum(rank => 1d / Math.Log2(rank + 1));
            ndcg.Add(idealDcg == 0 ? 1 : dcg / idealDcg);

            if (testCase.ExpectedAnswerable && observation.PredictedAnswerable) truePositive++;
            if (!testCase.ExpectedAnswerable && observation.PredictedAnswerable) falsePositive++;
            if (testCase.ExpectedAnswerable && !observation.PredictedAnswerable) falseNegative++;
            if (!testCase.ExpectedAnswerable)
            {
                notAnswerableCount++;
                if (!observation.ModelInvoked) prevented++;
            }
        }

        return new CorporateRagEvaluationReport(
            precision.Average(), recall.Average(), reciprocalRanks.Average(), ndcg.Average(),
            Divide(truePositive, truePositive + falsePositive), Divide(truePositive, truePositive + falseNegative),
            Divide(prevented, notAnswerableCount),
            TimeSpan.FromTicks((long)observations.Average(item => item.RerankingDuration.Ticks)));
    }

    private static double Divide(int numerator, int denominator) => denominator == 0 ? 0 : (double)numerator / denominator;
}
