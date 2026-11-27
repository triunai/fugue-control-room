Research for D-064 search hardening, 2026-11-28, Meilisearch v1.12; UNVERIFIED marks kept

Source: a Haiku research agent (web search, Meilisearch docs). Section 1 is recorded in full below. Sections 2 to 5 (security
and scoped search, global-search UX, operations, other record types worth searching) arrived truncated: the search-hardening
lane re-derives them from the official docs and cites URLs.

## 1. Index settings

- searchableAttributes: order sets attribute ranking; put exact identifiers first: number, policyNumber, claimantName,
  incidentDescription, incidentLocation. https://meilisearch.com/docs/reference/api/settings ,
  https://meilisearch.com/docs/learn/relevancy/displayed_searchable_attributes
- displayedAttributes: return only what the UI needs; hidden fields stay searchable. (same source)
- filterableAttributes: market, line, status, claimantUserId must be listed to filter or facet on them.
  https://docs.meilisearch.com/learn/getting_started/filtering_and_sorting
- sortableAttributes: list every field used with sort (e.g. submittedAt); an unknown name is accepted silently, so check
  spelling. https://www.meilisearch.com/docs/capabilities/filtering_sorting_faceting/how_to/sort_results
- rankingRules: default words, typo, proximity, attributeRank, sort, wordPosition, exactness; each rule only breaks ties
  left by earlier rules, so keep words first. https://www.meilisearch.com/docs/capabilities/full_text_search/relevancy/ranking_rules
- typoTolerance: disableOnAttributes ["number", "policyNumber"] so codes match exactly; keep minWordSizeForTypos in the
  recommended ranges (oneTypo 2-8, twoTypos 4-14). https://www.meilisearch.com/docs/learn/relevancy/typo_tolerance_settings
- synonyms: sg <-> singapore, my <-> malaysia, and so on; synonyms apply to 1-3 word terms; check whether car/vehicle for
  motor add noise. Two-way behaviour UNVERIFIED.
  https://www.meilisearch.com/docs/capabilities/full_text_search/relevancy/synonyms
- stopWords: only true filler ("the", "a", "of"); never "sg".
  https://www.meilisearch.com/docs/capabilities/indexing/advanced/tokenization
- separatorTokens / nonSeparatorTokens: hyphens are probably default separators, so CLM-SG-2026-900013 splits into parts and
  "SG" matches every Singapore claim. UNVERIFIED; test before changing. (tokenization link above)
- matchingStrategy: last (default) drops words from the end when too few matches, suits search-as-you-type; all is stricter;
  frequency (1.9+) drops the most common terms first.
  https://www.meilisearch.com/docs/capabilities/full_text_search/how_to/use_matching_strategy
- Phrase failure: with last, "Fresh stack sink check" should still return partial matches; failing suggests the text is not in
  the indexed document or the field is not searchable. Verify first.
  https://www.meilisearch.com/docs/capabilities/full_text_search/getting_started/phrase_search
- Prefix: last word only; default indexingTime; quoted phrases match consecutively.
  https://www.meilisearch.com/docs/capabilities/full_text_search/how_to/configure_prefix_search
- Highlighting: attributesToHighlight and attributesToCrop; results under _formatted; crop defaults to 10 words.
  https://www.meilisearch.com/docs/capabilities/full_text_search/how_to/highlight_search_results
- Facets: facetDistribution gives counts for market, line and status chips; maxValuesPerFacet defaults to 100.
  https://www.meilisearch.com/docs/capabilities/filtering_sorting_faceting/how_to/handle_large_facet_cardinality

## Observed in Fugue (lead, 2026-11-28 02:52)
The claims index makes only number, policyNumber, incidentLocation, incidentDescription and claimantName searchable, so "motor",
"sg" and "Singapore" cannot match on market or line; the owner reports "Fresh stack sink check" did not match a claim whose
description is exactly that. Both go first in the hardening lane, each with a failing test before the fix.
