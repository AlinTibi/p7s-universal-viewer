# Gradual Pages migration

All six HTML URLs and the Google ownership file remain at their existing paths. No redirect is added. The old homepage stays self-canonical; the five overlapping secondary pages consolidate to that working homepage. Only the canonical homepage is advertised in the old sitemap. The ALMARFELD product link is labeled as pending reviewed deployment.

After the ALMARFELD product and guide are live and checked, review a separate canonical migration to those routes. Do not point canonicals at unpublished pages, delete legacy URLs or immediately redirect the traffic-bearing homepage. The old v1.0.0 installer links remain unchanged.

The previously cached Search Console sitemap failure cannot be declared resolved before this PR is deployed and Google rereads the publicly accessible XML. Do not repeatedly resubmit or request indexing.
