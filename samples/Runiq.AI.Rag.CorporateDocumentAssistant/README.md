# Corporate Document Assistant

## What this sample demonstrates

This sample is an OpenAI-backed RAG application built entirely on the production Runiq runtime. It registers the typed `corporate-documents` index, discovers Markdown files from a directory, selects `OpenAiEmbeddingModels.TextEmbedding3Small`, uses the DI-managed in-memory vector store, and ingests on startup. The same index then powers RAG Management, Agent Chat, the RAG lifecycle timeline, and grounding evidence.

The optional retrieval comparison endpoint also resolves the production `IRagRetriever`; it does not bypass the registered index or retrieval pipeline.

## Prerequisites

- .NET 10 SDK
- An OpenAI API key with access to `gpt-4.1-mini` and `text-embedding-3-small`

## Configure OpenAI

The sample reads `OpenAI:ApiKey`. Do not store the key in source control.

PowerShell:

```powershell
$env:OpenAI__ApiKey = "your-api-key"
```

Bash:

```bash
export OpenAI__ApiKey="your-api-key"
```

User secrets:

```powershell
dotnet user-secrets set "OpenAI:ApiKey" "your-api-key" --project samples/Runiq.AI.Rag.CorporateDocumentAssistant
```

## Run the sample

```powershell
dotnet run --project samples/Runiq.AI.Rag.CorporateDocumentAssistant
```

Open the `/dashboard` URL printed by ASP.NET Core. Startup fails with a short configuration error when the API key is missing; the key is not included in that error.

## Verify the RAG index

Open **RAG Management** and select `corporate-documents`. Verify:

- source type is `Directory`;
- ingestion strategy is `On startup`;
- vector store is `InMemory`;
- embedding reference is `openai/text-embedding-3-small`;
- readiness is `Ready` and the last operation is `Completed`;
- discovered-document, chunk, and embedding counters are positive.

Starting ingestion again from this screen uses the same managed runtime. Unchanged documents are reported as `Skipped` because their hashes have not changed.

## Ask questions in Agent Chat

Open **Agents**, select **Corporate Document Assistant**, and open Agent Chat. A covered answer can cite the selected policy as `[1]` and shows a separate **Sources cited** section below the answer. These validated citations map only markers actually emitted by the assistant to selected model context. The **Grounded with N sources** card remains broader evidence: it identifies all selected and rejected retrieval results even when the assistant did not cite every selected source.

The evidence represents the actual accepted model context. It is not a formal `[1]` citation system.

## Compare retrieval modes

After the index is ready, use the same production retriever through:

```text
/retrieval-demo/semantic?query=remote%20work
/retrieval-demo/lexical?query=%22manager%20approval%22
/retrieval-demo/hybrid?query=IRagRetriever
/retrieval-demo/hybrid?query=CS1503
/retrieval-demo/lexical?query=retrieval-policy.cs
/retrieval-demo/lexical?query=%22retrieval%20compatibility%20review%22
```

The JSON response keeps semantic score fields separate from `provenance.lexicalRawScore` and
`provenance.reciprocalRankFusionScore`, and reports semantic, lexical, and pre-limit fused candidate counts.
Quoted input demonstrates exact-phrase lexical intent; identifier input demonstrates token-preserving lexical matching.

## Covered example questions

- How many remote work days are employees allowed?
- Which expenses require manager approval?
- What should an employee do after discovering a security incident?

## Uncovered example question

- What is the company's parental leave policy?

For the uncovered question, the required-context policy returns a not-found answer without invoking the model. Agent Chat shows **No grounding context**, the structured no-context reason, and an empty selected-source list.

## Product evaluation

`Evaluation/corporate-rag-evaluation.json` is the versioned, credential-free validation set for this sample. It covers answerable and not-answerable questions, a semantically similar wrong-document candidate, exact technical identifiers, and an intentional manager-approval tie. `CorporateRagEvaluation` joins one observation to every case and reports context precision, Recall@K, MRR, NDCG, answerability precision/recall, wrong-answer prevention rate, and average reranking latency.

Run the automated evaluation contract and smoke flow with:

```powershell
dotnet test tests/Runiq.AI.Rag.Tests --filter CorporateRagEvaluationTests
```

The smoke test uses deterministic embeddings and a deterministic chat client so it needs no credential or network access, while retaining the production directory ingestion, in-memory vector store, retrieval, acceptance, sample reranker, context budget, citation extraction, and Agent Chat response projection. One covered question must reach the model and produce a citation; one parental-leave question must be classified `NotAnswerable` and must not invoke the model.

Metric interpretation:

- **Context precision** measures the relevant share of context actually selected for the model, not all retrieved candidates.
- **Recall@K**, **MRR**, and **NDCG** use the ordered document IDs at the configured `topK`; ties must retain deterministic source order.
- **Answerability precision/recall** compare aggregate reranker answerability with `expectedAnswerable`. Candidate answerability remains diagnostic.
- **Wrong-answer prevention rate** is the share of expected not-answerable cases for which model invocation was skipped.
- **Reranking latency** is recorded separately from retrieval and model latency and averaged across the set.

For release comparisons, run the same observations with reranking enabled and disabled and retain both reports. Compare ranking/answerability quality together with end-to-end latency; do not treat a latency improvement that reduces wrong-answer prevention as a quality-neutral win.

## How startup ingestion works

The `SampleDocuments` path is resolved from `AppContext.BaseDirectory`, so behavior does not depend on the current working directory. The project copies the bundled Markdown files to the build output. During host startup the managed ingestion runtime discovers documents, chunks them, creates OpenAI embeddings, writes them to the configured store, and marks the index ready only after the operation completes. A missing directory, embedding failure, or partial failure prevents false readiness.

Retrieved document text remains untrusted external context. It is delimited below system authority, and document-embedded instructions are not promoted into system instructions.

## In-memory store lifetime

Ingestion and retrieval resolve the same DI-managed store instance for the application lifetime. Restarting the application creates an empty store and runs `OnStartup` ingestion again.

## Troubleshooting

- **OpenAI API key is missing:** configure `OpenAI:ApiKey` with an environment variable or user secret.
- **`SampleDocuments` is missing:** rebuild the sample and confirm the folder exists beside the sample assembly.
- **Startup ingestion fails:** check the server log for credential, model-access, network, or document errors. Credentials and provider response bodies are not exposed in Dashboard payloads.
- **The index is not ready:** correct the startup error and restart. A failed initial ingestion intentionally prevents the application from appearing ready.
