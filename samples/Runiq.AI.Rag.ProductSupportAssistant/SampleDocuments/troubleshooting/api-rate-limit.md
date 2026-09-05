---
documentType: troubleshooting
productArea: api
version: 4.8
publishedAt: 2026-08-27
visibility: public
plan: business
---
# Handle API rate limiting

HTTP 429 responses include `Retry-After` in seconds and `Northstar-RateLimit-Reset` as a UTC epoch value. Respect the larger delay, apply exponential backoff with jitter, and retry only idempotent operations unless the request uses a stable idempotency key.

Northstar Cloud 4.8 provides 600 requests per minute per tenant on Business and 1,500 on Enterprise. Short bursts may consume the full minute allocation. Parallel API tokens do not multiply a tenant's allowance.
